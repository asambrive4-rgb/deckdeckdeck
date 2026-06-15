using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

internal static class SettingsKeys
{
    public const string BringWindowToFrontOnHotkey = "bringWindowToFrontOnHotkey";
    public const string AutoHideAfterPaste = "autoHideAfterPaste";
    public const string RestoreClipboardAfterPaste = "restoreClipboardAfterPaste";
    public const string AutoBackupEnabled = "autoBackupEnabled";
    public const string BackupFolderPath = "backupFolderPath";
    public const string AutoBackupRetentionCount = "autoBackupRetentionCount";
    public const string LastBackupCreatedAt = "lastBackupCreatedAt";
    public const string SpotifyClientId = "spotifyClientId";
    public const string SpotifyAccessToken = "spotifyAccessToken";
    public const string SpotifyRefreshToken = "spotifyRefreshToken";
    public const string SpotifyTokenExpiresAt = "spotifyTokenExpiresAt";
    public const string SpotifyConnectedUserDisplayName = "spotifyConnectedUserDisplayName";
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
        new(SpotifyClientId, string.Empty),
        new(SpotifyAccessToken, string.Empty),
        new(SpotifyRefreshToken, string.Empty),
        new(SpotifyTokenExpiresAt, string.Empty),
        new(SpotifyConnectedUserDisplayName, string.Empty),
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
