using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.IO;

namespace DeckDeckDeck.App.Infrastructure.Storage;

public sealed class FileLogger : IAppLogger
{
    private readonly AppStoragePaths _fileStorageService;

    public FileLogger(AppStoragePaths fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public void Log(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(_fileStorageService.LogsPath);
            var logPath = Path.Combine(_fileStorageService.LogsPath, "app.log");
            var line = exception is null
                ? $"{DateTimeOffset.Now:u} {message}"
                : $"{DateTimeOffset.Now:u} {message} {exception.GetType().Name}: {exception.Message}";

            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never interrupt the launcher flow.
        }
    }
}
