using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

public sealed class StoredPathMigration
{
    private readonly AppDbContextFactory _dbContextFactory;
    private readonly AppStoragePaths _fileStorageService;

    public StoredPathMigration(
        AppDbContextFactory dbContextFactory,
        AppStoragePaths fileStorageService)
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
