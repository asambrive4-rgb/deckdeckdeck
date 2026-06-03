using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Services;

public sealed class SlotService
{
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
                category?.ThumbnailPath,
                IsEnabled(slotKey, settings),
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
            return new SlotViewModel(
                slotKey,
                snippet?.Title,
                snippet?.ThumbnailPath,
                IsEnabled(slotKey, settings),
                selectedSlotKey => onSelected(selectedSlotKey, snippet),
                selectedSlotKey => onEdit(selectedSlotKey, snippet));
        }));
    }

    private static bool IsEnabled(SlotKey slotKey, AppSettings settings)
    {
        return !settings.EnabledSlotKeys.TryGetValue(slotKey, out var enabled) || enabled;
    }
}
