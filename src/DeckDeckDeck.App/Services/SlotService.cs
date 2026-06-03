using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Services;

public sealed class SlotService
{
    public NumpadGridViewModel BuildCategoryGrid(
        IEnumerable<Category> categories,
        AppSettings settings,
        Action<SlotKey, Category?> onSelected)
    {
        var categoriesBySlot = categories.ToDictionary(category => category.SlotKey);

        return new NumpadGridViewModel(SlotKeyCatalog.All.Select(slotKey =>
        {
            categoriesBySlot.TryGetValue(slotKey, out var category);
            return new SlotViewModel(
                slotKey,
                category?.Name,
                IsEnabled(slotKey, settings),
                selectedSlotKey => onSelected(selectedSlotKey, category));
        }));
    }

    public NumpadGridViewModel BuildSnippetGrid(
        IEnumerable<Snippet> snippets,
        AppSettings settings,
        Action<SlotKey, Snippet?> onSelected)
    {
        var snippetsBySlot = snippets.ToDictionary(snippet => snippet.SlotKey);

        return new NumpadGridViewModel(SlotKeyCatalog.All.Select(slotKey =>
        {
            snippetsBySlot.TryGetValue(slotKey, out var snippet);
            return new SlotViewModel(
                slotKey,
                snippet?.Title,
                IsEnabled(slotKey, settings),
                selectedSlotKey => onSelected(selectedSlotKey, snippet));
        }));
    }

    private static bool IsEnabled(SlotKey slotKey, AppSettings settings)
    {
        return !settings.EnabledSlotKeys.TryGetValue(slotKey, out var enabled) || enabled;
    }
}
