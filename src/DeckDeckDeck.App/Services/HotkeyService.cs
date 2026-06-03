using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyIdBase = 1000;

    private readonly Dictionary<int, SlotKey> _hotkeysById = new();
    private readonly HashSet<int> _registeredIds = [];
    private HwndSource? _source;
    private IntPtr _windowHandle;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public IReadOnlyList<string> Start(IntPtr windowHandle)
    {
        if (_source is not null)
        {
            return [];
        }

        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);

        if (_source is null)
        {
            return ["global hotkey registration failed: window source was not ready"];
        }

        _source.AddHook(WndProc);

        var failures = new List<string>();
        for (var digit = 0; digit <= 9; digit++)
        {
            var id = HotkeyIdBase + digit;
            var slotKey = GetSlotKey(digit);
            var virtualKey = Win32Constants.VkNumpad0 + (uint)digit;
            var modifiers = Win32Constants.ModControl | Win32Constants.ModNoRepeat;

            _hotkeysById[id] = slotKey;

            if (User32.RegisterHotKey(windowHandle, id, modifiers, virtualKey))
            {
                _registeredIds.Add(id);
                continue;
            }

            var errorCode = Marshal.GetLastWin32Error();
            failures.Add($"global hotkey registration failed for Ctrl+Numpad {digit} (Win32 error {errorCode})");
        }

        return failures;
    }

    public void Dispose()
    {
        foreach (var id in _registeredIds)
        {
            User32.UnregisterHotKey(_windowHandle, id);
        }

        _registeredIds.Clear();
        _hotkeysById.Clear();

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _windowHandle = IntPtr.Zero;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != Win32Constants.WmHotkey)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        if (!_hotkeysById.TryGetValue(id, out var slotKey))
        {
            return IntPtr.Zero;
        }

        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(slotKey));
        handled = true;
        return IntPtr.Zero;
    }

    private static SlotKey GetSlotKey(int digit)
    {
        return digit switch
        {
            0 => SlotKey.Numpad0,
            1 => SlotKey.Numpad1,
            2 => SlotKey.Numpad2,
            3 => SlotKey.Numpad3,
            4 => SlotKey.Numpad4,
            5 => SlotKey.Numpad5,
            6 => SlotKey.Numpad6,
            7 => SlotKey.Numpad7,
            8 => SlotKey.Numpad8,
            9 => SlotKey.Numpad9,
            _ => throw new ArgumentOutOfRangeException(nameof(digit), digit, null)
        };
    }
}
