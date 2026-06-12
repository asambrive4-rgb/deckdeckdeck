using System.IO;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class SnippetImageService
{
    private readonly FileIconCacheService _fileIconCacheService;

    public SnippetImageService(FileIconCacheService fileIconCacheService)
    {
        _fileIconCacheService = fileIconCacheService;
    }

    public string? GetDisplayImagePath(Snippet? snippet)
    {
        return GetStoredDisplayImagePath(snippet);
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

    public static string? GetStoredDisplayImagePath(Snippet? snippet)
    {
        if (snippet is null)
        {
            return null;
        }

        return snippet.SlotImageMode switch
        {
            SlotImageMode.Custom => snippet.ThumbnailPath,
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.LaunchFile
                && !string.IsNullOrWhiteSpace(snippet.AutoIconPath)
                && File.Exists(snippet.AutoIconPath) => snippet.AutoIconPath,
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.MediaAction =>
                MediaIconResources.GetIconResourcePath(snippet.MediaCommand),
            _ => null
        };
    }
}
