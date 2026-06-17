using System.Runtime.InteropServices;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class DirectHotkeyRegistrar : IDisposable
{
    private readonly Func<int, bool> _isKeyDown;
    private readonly object _syncRoot = new();
    private Dictionary<HotkeyGesture, Guid> _hotkeysByGesture = new();
    private HashSet<HotkeyGesture> _pressedGestures = [];
    private User32.LowLevelKeyboardProc? _keyboardHookCallback;
    private IntPtr _keyboardHookHandle;

    public DirectHotkeyRegistrar()
        : this(IsKeyDown)
    {
    }

    internal DirectHotkeyRegistrar(Func<int, bool> isKeyDown)
    {
        _isKeyDown = isKeyDown;
    }

    public event EventHandler<DirectHotkeyPressedEventArgs>? DirectHotkeyPressed;

    public bool IsSuspended { get; set; }

    public IReadOnlyList<string> Start()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            return [];
        }

        _keyboardHookCallback = KeyboardHookProc;
        _keyboardHookHandle = User32.SetWindowsHookEx(
            Win32Constants.WhKeyboardLl,
            _keyboardHookCallback,
            Kernel32.GetModuleHandle(null),
            dwThreadId: 0);

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            return [];
        }

        var errorCode = Marshal.GetLastWin32Error();
        _keyboardHookCallback = null;
        return [$"직접 실행 핫키 감지를 시작하지 못했습니다. (Win32 오류 {errorCode})"];
    }

    public void Refresh(IReadOnlyList<DirectHotkeyRegistration> registrations)
    {
        lock (_syncRoot)
        {
            _hotkeysByGesture = registrations
                .Where(registration => registration.Gesture.IsComplete)
                .GroupBy(registration => registration.Gesture)
                .ToDictionary(group => group.Key, group => group.First().HotkeyActionId);
            _pressedGestures = [];
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _hotkeysByGesture.Clear();
            _pressedGestures.Clear();
        }

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        _keyboardHookCallback = null;
    }

    internal bool HandleKeyboardEventForTest(int message, uint virtualKey)
    {
        if (IsSuspended)
        {
            return false;
        }

        if (message is Win32Constants.WmKeyup or Win32Constants.WmSyskeyup)
        {
            return ProcessKeyUp(virtualKey);
        }

        if (message is Win32Constants.WmKeydown or Win32Constants.WmSyskeydown)
        {
            return ProcessKeyDown(virtualKey);
        }

        return false;
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || IsSuspended)
        {
            return User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var keyboardInput = Marshal.PtrToStructure<User32.KeyboardLowLevelHookStruct>(lParam);
        var virtualKey = keyboardInput.VirtualKeyCode;

        if (message is Win32Constants.WmKeyup or Win32Constants.WmSyskeyup)
        {
            return HandleKeyUp(nCode, wParam, lParam, virtualKey);
        }

        if (message is Win32Constants.WmKeydown or Win32Constants.WmSyskeydown)
        {
            return HandleKeyDown(nCode, wParam, lParam, virtualKey);
        }

        return User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private IntPtr HandleKeyDown(int nCode, IntPtr wParam, IntPtr lParam, uint virtualKey)
    {
        return ProcessKeyDown(virtualKey)
            ? new IntPtr(1)
            : User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private IntPtr HandleKeyUp(int nCode, IntPtr wParam, IntPtr lParam, uint virtualKey)
    {
        return ProcessKeyUp(virtualKey)
            ? new IntPtr(1)
            : User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private bool ProcessKeyDown(uint virtualKey)
    {
        var gesture = new HotkeyGesture(virtualKey, GetCurrentModifiers());
        Guid hotkeyActionId;
        var shouldRaise = false;

        lock (_syncRoot)
        {
            if (!_hotkeysByGesture.TryGetValue(gesture, out hotkeyActionId))
            {
                return false;
            }

            shouldRaise = _pressedGestures.Add(gesture);
        }

        if (shouldRaise)
        {
            DirectHotkeyPressed?.Invoke(this, new DirectHotkeyPressedEventArgs(hotkeyActionId));
        }

        return true;
    }

    private bool ProcessKeyUp(uint virtualKey)
    {
        var shouldBlock = false;
        lock (_syncRoot)
        {
            var releasedGestures = _pressedGestures
                .Where(gesture => gesture.VirtualKey == virtualKey)
                .ToList();

            foreach (var gesture in releasedGestures)
            {
                _pressedGestures.Remove(gesture);
                shouldBlock = true;
            }
        }

        return shouldBlock;
    }

    private HotkeyModifiers GetCurrentModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        if (_isKeyDown(Win32Constants.VkControl))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (_isKeyDown(Win32Constants.VkShift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (_isKeyDown(Win32Constants.VkMenu))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (_isKeyDown(Win32Constants.VkLWin) || _isKeyDown(Win32Constants.VkRWin))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (User32.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}
