// 역할: 텍스트 붙여넣기 선택 팔레트 창을 화면의 적절한 위치에 띄우고 제어합니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
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

    /// <summary>
    /// 창을 숨긴 뒤 창이 있던 화면 영역을 강제로 다시 그리게 해서 검은 잔상이 남지 않게 합니다.
    /// </summary>
    public void HideAndRepaintBehind(Action hide)
    {
        if (_windowHandle == IntPtr.Zero || !User32.GetWindowRect(_windowHandle, out var bounds))
        {
            hide();
            return;
        }

        hide();
        User32.RedrawWindow(
            IntPtr.Zero,
            ref bounds,
            IntPtr.Zero,
            Win32Constants.RdwInvalidate
                | Win32Constants.RdwErase
                | Win32Constants.RdwAllchildren
                | Win32Constants.RdwUpdatenow);
    }

    public void Dispose()
    {
        _windowHandle = IntPtr.Zero;
    }
}
