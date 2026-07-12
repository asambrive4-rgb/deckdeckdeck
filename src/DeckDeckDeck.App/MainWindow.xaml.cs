using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Diagnostics;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App;

public partial class MainWindow : Window
{
    private readonly GlobalHotkeyRegistrar _globalHotkeyRegistrar = new();
    private DirectHotkeyCoordinator? _directHotkeyCoordinator;
    private readonly NumpadCapture _numpadCapture = new();
    private readonly PasteSelectionSession _pasteSelectionSession = new();
    private readonly PaletteWindowController _paletteWindowController = new();
    private readonly WindowPlacementResolver _windowPlacementResolver = new();
    private readonly Win32WindowFocusAdapter _windowFocusAdapter = new();
    private IntPtr _windowHandle;
    private IntPtr _lastPasteTargetWindowHandle;
    private bool _allowClose;
    private bool _isHidingToTrayFromClose;
    private bool _hasAppliedWindowPlacement;
    private readonly AppIconProvider _iconProvider;
    private readonly StartupTimingLog? _startupTiming;

    public MainWindow()
        : this(new AppIconProvider(), startupTiming: null)
    {
    }

    internal MainWindow(AppIconProvider iconProvider, StartupTimingLog? startupTiming)
    {
        _iconProvider = iconProvider;
        _startupTiming = startupTiming;
        using (_startupTiming?.Measure("main window initialize component"))
        {
            InitializeComponent();
        }

        UseApplicationIcon();
        WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
        _globalHotkeyRegistrar.HotkeyPressed += OnHotkeyPressed;
        _globalHotkeyRegistrar.HotkeyLongPressed += OnHotkeyLongPressed;
        _numpadCapture.SlotCaptured += OnNumpadSlotCaptured;
    }

    internal void AttachViewModel(MainViewModel viewModel)
    {
        DataContext = viewModel;
        _directHotkeyCoordinator = new DirectHotkeyCoordinator(
            new DirectHotkeyRegistrar(
                new ShouldPassThroughDirectHotkeyUseCase(new TextInputFocusDetector())),
            viewModel);
        _directHotkeyCoordinator.DirectHotkeyPressed += OnDirectHotkeyPressed;

        // Shell-first: SourceInitialized already placed default bottom-right (no settings yet).
        // Re-apply only when settings carry a real saved position — not empty / (0,0) /
        // near-top-left corruption — so the window does not jump after home load.
        if (_windowHandle != IntPtr.Zero)
        {
            StartDirectHotkeys(viewModel);
            var settings = viewModel.LoadSettings();
            if (WindowPlacementRules.HasUsableSavedWindowPlacement(settings))
            {
                ApplyWindowPlacement(IntPtr.Zero, settings);
            }
        }
    }

    private void StartDirectHotkeys(MainViewModel viewModel)
    {
        List<string> failures;
        using (_startupTiming?.Measure("direct hotkey registration"))
        {
            failures = _directHotkeyCoordinator?.Start().ToList() ?? [];
        }

        if (failures.Count > 0)
        {
            viewModel.ReportHotkeyRegistrationFailure(failures);
        }
    }

    internal IntPtr GetPasteTargetWindowHandle()
    {
        return _lastPasteTargetWindowHandle;
    }

    internal Action CreatePasteSelectionCompletion()
    {
        return _pasteSelectionSession.CreateCompletion(EndPasteSelection);
    }

    private void UseApplicationIcon()
    {
        using (_startupTiming?.Measure("window icon load"))
        {
            var icon = _iconProvider.GetWindowIcon();
            if (icon is null)
            {
                return;
            }

            Icon = icon;
        }
    }

    internal void AllowCloseForExit()
    {
        _allowClose = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!NumpadKeyMap.TryGetSlotKey(e.Key, out var slotKey))
        {
            return;
        }

        e.Handled = viewModel.SelectSlot(slotKey);
    }

    private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        ApplyWindowCornerPreference();
        _paletteWindowController.Attach(_windowHandle);
        ApplyWindowPlacement(IntPtr.Zero);

        List<string> failures;
        using (_startupTiming?.Measure("global hotkey registration"))
        {
            failures = _globalHotkeyRegistrar.Start(_windowHandle).ToList();
        }

        // Direct hotkeys need MainViewModel; when shell-first, AttachViewModel starts them later.
        if (_directHotkeyCoordinator is not null)
        {
            using (_startupTiming?.Measure("direct hotkey registration"))
            {
                failures.AddRange(_directHotkeyCoordinator.Start());
            }
        }

        if (failures.Count > 0 && DataContext is MainViewModel viewModel)
        {
            viewModel.ReportHotkeyRegistrationFailure(failures);
        }
    }

    private void ApplyWindowCornerPreference()
    {
        try
        {
            // Windows 11 (build 22000) 이상인 경우에만 설정
            if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
            {
                var preference = (int)Native.DwmApi.DWM_WINDOW_CORNER_PREFERENCE.Round;
                Native.DwmApi.DwmSetWindowAttribute(
                    _windowHandle,
                    Native.DwmApi.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref preference,
                    sizeof(int));
            }
        }
        catch
        {
            // Fallback: OS 버전에 따른 P/Invoke 오류 또는 예외 발생 시 안전하게 무시
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SaveWindowPlacement();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Dispose();
        }

        _numpadCapture.Dispose();
        _paletteWindowController.Dispose();
        if (_directHotkeyCoordinator is not null)
        {
            _directHotkeyCoordinator.DirectHotkeyPressed -= OnDirectHotkeyPressed;
            _directHotkeyCoordinator.Dispose();
            _directHotkeyCoordinator = null;
        }
        _globalHotkeyRegistrar.Dispose();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _isHidingToTrayFromClose = true;
        try
        {
            SaveWindowPlacement();
            WindowState = WindowState.Minimized;
            Hide();
        }
        finally
        {
            _isHidingToTrayFromClose = false;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            if (_isHidingToTrayFromClose)
            {
                return;
            }

            SaveWindowPlacement();
            Hide();
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        RunOnUiThread(() => HandleHotkey(e.SlotKey));
    }

    private void OnNumpadSlotCaptured(object? sender, HotkeyPressedEventArgs e)
    {
        RunOnUiThread(() => SelectCapturedSlot(e.SlotKey));
    }

    private void OnHotkeyLongPressed(object? sender, HotkeyPressedEventArgs e)
    {
        RunOnUiThread(() => HandleHotkeyLongPress(e.SlotKey));
    }

    private void OnDirectHotkeyPressed(object? sender, DirectHotkeyPressedEventArgs e)
    {
        RunOnUiThread(() => HandleDirectHotkey(e.HotkeyActionId));
    }

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            RunUiActionSafely(action);
            return;
        }

        _ = Dispatcher.BeginInvoke(
            () => RunUiActionSafely(action),
            DispatcherPriority.Normal);
    }

    private void RunUiActionSafely(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ReportBackgroundStatus("Hotkey handling failed.");
            }
        }
    }

    private void HandleHotkey(SlotKey slotKey)
    {
        _lastPasteTargetWindowHandle = _windowFocusAdapter.GetForegroundWindow();
        EnterPasteMode();
        ShowPastePalette();

        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (slotKey == SlotKey.Numpad0)
        {
            viewModel.OpenHomeFromHotkey();
            return;
        }

        viewModel.OpenCategoryFromHotkey(slotKey);
    }

    private void HandleDirectHotkey(Guid hotkeyActionId)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        _lastPasteTargetWindowHandle = _windowFocusAdapter.GetForegroundWindow();
        _ = ExecuteDirectHotkeySafely(viewModel, hotkeyActionId);
    }

    private void HandleHotkeyLongPress(SlotKey slotKey)
    {
        if (slotKey != SlotKey.Numpad0)
        {
            return;
        }

        EndPasteSelection();
        WindowState = WindowState.Minimized;
    }

    private void SelectCapturedSlot(SlotKey slotKey)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectSlot(slotKey);
        }
    }

    private void EnterPasteMode()
    {
        _pasteSelectionSession.Start();
        _numpadCapture.Start(_windowHandle);
    }

    private async Task ExecuteDirectHotkeySafely(MainViewModel viewModel, Guid hotkeyActionId)
    {
        try
        {
            await viewModel.ExecuteDirectHotkeyAsync(hotkeyActionId);
        }
        catch
        {
            viewModel.ReportBackgroundStatus("핫키 실행 중 오류가 발생했습니다.");
        }
    }

    private void EndPasteSelection()
    {
        _numpadCapture.Stop();
    }

    internal void EnterEditMode()
    {
        EndPasteSelection();
        BringWindowToFrontForEdit();
    }

    internal void HideAfterPaste()
    {
        EndPasteSelection();
        SaveWindowPlacement();
        Hide();
    }

    private void ShowPastePalette()
    {
        var settings = DataContext is MainViewModel viewModel
            ? viewModel.LoadSettings()
            : new AppSettings();

        if (!IsVisible)
        {
            ApplyWindowPlacement(_lastPasteTargetWindowHandle, settings);
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            _paletteWindowController.ShowWithoutActivation();
        }

        var presentation = new PreparePastePalettePresentationUseCase().Execute(
            new PreparePastePalettePresentationRequest(settings));

        switch (presentation.ZOrderMode)
        {
            case PastePaletteZOrderMode.BringToFrontTemporarily:
                _paletteWindowController.BringToFrontWithoutActivation();
                Dispatcher.BeginInvoke(
                    () => _paletteWindowController.ReturnToNormalZOrderWithoutActivation(),
                    DispatcherPriority.ApplicationIdle);
                break;
            case PastePaletteZOrderMode.SendToBottom:
                _paletteWindowController.SendToBottomWithoutActivation();
                break;
        }
    }

    private void BringWindowToFrontForEdit()
    {
        if (!IsVisible)
        {
            ApplyWindowPlacement(IntPtr.Zero);
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Focus();
    }

    private void ApplyWindowPlacement(IntPtr fallbackWindowHandle, AppSettings? settings = null)
    {
        // Shell-first: before DataContext, use empty settings → default bottom-right.
        // After attach, reload settings; (0,0) saved during the Manual default bug is ignored
        // by WindowPlacementResolver so we do not jump back to the top-left.
        var effectiveSettings = settings
            ?? (DataContext is MainViewModel viewModel ? viewModel.LoadSettings() : new AppSettings());

        var placement = _windowPlacementResolver.ResolveForWindow(
            this,
            effectiveSettings,
            fallbackWindowHandle);
        Left = placement.Left;
        Top = placement.Top;
        _hasAppliedWindowPlacement = true;
    }

    private void SaveWindowPlacement()
    {
        if (DataContext is not MainViewModel viewModel
            || _windowHandle == IntPtr.Zero
            || !_hasAppliedWindowPlacement)
        {
            return;
        }

        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, GetWindowWidthForPlacement(), GetWindowHeightForPlacement())
            : RestoreBounds;

        if (!double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top))
        {
            return;
        }

        // Never persist WPF Manual origin — that was the shell-first corruption path.
        if (WindowPlacementRules.IsUnsetOrWpfManualOrigin(bounds.Left, bounds.Top))
        {
            return;
        }

        viewModel.SaveWindowPlacement(
            bounds.Left,
            bounds.Top,
            _windowPlacementResolver.GetScreenDeviceName(this));
    }

    private double GetWindowWidthForPlacement()
    {
        return ActualWidth > 0 ? ActualWidth : Width;
    }

    private double GetWindowHeightForPlacement()
    {
        return ActualHeight > 0 ? ActualHeight : Height;
    }
}

