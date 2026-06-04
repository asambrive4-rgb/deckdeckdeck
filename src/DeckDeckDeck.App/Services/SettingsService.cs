using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class SettingsService
{
    private const string BringWindowToFrontOnHotkeyKey = "bringWindowToFrontOnHotkey";
    private const string AutoHideAfterPasteKey = "autoHideAfterPaste";
    private const string RestoreClipboardAfterPasteKey = "restoreClipboardAfterPaste";
    private const string HomeHotkeyKey = "homeHotkey";
    private const string DirectCategoryHotkeysKey = "directCategoryHotkeys";
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
            BringWindowToFrontOnHotkey = ReadBool(entries, BringWindowToFrontOnHotkeyKey, true),
            AutoHideAfterPaste = ReadBool(entries, AutoHideAfterPasteKey, true),
            RestoreClipboardAfterPaste = ReadBool(entries, RestoreClipboardAfterPasteKey, true),
            HomeHotkey = ReadString(entries, HomeHotkeyKey, "Ctrl + Numpad0"),
            DirectCategoryHotkeys = ReadString(entries, DirectCategoryHotkeysKey, "Ctrl + Numpad1~9"),
            EnabledSlotKeys = SlotKeyCatalog.All.ToDictionary(
                slotKey => slotKey,
                slotKey => ReadBool(entries, GetSlotEnabledKey(slotKey), true))
        };
    }

    public void EnsureDefaults()
    {
        using var dbContext = _dbContextFactory.Create();
        AddIfMissing(dbContext, BringWindowToFrontOnHotkeyKey, true.ToString());
        AddIfMissing(dbContext, AutoHideAfterPasteKey, true.ToString());
        AddIfMissing(dbContext, RestoreClipboardAfterPasteKey, true.ToString());
        AddIfMissing(dbContext, HomeHotkeyKey, "Ctrl + Numpad0");
        AddIfMissing(dbContext, DirectCategoryHotkeysKey, "Ctrl + Numpad1~9");

        foreach (var slotKey in SlotKeyCatalog.All)
        {
            AddIfMissing(dbContext, GetSlotEnabledKey(slotKey), true.ToString());
        }

        dbContext.SaveChanges();
    }

    public void SetSlotEnabled(SlotKey slotKey, bool enabled)
    {
        using var dbContext = _dbContextFactory.Create();
        Upsert(dbContext, GetSlotEnabledKey(slotKey), enabled.ToString());
        dbContext.SaveChanges();
    }

    public void Save(AppSettings settings)
    {
        using var dbContext = _dbContextFactory.Create();
        Upsert(dbContext, BringWindowToFrontOnHotkeyKey, settings.BringWindowToFrontOnHotkey.ToString());
        Upsert(dbContext, AutoHideAfterPasteKey, settings.AutoHideAfterPaste.ToString());
        Upsert(dbContext, RestoreClipboardAfterPasteKey, settings.RestoreClipboardAfterPaste.ToString());
        Upsert(dbContext, HomeHotkeyKey, settings.HomeHotkey);
        Upsert(dbContext, DirectCategoryHotkeysKey, settings.DirectCategoryHotkeys);

        foreach (var slotKey in SlotKeyCatalog.All)
        {
            var enabled = !settings.EnabledSlotKeys.TryGetValue(slotKey, out var value) || value;
            Upsert(dbContext, GetSlotEnabledKey(slotKey), enabled.ToString());
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

    private static void Upsert(AppDbContext dbContext, string key, string value)
    {
        var setting = dbContext.Settings.FirstOrDefault(item => item.Key == key);

        if (setting is null)
        {
            dbContext.Settings.Add(new SettingEntry { Key = key, Value = value });
            return;
        }

        setting.Value = value;
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
