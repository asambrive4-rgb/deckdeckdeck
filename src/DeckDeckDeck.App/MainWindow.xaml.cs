using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App;

public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeyService = new();
    private readonly NumpadCaptureService _numpadCaptureService = new();
    private readonly PaletteWindowService _paletteWindowService = new();
    private readonly WindowFocusService _windowFocusService = new();
    private IntPtr _windowHandle;
    private IntPtr _lastPasteTargetWindowHandle;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            () => _lastPasteTargetWindowHandle,
            HideAfterPaste,
            EnterEditMode,
            EndPasteSelection);
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Closed += OnClosed;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _numpadCaptureService.SlotCaptured += OnNumpadSlotCaptured;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!TryGetSlotKey(e.Key, out var slotKey))
        {
            return;
        }

        e.Handled = viewModel.SelectSlot(slotKey);
    }

    private static bool TryGetSlotKey(Key key, out SlotKey slotKey)
    {
        slotKey = key switch
        {
            Key.NumPad0 => SlotKey.Numpad0,
            Key.NumPad1 => SlotKey.Numpad1,
            Key.NumPad2 => SlotKey.Numpad2,
            Key.NumPad3 => SlotKey.Numpad3,
            Key.NumPad4 => SlotKey.Numpad4,
            Key.NumPad5 => SlotKey.Numpad5,
            Key.NumPad6 => SlotKey.Numpad6,
            Key.NumPad7 => SlotKey.Numpad7,
            Key.NumPad8 => SlotKey.Numpad8,
            Key.NumPad9 => SlotKey.Numpad9,
            Key.Divide => SlotKey.NumpadDivide,
            Key.Multiply => SlotKey.NumpadMultiply,
            Key.Subtract => SlotKey.NumpadSubtract,
            Key.Add => SlotKey.NumpadAdd,
            Key.Decimal => SlotKey.NumpadDecimal,
            _ => default
        };

        return key is Key.NumPad0
            or Key.NumPad1
            or Key.NumPad2
            or Key.NumPad3
            or Key.NumPad4
            or Key.NumPad5
            or Key.NumPad6
            or Key.NumPad7
            or Key.NumPad8
            or Key.NumPad9
            or Key.Divide
            or Key.Multiply
            or Key.Subtract
            or Key.Add
            or Key.Decimal;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _paletteWindowService.Attach(_windowHandle);

        var failures = _hotkeyService.Start(_windowHandle);

        if (failures.Count > 0 && DataContext is MainViewModel viewModel)
        {
            viewModel.ReportHotkeyRegistrationFailure(failures);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _numpadCaptureService.Dispose();
        _paletteWindowService.Dispose();
        _hotkeyService.Dispose();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
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
        Hide();
    }

    private void ShowPastePalette()
    {
        var settings = DataContext is MainViewModel viewModel
            ? viewModel.LoadSettings()
            : new AppSettings();

        if (!IsVisible)
        {
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
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Focus();
    }
}
