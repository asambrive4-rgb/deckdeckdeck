// 역할: 윈도우에 설치된 스포티파이(Spotify) 프로그램을 찾아 실행하는 기능을 구현합니다.
using System.Diagnostics;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class SpotifyAppLaunchGatewayAdapter : ISpotifyAppLaunchGateway
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public SpotifyAppLaunchGatewayAdapter()
        : this(Process.Start)
    {
    }

    internal SpotifyAppLaunchGatewayAdapter(Func<ProcessStartInfo, Process?> startProcess)
    {
        _startProcess = startProcess;
    }

    public bool TryLaunch()
    {
        try
        {
            _startProcess(new ProcessStartInfo
            {
                FileName = "spotify:",
                UseShellExecute = true
            });

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
