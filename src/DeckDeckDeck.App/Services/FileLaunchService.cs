using System.Diagnostics;
using System.IO;

namespace DeckDeckDeck.App.Services;

public sealed class FileLaunchService : IFileLaunchService
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public FileLaunchService()
        : this(Process.Start)
    {
    }

    internal FileLaunchService(Func<ProcessStartInfo, Process?> startProcess)
    {
        _startProcess = startProcess;
    }

    public bool TryLaunch(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        // LaunchFile is intentionally limited to file/folder paths, not shell commands or arguments.
        _startProcess(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });

        return true;
    }
}
