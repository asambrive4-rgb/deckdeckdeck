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
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
