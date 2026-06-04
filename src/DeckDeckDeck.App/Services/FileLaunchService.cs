using System.Diagnostics;
using System.IO;

namespace DeckDeckDeck.App.Services;

public sealed class FileLaunchService : IFileLaunchService
{
    public bool TryLaunch(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        // LaunchFile is intentionally limited to file/folder paths, not shell commands or arguments.
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });

        return process is not null;
    }
}
