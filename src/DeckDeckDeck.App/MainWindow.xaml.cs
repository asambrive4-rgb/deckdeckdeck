using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using DrawingIcon = System.Drawing.Icon;

namespace DeckDeckDeck.App;

public partial class MainWindow : Window
{
    private readonly GlobalHotkeyRegistrar _globalHotkeyRegistrar = new();
    private readonly DirectHotkeyRegistrar _directHotkeyRegistrar;
    private readonly NumpadCapture _numpadCapture = new();
    private readonly PasteSelectionSession _pasteSelectionSession = new();
    private readonly PaletteWindowController _paletteWindowController = new();
    private readonly WindowPlacementResolver _windowPlacementResolver = new();
    private readonly Win32WindowFocusAdapter _windowFocusAdapter = new();
    private IntPtr _windowHandle;
    private IntPtr _lastPasteTargetWindowHandle;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _directHotkeyRegistrar = new DirectHotkeyRegistrar(
            new ShouldPassThroughDirectHotkeyUseCase(new TextInputFocusDetector()));
        UseApplicationIcon();
        WindowStartupLocation = WindowStartupLocation.Manual;
        DataContext = MainViewModelFactory.CreateDefault(
            () => _lastPasteTargetWindowHandle,
            HideAfterPaste,
            EnterEditMode,
            completePasteSelection: null,
            createPasteSelectionCompletion: () => _pasteSelectionSession.CreateCompletion(EndPasteSelection));
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
        _globalHotkeyRegistrar.HotkeyPressed += OnHotkeyPressed;
        _globalHotkeyRegistrar.HotkeyLongPressed += OnHotkeyLongPressed;
        _directHotkeyRegistrar.DirectHotkeyPressed += OnDirectHotkeyPressed;
        _numpadCapture.SlotCaptured += OnNumpadSlotCaptured;
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.DirectHotkeysChanged += OnDirectHotkeysChanged;
        }
    }

    private void UseApplicationIcon()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            return;
        }

        using var icon = DrawingIcon.ExtractAssociatedIcon(processPath);
        if (icon is null)
        {
            return;
        }

        var imageSource = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        imageSource.Freeze();
        Icon = imageSource;
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

        var failures = _globalHotkeyRegistrar.Start(_windowHandle).ToList();
        failures.AddRange(_directHotkeyRegistrar.Start());
        RefreshDirectHotkeys();

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
            viewModel.DirectHotkeysChanged -= OnDirectHotkeysChanged;
            viewModel.Dispose();
        }

        _numpadCapture.Dispose();
        _paletteWindowController.Dispose();
        _directHotkeyRegistrar.Dispose();
        _globalHotkeyRegistrar.Dispose();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        WindowState = WindowState.Minimized;
        SaveWindowPlacement();
        Hide();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            SaveWindowPlacement();
            Hide();
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => HandleHotkey(e.SlotKey));
            return;
        }

        HandleHotkey(e.SlotKey);
    }

    private void OnNumpadSlotCaptured(object? sender, HotkeyPressedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SelectCapturedSlot(e.SlotKey));
            return;
        }

        SelectCapturedSlot(e.SlotKey);
    }

    private void OnHotkeyLongPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => HandleHotkeyLongPress(e.SlotKey));
            return;
        }

        HandleHotkeyLongPress(e.SlotKey);
    }

    private void OnDirectHotkeyPressed(object? sender, DirectHotkeyPressedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => HandleDirectHotkey(e.HotkeyActionId));
            return;
        }

        HandleDirectHotkey(e.HotkeyActionId);
    }

    private void OnDirectHotkeysChanged(object? sender, EventArgs e)
    {
        RefreshDirectHotkeys();
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

    private void RefreshDirectHotkeys()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        _directHotkeyRegistrar.Refresh(viewModel.LoadActiveDirectHotkeys());
        _directHotkeyRegistrar.IsSuspended = viewModel.IsCapturingHotkeyInput;
    }

    private void EndPasteSelection()
    {
        _numpadCapture.Stop();
    }

    private void EnterEditMode()
    {
        EndPasteSelection();
        BringWindowToFrontForEdit();
    }

    private void HideAfterPaste()
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
            ApplyWindowPlacement(_lastPasteTargetWindowHandle);
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            _paletteWindowController.ShowWithoutActivation();
        }

        if (settings.BringWindowToFrontOnHotkey)
        {
            _paletteWindowController.BringToFrontWithoutActivation();
            Dispatcher.BeginInvoke(
                () => _paletteWindowController.ReturnToNormalZOrderWithoutActivation(),
                DispatcherPriority.ApplicationIdle);
            return;
        }

        _paletteWindowController.SendToBottomWithoutActivation();
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

    private void ApplyWindowPlacement(IntPtr fallbackWindowHandle)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var placement = _windowPlacementResolver.ResolveForWindow(
            this,
            viewModel.LoadSettings(),
            fallbackWindowHandle);
        Left = placement.Left;
        Top = placement.Top;
    }

    private void SaveWindowPlacement()
    {
        if (DataContext is not MainViewModel viewModel || _windowHandle == IntPtr.Zero)
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

