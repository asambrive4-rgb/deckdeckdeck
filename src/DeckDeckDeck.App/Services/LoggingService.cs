using System.IO;

namespace DeckDeckDeck.App.Services;

public sealed class LoggingService
{
    private readonly FileStorageService _fileStorageService;

    public LoggingService(FileStorageService fileStorageService)
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
