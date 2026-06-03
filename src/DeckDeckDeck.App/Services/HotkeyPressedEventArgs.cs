using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class HotkeyPressedEventArgs : EventArgs
{
    public HotkeyPressedEventArgs(SlotKey slotKey)
    {
        SlotKey = slotKey;
    }

    public SlotKey SlotKey { get; }
}
