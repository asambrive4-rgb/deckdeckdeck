using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class PaletteWindowController : IDisposable
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
