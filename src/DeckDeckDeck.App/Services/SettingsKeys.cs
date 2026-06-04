using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

internal static class SettingsKeys
{
    public const string BringWindowToFrontOnHotkey = "bringWindowToFrontOnHotkey";
    public const string AutoHideAfterPaste = "autoHideAfterPaste";
    public const string RestoreClipboardAfterPaste = "restoreClipboardAfterPaste";
    public const string AutoBackupEnabled = "autoBackupEnabled";
    public const string BackupFolderPath = "backupFolderPath";
    public const string AutoBackupRetentionCount = "autoBackupRetentionCount";
    public const string LastBackupCreatedAt = "lastBackupCreatedAt";
    public const string HomeHotkey = "homeHotkey";
    public const string DirectCategoryHotkeys = "directCategoryHotkeys";
    public const string LastWindowLeft = "lastWindowLeft";
    public const string LastWindowTop = "lastWindowTop";
    public const string LastWindowScreenDeviceName = "lastWindowScreenDeviceName";

    private const string CategorySlotEnabledPrefix = "enabledCategorySlotKeys.";
    private const string SnippetSlotEnabledPrefix = "enabledSnippetSlotKeys.";

    public static IReadOnlyList<KeyValuePair<string, string>> Defaults { get; } =
    [
        new(BringWindowToFrontOnHotkey, true.ToString()),
        new(AutoHideAfterPaste, true.ToString()),
        new(RestoreClipboardAfterPaste, true.ToString()),
        new(AutoBackupEnabled, false.ToString()),
        new(BackupFolderPath, string.Empty),
        new(AutoBackupRetentionCount, "10"),
        new(LastBackupCreatedAt, string.Empty),
        new(HomeHotkey, "Ctrl + Numpad0"),
        new(DirectCategoryHotkeys, "Ctrl + Numpad1~9, /, *, -, +, ."),
        new(LastWindowLeft, string.Empty),
        new(LastWindowTop, string.Empty),
        new(LastWindowScreenDeviceName, string.Empty)
    ];

    public static string GetCategorySlotEnabledKey(SlotKey slotKey)
    {
        return $"{CategorySlotEnabledPrefix}{slotKey}";
    }

    public static string GetSnippetSlotEnabledKey(SlotKey slotKey)
    {
        return $"{SnippetSlotEnabledPrefix}{slotKey}";
    }
}
