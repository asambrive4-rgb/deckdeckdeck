using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class NumpadCaptureService : IDisposable
{
    private const int HotkeyIdBase = 2000;

    private readonly Dictionary<int, SlotKey> _hotkeysById = new();
    private readonly HashSet<int> _registeredIds = [];
    private HwndSource? _source;
    private IntPtr _windowHandle;

    public event EventHandler<HotkeyPressedEventArgs>? SlotCaptured;

    public bool IsCapturing { get; private set; }

    public void Start(IntPtr windowHandle)
    {
        if (IsCapturing)
        {
            return;
        }

        _windowHandle = windowHandle;
        if (_source is null)
        {
            _source = HwndSource.FromHwnd(windowHandle);
            _source?.AddHook(WndProc);
        }

        foreach (var (slotKey, virtualKey) in GetCapturedKeys())
        {
            var id = HotkeyIdBase + slotKey.GetSortOrder();
            _hotkeysById[id] = slotKey;

            if (User32.RegisterHotKey(windowHandle, id, Win32Constants.ModNoRepeat, virtualKey))
            {
                _registeredIds.Add(id);
            }
        }

        IsCapturing = _registeredIds.Count > 0;
    }

    public void Stop()
    {
        foreach (var id in _registeredIds)
        {
            User32.UnregisterHotKey(_windowHandle, id);
        }

        _registeredIds.Clear();
        _hotkeysById.Clear();
        IsCapturing = false;
    }

    public void Dispose()
    {
        Stop();

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

        SlotCaptured?.Invoke(this, new HotkeyPressedEventArgs(slotKey));
        handled = true;
        return IntPtr.Zero;
    }

    private static IReadOnlyList<(SlotKey SlotKey, uint VirtualKey)> GetCapturedKeys()
    {
        return
        [
            (SlotKey.Numpad0, Win32Constants.VkNumpad0),
            (SlotKey.Numpad1, Win32Constants.VkNumpad0 + 1),
            (SlotKey.Numpad2, Win32Constants.VkNumpad0 + 2),
            (SlotKey.Numpad3, Win32Constants.VkNumpad0 + 3),
            (SlotKey.Numpad4, Win32Constants.VkNumpad0 + 4),
            (SlotKey.Numpad5, Win32Constants.VkNumpad0 + 5),
            (SlotKey.Numpad6, Win32Constants.VkNumpad0 + 6),
            (SlotKey.Numpad7, Win32Constants.VkNumpad0 + 7),
            (SlotKey.Numpad8, Win32Constants.VkNumpad0 + 8),
            (SlotKey.Numpad9, Win32Constants.VkNumpad0 + 9),
            (SlotKey.NumpadDivide, Win32Constants.VkDivide),
            (SlotKey.NumpadMultiply, Win32Constants.VkMultiply),
            (SlotKey.NumpadSubtract, Win32Constants.VkSubtract),
            (SlotKey.NumpadAdd, Win32Constants.VkAdd),
            (SlotKey.NumpadDecimal, Win32Constants.VkDecimal)
        ];
    }
}
