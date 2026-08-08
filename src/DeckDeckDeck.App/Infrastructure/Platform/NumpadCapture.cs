using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class NumpadCapture : IDisposable
{
    private const int HotkeyIdBase = 2000;

    private readonly HashSet<uint> _hookKeysDown = [];
    private readonly Dictionary<uint, SlotKey> _hookSlotsByVirtualKey = new();
    private readonly Dictionary<int, SlotKey> _hotkeysById = new();
    private readonly Func<User32.LowLevelKeyboardProc, IntPtr> _installKeyboardHook;
    private readonly Func<int, bool> _isKeyDown;
    private readonly Func<IntPtr, int, uint, uint, bool> _registerHotKey;
    private readonly HashSet<int> _registeredIds = [];
    private readonly Func<IntPtr, bool> _uninstallKeyboardHook;
    private readonly Func<IntPtr, int, bool> _unregisterHotKey;
    private User32.LowLevelKeyboardProc? _keyboardHookCallback;
    private IntPtr _keyboardHookHandle;
    private HwndSource? _source;
    private IntPtr _windowHandle;

    public NumpadCapture()
        : this(
            User32.RegisterHotKey,
            User32.UnregisterHotKey,
            callback => User32.SetWindowsHookEx(
                Win32Constants.WhKeyboardLl,
                callback,
                Kernel32.GetModuleHandle(null),
                dwThreadId: 0),
            User32.UnhookWindowsHookEx,
            IsKeyDown)
    {
    }

    internal NumpadCapture(
        Func<IntPtr, int, uint, uint, bool> registerHotKey,
        Func<IntPtr, int, bool> unregisterHotKey,
        Func<User32.LowLevelKeyboardProc, IntPtr> installKeyboardHook,
        Func<IntPtr, bool> uninstallKeyboardHook,
        Func<int, bool> isKeyDown)
    {
        _registerHotKey = registerHotKey;
        _unregisterHotKey = unregisterHotKey;
        _installKeyboardHook = installKeyboardHook;
        _uninstallKeyboardHook = uninstallKeyboardHook;
        _isKeyDown = isKeyDown;
    }

    public event EventHandler<HotkeyPressedEventArgs>? SlotCaptured;

    public bool IsCapturing { get; private set; }

    public void Start(IntPtr windowHandle)
    {
        if (IsCapturing)
        {
            return;
        }

        _windowHandle = windowHandle;
        if (_source is null && windowHandle != IntPtr.Zero)
        {
            _source = HwndSource.FromHwnd(windowHandle);
            _source?.AddHook(WndProc);
        }

        foreach (var (slotKey, virtualKey) in NumpadKeyMap.GetVirtualKeys())
        {
            _hookSlotsByVirtualKey[virtualKey] = slotKey;
            var id = HotkeyIdBase + slotKey.GetSortOrder();
            if (_registerHotKey(windowHandle, id, Win32Constants.ModNoRepeat, virtualKey))
            {
                _hotkeysById[id] = slotKey;
                _registeredIds.Add(id);
            }
        }

        StartKeyboardHook();
        IsCapturing = _registeredIds.Count > 0 || _keyboardHookHandle != IntPtr.Zero;
    }

    public void Stop()
    {
        IsCapturing = false;
        foreach (var id in _registeredIds)
        {
            _unregisterHotKey(_windowHandle, id);
        }

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            _uninstallKeyboardHook(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        _keyboardHookCallback = null;
        _hookKeysDown.Clear();
        _hookSlotsByVirtualKey.Clear();
        _registeredIds.Clear();
        _hotkeysById.Clear();
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

    internal bool HandleKeyboardEventForTest(int message, uint virtualKey)
    {
        return ProcessKeyboardEvent(message, virtualKey);
    }

    private void StartKeyboardHook()
    {
        if (_hookSlotsByVirtualKey.Count == 0 || _keyboardHookHandle != IntPtr.Zero)
        {
            return;
        }

        _keyboardHookCallback = KeyboardHookProc;
        _keyboardHookHandle = _installKeyboardHook(_keyboardHookCallback);
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            return;
        }

        _keyboardHookCallback = null;
        _hookSlotsByVirtualKey.Clear();
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !IsCapturing)
        {
            return User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var keyboardInput = Marshal.PtrToStructure<User32.KeyboardLowLevelHookStruct>(lParam);
        return ProcessKeyboardEvent(message, keyboardInput.VirtualKeyCode)
            ? new IntPtr(1)
            : User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private bool ProcessKeyboardEvent(int message, uint virtualKey)
    {
        if (!IsCapturing || !_hookSlotsByVirtualKey.TryGetValue(virtualKey, out var slotKey))
        {
            return false;
        }

        if (message is Win32Constants.WmKeyup or Win32Constants.WmSyskeyup)
        {
            return _hookKeysDown.Remove(virtualKey);
        }

        if (message is not (Win32Constants.WmKeydown or Win32Constants.WmSyskeydown)
            || IsModifierPressed())
        {
            return false;
        }

        if (_hookKeysDown.Add(virtualKey))
        {
            SlotCaptured?.Invoke(this, new HotkeyPressedEventArgs(slotKey));
        }

        return true;
    }

    private bool IsModifierPressed()
    {
        return _isKeyDown(Win32Constants.VkControl)
            || _isKeyDown(Win32Constants.VkShift)
            || _isKeyDown(Win32Constants.VkMenu)
            || _isKeyDown(Win32Constants.VkLWin)
            || _isKeyDown(Win32Constants.VkRWin);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (User32.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}
