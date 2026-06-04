using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class PaletteWindowService : IDisposable
{
    private IntPtr _windowHandle;

    public void Attach(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
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
        _windowHandle = IntPtr.Zero;
    }
}
