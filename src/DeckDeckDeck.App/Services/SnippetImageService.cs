using System.IO;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class SnippetImageService
{
    private readonly FileIconCacheService _fileIconCacheService;
    private readonly SnippetService _snippetService;

    public SnippetImageService(SnippetService snippetService, FileIconCacheService fileIconCacheService)
    {
        _snippetService = snippetService;
        _fileIconCacheService = fileIconCacheService;
    }

    public string? GetDisplayImagePath(Snippet? snippet)
    {
        if (snippet is null)
        {
            return null;
        }

        if (snippet.SlotImageMode == SlotImageMode.Custom)
        {
            return snippet.ThumbnailPath;
        }

        if (snippet.SlotImageMode != SlotImageMode.Auto || snippet.ActionType != SnippetActionType.LaunchFile)
        {
            return null;
        }

        var current = AutoIconCacheEntry.FromSnippet(snippet);
        var autoIcon = PrepareAutoIcon(snippet.ActionType, snippet.LaunchPath, current);
        if (autoIcon != current)
        {
            _snippetService.UpdateAutoIcon(snippet.Id, autoIcon);
        }

        return autoIcon?.IconPath;
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
            _ => null
        };
    }
}
