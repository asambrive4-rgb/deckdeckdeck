using System.Diagnostics;

namespace DeckDeckDeck.App.Services;

public sealed class SpotifyAppLaunchService : ISpotifyAppLaunchService
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public SpotifyAppLaunchService()
        : this(Process.Start)
    {
    }

    internal SpotifyAppLaunchService(Func<ProcessStartInfo, Process?> startProcess)
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
