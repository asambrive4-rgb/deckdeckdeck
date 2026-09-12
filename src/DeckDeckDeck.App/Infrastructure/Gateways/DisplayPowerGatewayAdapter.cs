// 역할: 윈도우 운영체제에 신호를 보내 모니터 화면을 끄거나 켜는 시스템 제어를 수행합니다.
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class DisplayPowerGatewayAdapter : IDisplayPowerGateway
{
    private readonly Func<IntPtr, uint, IntPtr, IntPtr, bool> _postMessage;

    public DisplayPowerGatewayAdapter()
        : this(User32.PostMessage)
    {
    }

    internal DisplayPowerGatewayAdapter(Func<IntPtr, uint, IntPtr, IntPtr, bool> postMessage)
    {
        _postMessage = postMessage;
    }

    public bool TryTurnOffDisplay()
    {
        return _postMessage(
            Win32Constants.HwndBroadcast,
            Win32Constants.WmSyscommand,
            Win32Constants.ScMonitorpower,
            Win32Constants.MonitorPowerOff);
    }
}
