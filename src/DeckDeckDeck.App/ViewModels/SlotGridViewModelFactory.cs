using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SlotGridViewModelFactory
{
    private readonly IStoredImagePathResolver? _storedImagePathResolver;

    public SlotGridViewModelFactory(IStoredImagePathResolver? storedImagePathResolver = null)
    {
        _storedImagePathResolver = storedImagePathResolver;
    }

    public NumpadGridViewModel BuildCategoryGrid(
        IEnumerable<Category> categories,
        AppSettings settings,
        Action<SlotKey, Category?> onSelected,
        Action<SlotKey, Category?> onEdit)
    {
        var categoriesBySlot = categories.ToDictionary(category => category.SlotKey);

        return new NumpadGridViewModel(SlotKeyCatalog.All.Select(slotKey =>
        {
            categoriesBySlot.TryGetValue(slotKey, out var category);
            return new SlotViewModel(
                slotKey,
                category?.Name,
                ResolveDisplayPath(category?.ThumbnailPath),
                SlotRules.IsEnabled(slotKey, settings.EnabledCategorySlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, category),
                selectedSlotKey => onEdit(selectedSlotKey, category));
        }));
    }

    public NumpadGridViewModel BuildSnippetGrid(
        IEnumerable<Snippet> snippets,
        AppSettings settings,
        Action<SlotKey, Snippet?> onSelected,
        Action<SlotKey, Snippet?> onEdit)
    {
        var snippetsBySlot = snippets.ToDictionary(snippet => snippet.SlotKey);

        return new NumpadGridViewModel(SlotKeyCatalog.All.Select(slotKey =>
        {
            snippetsBySlot.TryGetValue(slotKey, out var snippet);
            var thumbnailPath = SnippetImageResolver.GetStoredDisplayImagePath(snippet, _storedImagePathResolver);
            return new SlotViewModel(
                slotKey,
                snippet?.Title,
                thumbnailPath,
                SlotRules.IsEnabled(slotKey, settings.EnabledSnippetSlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, snippet),
                selectedSlotKey => onEdit(selectedSlotKey, snippet));
        }));
    }

    private string? ResolveDisplayPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || _storedImagePathResolver is null)
        {
            return path;
        }

        return _storedImagePathResolver.ResolveDisplayPath(path);
    }
}
