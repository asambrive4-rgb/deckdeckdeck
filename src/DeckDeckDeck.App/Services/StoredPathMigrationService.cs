using DeckDeckDeck.App.Data;

namespace DeckDeckDeck.App.Services;

public sealed class StoredPathMigrationService
{
    private readonly AppDbContextFactory _dbContextFactory;
    private readonly FileStorageService _fileStorageService;

    public StoredPathMigrationService(
        AppDbContextFactory dbContextFactory,
        FileStorageService fileStorageService)
    {
        _dbContextFactory = dbContextFactory;
        _fileStorageService = fileStorageService;
    }

    public void NormalizeManagedPaths()
    {
        using var dbContext = _dbContextFactory.Create();
        var changed = false;

        foreach (var category in dbContext.Categories)
        {
            changed |= NormalizePath(value => category.ImagePath = value, category.ImagePath);
            changed |= NormalizePath(value => category.ThumbnailPath = value, category.ThumbnailPath);
        }

        foreach (var snippet in dbContext.Snippets)
        {
            changed |= NormalizePath(value => snippet.ImagePath = value, snippet.ImagePath);
            changed |= NormalizePath(value => snippet.ThumbnailPath = value, snippet.ThumbnailPath);
            changed |= NormalizePath(value => snippet.AutoIconPath = value, snippet.AutoIconPath);
        }

        if (changed)
        {
            dbContext.SaveChanges();
        }
    }

    private bool NormalizePath(Action<string?> setValue, string? currentValue)
    {
        if (!_fileStorageService.TryGetManagedRelativePath(currentValue, out var relativePath)
            || string.Equals(currentValue, relativePath, StringComparison.Ordinal))
        {
            return false;
        }

        setValue(relativePath);
        return true;
    }
}
