using System.IO;

namespace DeckDeckDeck.App.Services;

public interface IStoredImagePathResolver
{
    string? ResolveDisplayPath(string? storedPath);

    bool FileExists(string? storedPath);
}

public sealed class StoredImagePathResolver : IStoredImagePathResolver
{
    private readonly FileStorageService _fileStorageService;

    public StoredImagePathResolver(FileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public string? ResolveDisplayPath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        var absolutePath = _fileStorageService.ToAbsolutePath(storedPath);
        return File.Exists(absolutePath) ? absolutePath : storedPath;
    }

    public bool FileExists(string? storedPath)
    {
        return !string.IsNullOrWhiteSpace(storedPath)
            && File.Exists(_fileStorageService.ToAbsolutePath(storedPath));
    }
}
