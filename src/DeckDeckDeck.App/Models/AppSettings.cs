namespace DeckDeckDeck.App.Models;

public sealed class AppSettings
{
    public bool BringWindowToFrontOnHotkey { get; set; } = true;

    public bool AutoHideAfterPaste { get; set; } = true;

    public bool RestoreClipboardAfterPaste { get; set; } = true;

    public bool AutoBackupEnabled { get; set; }

    public string BackupFolderPath { get; set; } = string.Empty;

    public int AutoBackupRetentionCount { get; set; } = 10;

    public DateTimeOffset? LastBackupCreatedAt { get; set; }

    public Dictionary<SlotKey, bool> EnabledCategorySlotKeys { get; set; } =
        SlotKeyCatalog.All.ToDictionary(slotKey => slotKey, _ => true);

    public Dictionary<SlotKey, bool> EnabledSnippetSlotKeys { get; set; } =
        SlotKeyCatalog.All.ToDictionary(slotKey => slotKey, _ => true);

    public string HomeHotkey { get; set; } = "Ctrl + Numpad0";

    public string DirectCategoryHotkeys { get; set; } = "Ctrl + Numpad1~9, /, *, -, +, .";

    public double? LastWindowLeft { get; set; }

    public double? LastWindowTop { get; set; }

    public string? LastWindowScreenDeviceName { get; set; }
}
