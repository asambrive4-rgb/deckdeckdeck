using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SlotGridViewModelFactory
{
    private readonly IStoredImagePathResolver? _storedImagePathResolver;
    private readonly ISnippetImageResolver? _snippetImageResolver;

    public SlotGridViewModelFactory(
        IStoredImagePathResolver? storedImagePathResolver = null,
        ISnippetImageResolver? snippetImageResolver = null)
    {
        _storedImagePathResolver = storedImagePathResolver;
        _snippetImageResolver = snippetImageResolver;
    }

    public NumpadGridViewModel BuildCategoryGrid(
        IEnumerable<Category> categories,
        AppSettings settings,
        Action<SlotKey, Category?> onSelected,
        Action<SlotKey, Category?> onEdit,
        Action? onHotkeySelected = null)
    {
        var categoriesBySlot = categories.ToDictionary(category => category.SlotKey);

        return new NumpadGridViewModel(
            SlotKeyCatalog.All.Select(slotKey =>
        {
            categoriesBySlot.TryGetValue(slotKey, out var category);
            return new SlotViewModel(
                slotKey,
                category?.Name,
                ResolveDisplayPath(category?.ThumbnailPath),
                SlotRules.IsEnabled(slotKey, settings.EnabledCategorySlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, category),
                selectedSlotKey => onEdit(selectedSlotKey, category));
        }),
            onHotkeySelected is null
                ? HotkeyTileViewModel.Disabled()
                : HotkeyTileViewModel.Enabled(onHotkeySelected));
    }

    public NumpadGridViewModel BuildSnippetGrid(
        IEnumerable<Snippet> snippets,
        AppSettings settings,
        Action<SlotKey, Snippet?> onSelected,
        Action<SlotKey, Snippet?> onEdit)
    {
        var snippetsBySlot = snippets.ToDictionary(snippet => snippet.SlotKey);

        return new NumpadGridViewModel(
            SlotKeyCatalog.All.Select(slotKey =>
        {
            snippetsBySlot.TryGetValue(slotKey, out var snippet);
            var thumbnailPath = ResolveSnippetDisplayPath(snippet);
            return new SlotViewModel(
                slotKey,
                snippet?.Title,
                thumbnailPath,
                SlotRules.IsEnabled(slotKey, settings.EnabledSnippetSlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, snippet),
                selectedSlotKey => onEdit(selectedSlotKey, snippet));
        }),
            HotkeyTileViewModel.Disabled());
    }

    private string? ResolveDisplayPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || _storedImagePathResolver is null)
        {
            return path;
        }

        return _storedImagePathResolver.ResolveDisplayPath(path);
    }

    private string? ResolveSnippetDisplayPath(Snippet? snippet)
    {
        if (_snippetImageResolver is not null)
        {
            return _snippetImageResolver.GetDisplayImagePath(snippet);
        }

        if (snippet is null)
        {
            return null;
        }

        return snippet.SlotImageMode switch
        {
            SlotImageMode.Custom => ResolveDisplayPath(snippet.ThumbnailPath),
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.LaunchFile
                && !string.IsNullOrWhiteSpace(snippet.AutoIconPath)
                && CanDisplayStoredPath(snippet.AutoIconPath) =>
                ResolveDisplayPath(snippet.AutoIconPath),
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.MediaAction =>
                MediaIconResourcePaths.GetIconResourcePath(snippet.MediaCommand),
            _ => null
        };
    }

    private bool CanDisplayStoredPath(string storedPath)
    {
        return _storedImagePathResolver?.FileExists(storedPath) == true;
    }
}
