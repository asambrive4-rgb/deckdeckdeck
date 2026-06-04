using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyIdBase = 1000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly Dictionary<int, SlotKey> _hotkeysById = new();
    private readonly Dictionary<uint, SlotKey> _hookFallbackHotkeysByVirtualKey = new();
    private readonly HashSet<uint> _hookFallbackKeysDown = [];
    private readonly HashSet<int> _registeredIds = [];
    private User32.LowLevelKeyboardProc? _keyboardHookCallback;
    private IntPtr _keyboardHookHandle;
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
            return ["전역 단축키를 등록하지 못했습니다: 창이 아직 준비되지 않았습니다."];
        }

        _source.AddHook(WndProc);

        var failures = new List<string>();
        foreach (var (slotKey, virtualKey) in GetRegisteredHotkeys())
        {
            var id = HotkeyIdBase + slotKey.GetSortOrder();
            var modifiers = Win32Constants.ModControl | Win32Constants.ModNoRepeat;

            _hotkeysById[id] = slotKey;

            if (User32.RegisterHotKey(windowHandle, id, modifiers, virtualKey))
            {
                _registeredIds.Add(id);
                continue;
            }

            var errorCode = Marshal.GetLastWin32Error();
            _hookFallbackHotkeysByVirtualKey[virtualKey] = slotKey;

            if (errorCode != ErrorHotkeyAlreadyRegistered)
            {
                failures.Add($"Ctrl+Numpad {slotKey.GetDisplayText()} 전역 단축키를 등록하지 못했습니다. (Win32 오류 {errorCode})");
            }
        }

        failures.AddRange(StartKeyboardHookFallback());

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
        _hookFallbackHotkeysByVirtualKey.Clear();
        _hookFallbackKeysDown.Clear();

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        _keyboardHookCallback = null;

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

    private IReadOnlyList<string> StartKeyboardHookFallback()
    {
        if (_hookFallbackHotkeysByVirtualKey.Count == 0 || _keyboardHookHandle != IntPtr.Zero)
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
        var failures = _hookFallbackHotkeysByVirtualKey.Values
            .Select(slotKey => $"Ctrl+Numpad {slotKey.GetDisplayText()} 보조 단축키 감지를 시작하지 못했습니다. (Win32 오류 {errorCode})")
            .ToArray();

        _hookFallbackHotkeysByVirtualKey.Clear();
        _keyboardHookCallback = null;
        return failures;
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var keyboardInput = Marshal.PtrToStructure<User32.KeyboardLowLevelHookStruct>(lParam);
        var virtualKey = keyboardInput.VirtualKeyCode;

        if (message is Win32Constants.WmKeyup or Win32Constants.WmSyskeyup)
        {
            _hookFallbackKeysDown.Remove(virtualKey);
            return User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        if (message is Win32Constants.WmKeydown or Win32Constants.WmSyskeydown
            && TryGetHookFallbackSlotKey(virtualKey, out var slotKey))
        {
            if (_hookFallbackKeysDown.Add(virtualKey))
            {
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(slotKey));
            }

            return new IntPtr(1);
        }

        return User32.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private bool TryGetHookFallbackSlotKey(uint virtualKey, out SlotKey slotKey)
    {
        if (!_hookFallbackHotkeysByVirtualKey.TryGetValue(virtualKey, out slotKey))
        {
            return false;
        }

        return IsKeyDown(Win32Constants.VkControl)
            && !IsKeyDown(Win32Constants.VkShift)
            && !IsKeyDown(Win32Constants.VkMenu);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (User32.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    public static IReadOnlyList<(SlotKey SlotKey, uint VirtualKey)> GetRegisteredHotkeys()
    {
        return NumpadKeyMap.GetVirtualKeys();
    }
}
