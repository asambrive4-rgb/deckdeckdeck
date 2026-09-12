// 역할: 사용자가 지정한 인터넷 웹사이트 주소를 기본 웹 브라우저에서 열어줍니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Diagnostics;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class UrlLaunchGatewayAdapter : IUrlLaunchGateway
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public UrlLaunchGatewayAdapter()
        : this(Process.Start)
    {
    }

    internal UrlLaunchGatewayAdapter(Func<ProcessStartInfo, Process?> startProcess)
    {
        _startProcess = startProcess;
    }

    public bool TryLaunch(string url)
    {
        if (!UrlAddress.TryNormalize(url, out var normalizedUrl))
        {
            return false;
        }

        // LaunchUrl is intentionally limited to http/https web addresses.
        _startProcess(new ProcessStartInfo
        {
            FileName = normalizedUrl,
            UseShellExecute = true
        });

        return true;
    }
}
