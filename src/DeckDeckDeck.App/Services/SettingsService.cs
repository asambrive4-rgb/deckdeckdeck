using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class SettingsService
{
    private const string AutoHideAfterPasteKey = "autoHideAfterPaste";
    private const string RestoreClipboardAfterPasteKey = "restoreClipboardAfterPaste";
    private const string ShowDisabledSlotsKey = "showDisabledSlots";
    private const string HomeHotkeyKey = "homeHotkey";
    private const string DirectCategoryHotkeysKey = "directCategoryHotkeys";
    private const string ShowAdminPermissionNoticeKey = "showAdminPermissionNotice";
    private const string SlotEnabledPrefix = "enabledSlotKeys.";

    private readonly AppDbContextFactory _dbContextFactory;

    public SettingsService(AppDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public AppSettings Load()
    {
        EnsureDefaults();

        using var dbContext = _dbContextFactory.Create();
        var entries = dbContext.Settings.ToDictionary(setting => setting.Key, setting => setting.Value);

        return new AppSettings
        {
            AutoHideAfterPaste = ReadBool(entries, AutoHideAfterPasteKey, true),
            RestoreClipboardAfterPaste = ReadBool(entries, RestoreClipboardAfterPasteKey, true),
            ShowDisabledSlots = ReadBool(entries, ShowDisabledSlotsKey, true),
            HomeHotkey = ReadString(entries, HomeHotkeyKey, "Ctrl + Numpad0"),
            DirectCategoryHotkeys = ReadString(entries, DirectCategoryHotkeysKey, "Ctrl + Numpad1~9"),
            ShowAdminPermissionNotice = ReadBool(entries, ShowAdminPermissionNoticeKey, true),
            EnabledSlotKeys = SlotKeyCatalog.All.ToDictionary(
                slotKey => slotKey,
                slotKey => ReadBool(entries, GetSlotEnabledKey(slotKey), true))
        };
    }

    public void EnsureDefaults()
    {
        using var dbContext = _dbContextFactory.Create();
        AddIfMissing(dbContext, AutoHideAfterPasteKey, true.ToString());
        AddIfMissing(dbContext, RestoreClipboardAfterPasteKey, true.ToString());
        AddIfMissing(dbContext, ShowDisabledSlotsKey, true.ToString());
        AddIfMissing(dbContext, HomeHotkeyKey, "Ctrl + Numpad0");
        AddIfMissing(dbContext, DirectCategoryHotkeysKey, "Ctrl + Numpad1~9");
        AddIfMissing(dbContext, ShowAdminPermissionNoticeKey, true.ToString());

        foreach (var slotKey in SlotKeyCatalog.All)
        {
            AddIfMissing(dbContext, GetSlotEnabledKey(slotKey), true.ToString());
        }

        dbContext.SaveChanges();
    }

    private static void AddIfMissing(AppDbContext dbContext, string key, string value)
    {
        if (dbContext.Settings.Any(setting => setting.Key == key))
        {
            return;
        }

        dbContext.Settings.Add(new SettingEntry { Key = key, Value = value });
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
    {
        return values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string ReadString(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static string GetSlotEnabledKey(SlotKey slotKey)
    {
        return $"{SlotEnabledPrefix}{slotKey}";
    }
}
