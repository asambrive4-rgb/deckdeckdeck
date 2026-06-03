namespace DeckDeckDeck.App.Models;

public sealed class AppSettings
{
    public bool AutoHideAfterPaste { get; set; } = true;

    public bool RestoreClipboardAfterPaste { get; set; } = true;

    public Dictionary<SlotKey, bool> EnabledSlotKeys { get; set; } =
        SlotKeyCatalog.All.ToDictionary(slotKey => slotKey, _ => true);

    public string HomeHotkey { get; set; } = "Ctrl + Numpad0";

    public string DirectCategoryHotkeys { get; set; } = "Ctrl + Numpad1~9";
}
