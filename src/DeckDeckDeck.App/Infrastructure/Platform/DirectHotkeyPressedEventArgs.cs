namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class DirectHotkeyPressedEventArgs : EventArgs
{
    public DirectHotkeyPressedEventArgs(Guid hotkeyActionId)
    {
        HotkeyActionId = hotkeyActionId;
    }

    public Guid HotkeyActionId { get; }
}
