using System.Windows.Interop;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class PaletteWindowService : IDisposable
{
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private bool _isPasteMode;

    public void Attach(IntPtr windowHandle)
    {
        if (_source is not null)
        {
            return;
        }

        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);
        _source?.AddHook(WndProc);
    }

    public void SetPasteMode(bool enabled)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        _isPasteMode = enabled;

        var style = User32.GetWindowLongPtr(_windowHandle, Win32Constants.GwlExStyle).ToInt64();
        var nextStyle = enabled
            ? style | Win32Constants.WsExNoactivate
            : style & ~Win32Constants.WsExNoactivate;

        User32.SetWindowLongPtr(_windowHandle, Win32Constants.GwlExStyle, new IntPtr(nextStyle));
    }

    public void BringToFrontWithoutActivation()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        ShowWithoutActivation();
        User32.SetWindowPos(
            _windowHandle,
            Win32Constants.HwndTopmost,
            0,
            0,
            0,
            0,
            Win32Constants.SwpNomove
                | Win32Constants.SwpNosize
                | Win32Constants.SwpNoactivate
                | Win32Constants.SwpShowwindow);
    }

    public void ReturnToNormalZOrderWithoutActivation()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        User32.SetWindowPos(
            _windowHandle,
            Win32Constants.HwndNotopmost,
            0,
            0,
            0,
            0,
            Win32Constants.SwpNomove
                | Win32Constants.SwpNosize
                | Win32Constants.SwpNoactivate
                | Win32Constants.SwpShowwindow);
    }

    public void ShowWithoutActivation()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        User32.ShowWindow(_windowHandle, Win32Constants.SwShownoactivate);
    }

    public void SendToBottomWithoutActivation()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        User32.SetWindowPos(
            _windowHandle,
            Win32Constants.HwndBottom,
            0,
            0,
            0,
            0,
            Win32Constants.SwpNomove
                | Win32Constants.SwpNosize
                | Win32Constants.SwpNoactivate
                | Win32Constants.SwpShowwindow);
    }

    public void Dispose()
    {
        SetPasteMode(false);

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _windowHandle = IntPtr.Zero;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_isPasteMode || message != Win32Constants.WmMouseActivate)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(Win32Constants.MaNoActivate);
    }
}
