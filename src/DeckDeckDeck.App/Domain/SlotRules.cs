using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public static class SlotRules
{
    public static bool IsEnabled(
        SlotKey slotKey,
        IReadOnlyDictionary<SlotKey, bool> enabledSlotKeys)
    {
        return !enabledSlotKeys.TryGetValue(slotKey, out var enabled) || enabled;
    }
}
