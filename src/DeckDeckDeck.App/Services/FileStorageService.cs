using System.IO;

namespace DeckDeckDeck.App.Services;

public sealed class FileStorageService
{
    private const string AppFolderName = "NumpadPromptLauncher";

    public FileStorageService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName))
    {
    }

    public FileStorageService(string appDataPath)
    {
        AppDataPath = appDataPath;
        DatabasePath = Path.Combine(AppDataPath, "launcher.db");
        ImagesPath = Path.Combine(AppDataPath, "images");
        ImageOriginalsPath = Path.Combine(ImagesPath, "originals");
        ImageThumbnailsPath = Path.Combine(ImagesPath, "thumbnails");
        LogsPath = Path.Combine(AppDataPath, "logs");
        TempPath = Path.Combine(AppDataPath, "temp");
    }

    public string AppDataPath { get; }

    public string DatabasePath { get; }

    public string ImagesPath { get; }

    public string ImageOriginalsPath { get; }

    public string ImageThumbnailsPath { get; }

    public string LogsPath { get; }

    public string TempPath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(ImageOriginalsPath);
        Directory.CreateDirectory(ImageThumbnailsPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(TempPath);
    }
}
