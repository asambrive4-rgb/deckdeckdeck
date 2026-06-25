using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.IO;

namespace DeckDeckDeck.App.Infrastructure.Storage;

public sealed class SnippetImageResolver : ISnippetImageResolver
{
    private readonly FileIconCacheRepository _fileIconCacheService;
    private readonly IStoredImagePathResolver _storedImagePathResolver;

    public SnippetImageResolver(FileIconCacheRepository fileIconCacheService)
        : this(fileIconCacheService, new StoredImagePathResolver(new AppStoragePaths()))
    {
    }

    public SnippetImageResolver(
        FileIconCacheRepository fileIconCacheService,
        IStoredImagePathResolver storedImagePathResolver)
    {
        _fileIconCacheService = fileIconCacheService;
        _storedImagePathResolver = storedImagePathResolver;
    }

    public string? GetDisplayImagePath(Snippet? snippet)
    {
        return GetStoredDisplayImagePath(snippet, _storedImagePathResolver);
    }

    public AutoIconCacheEntry? PrepareAutoIcon(
        SnippetActionType actionType,
        string? launchPath,
        AutoIconCacheEntry? current)
    {
        return actionType == SnippetActionType.LaunchFile
            ? _fileIconCacheService.GetOrCreateIcon(launchPath, current)
            : null;
    }

    public static string? GetStoredDisplayImagePath(
        Snippet? snippet,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        if (snippet is null)
        {
            return null;
        }

        return snippet.SlotImageMode switch
        {
            SlotImageMode.Custom => ResolveDisplayPath(snippet.ThumbnailPath, storedImagePathResolver),
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.LaunchFile
                && !string.IsNullOrWhiteSpace(snippet.AutoIconPath)
                && CanDisplayStoredPath(snippet.AutoIconPath, storedImagePathResolver) =>
                ResolveDisplayPath(snippet.AutoIconPath, storedImagePathResolver),
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.MediaAction =>
                MediaIconResourcePaths.GetIconResourcePath(snippet.MediaCommand),
            _ => null
        };
    }

    private static string? ResolveDisplayPath(string? path, IStoredImagePathResolver? storedImagePathResolver)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return storedImagePathResolver?.ResolveDisplayPath(path) ?? path;
    }

    private static bool CanDisplayStoredPath(
        string storedPath,
        IStoredImagePathResolver? storedImagePathResolver)
    {
        return storedImagePathResolver?.FileExists(storedPath) == true
            || (Path.IsPathRooted(storedPath) && File.Exists(storedPath));
    }
}
