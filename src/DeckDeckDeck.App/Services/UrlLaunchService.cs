using System.Diagnostics;

namespace DeckDeckDeck.App.Services;

public sealed class UrlLaunchService : IUrlLaunchService
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public UrlLaunchService()
        : this(Process.Start)
    {
    }

    internal UrlLaunchService(Func<ProcessStartInfo, Process?> startProcess)
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
