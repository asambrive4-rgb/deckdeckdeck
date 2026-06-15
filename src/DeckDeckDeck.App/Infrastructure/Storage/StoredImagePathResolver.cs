using DeckDeckDeck.App.Composition;
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

public interface IStoredImagePathResolver
{
    string? ResolveDisplayPath(string? storedPath);

    bool FileExists(string? storedPath);
}

public sealed class StoredImagePathResolver : IStoredImagePathResolver
{
    private readonly AppStoragePaths _fileStorageService;

    public StoredImagePathResolver(AppStoragePaths fileStorageService)
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
