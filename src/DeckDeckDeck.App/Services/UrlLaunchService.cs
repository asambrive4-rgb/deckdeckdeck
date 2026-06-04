using System.Diagnostics;

namespace DeckDeckDeck.App.Services;

public sealed class UrlLaunchService : IUrlLaunchService
{
    public bool TryLaunch(string url)
    {
        if (!UrlAddress.TryNormalize(url, out var normalizedUrl))
        {
            return false;
        }

        // LaunchUrl is intentionally limited to http/https web addresses.
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = normalizedUrl,
            UseShellExecute = true
        });

        return process is not null;
    }
}
