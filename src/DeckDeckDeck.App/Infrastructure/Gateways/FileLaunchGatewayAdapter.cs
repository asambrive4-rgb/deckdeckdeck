using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Diagnostics;
using System.IO;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class FileLaunchGatewayAdapter : IFileLaunchGateway
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public FileLaunchGatewayAdapter()
        : this(Process.Start)
    {
    }

    internal FileLaunchGatewayAdapter(Func<ProcessStartInfo, Process?> startProcess)
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
