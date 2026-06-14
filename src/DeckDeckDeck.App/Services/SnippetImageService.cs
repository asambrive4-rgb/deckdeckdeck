using DeckDeckDeck.App.Models;
using System.IO;

namespace DeckDeckDeck.App.Services;

public sealed class SnippetImageService
{
    private readonly FileIconCacheService _fileIconCacheService;
    private readonly IStoredImagePathResolver _storedImagePathResolver;

    public SnippetImageService(FileIconCacheService fileIconCacheService)
        : this(fileIconCacheService, new StoredImagePathResolver(new FileStorageService()))
    {
    }

    public SnippetImageService(
        FileIconCacheService fileIconCacheService,
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
                MediaIconResources.GetIconResourcePath(snippet.MediaCommand),
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
