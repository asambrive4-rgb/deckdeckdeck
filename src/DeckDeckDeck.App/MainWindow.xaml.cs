using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DrawingIcon = System.Drawing.Icon;

namespace DeckDeckDeck.App;

public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeyService = new();
    private readonly NumpadCaptureService _numpadCaptureService = new();
    private readonly PaletteWindowService _paletteWindowService = new();
    private readonly WindowPlacementService _windowPlacementService = new();
    private readonly WindowFocusService _windowFocusService = new();
    private IntPtr _windowHandle;
    private IntPtr _lastPasteTargetWindowHandle;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        UseApplicationIcon();
        WindowStartupLocation = WindowStartupLocation.Manual;
        DataContext = new MainViewModel(
            () => _lastPasteTargetWindowHandle,
            HideAfterPaste,
            EnterEditMode,
            EndPasteSelection);
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _numpadCaptureService.SlotCaptured += OnNumpadSlotCaptured;
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
        _paletteWindowService.Attach(_windowHandle);
        ApplyWindowPlacement(IntPtr.Zero);

        var failures = _hotkeyService.Start(_windowHandle);

        if (failures.Count > 0 && DataContext is MainViewModel viewModel)
        {
            viewModel.ReportHotkeyRegistrationFailure(failures);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SaveWindowPlacement();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Dispose();
        }

        _numpadCaptureService.Dispose();
        _paletteWindowService.Dispose();
        _hotkeyService.Dispose();
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

    private void HandleHotkey(SlotKey slotKey)
    {
        _lastPasteTargetWindowHandle = _windowFocusService.GetForegroundWindow();
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

    private void SelectCapturedSlot(SlotKey slotKey)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectSlot(slotKey);
        }
    }

    private void EnterPasteMode()
    {
        _numpadCaptureService.Start(_windowHandle);
    }

    private void EndPasteSelection()
    {
        _numpadCaptureService.Stop();
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
            _paletteWindowService.ShowWithoutActivation();
        }

        if (settings.BringWindowToFrontOnHotkey)
        {
            _paletteWindowService.BringToFrontWithoutActivation();
            Dispatcher.BeginInvoke(
                () => _paletteWindowService.ReturnToNormalZOrderWithoutActivation(),
                DispatcherPriority.ApplicationIdle);
            return;
        }

        _paletteWindowService.SendToBottomWithoutActivation();
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

        var placement = _windowPlacementService.ResolveForWindow(
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
            _windowPlacementService.GetScreenDeviceName(this));
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
