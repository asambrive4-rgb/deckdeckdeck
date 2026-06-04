using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using System.Globalization;

namespace DeckDeckDeck.App.Services;

public sealed class SettingsService
{
    private const string BringWindowToFrontOnHotkeyKey = "bringWindowToFrontOnHotkey";
    private const string AutoHideAfterPasteKey = "autoHideAfterPaste";
    private const string RestoreClipboardAfterPasteKey = "restoreClipboardAfterPaste";
    private const string HomeHotkeyKey = "homeHotkey";
    private const string DirectCategoryHotkeysKey = "directCategoryHotkeys";
    private const string LastWindowLeftKey = "lastWindowLeft";
    private const string LastWindowTopKey = "lastWindowTop";
    private const string LastWindowScreenDeviceNameKey = "lastWindowScreenDeviceName";
    private const string CategorySlotEnabledPrefix = "enabledCategorySlotKeys.";
    private const string SnippetSlotEnabledPrefix = "enabledSnippetSlotKeys.";

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
            DirectCategoryHotkeys = ReadString(entries, DirectCategoryHotkeysKey, "Ctrl + Numpad1~9, /, *, -, +, ."),
            LastWindowLeft = ReadNullableDouble(entries, LastWindowLeftKey),
            LastWindowTop = ReadNullableDouble(entries, LastWindowTopKey),
            LastWindowScreenDeviceName = ReadNullableString(entries, LastWindowScreenDeviceNameKey),
            EnabledCategorySlotKeys = SlotKeyCatalog.All.ToDictionary(
                slotKey => slotKey,
                slotKey => ReadBool(entries, GetCategorySlotEnabledKey(slotKey), true)),
            EnabledSnippetSlotKeys = SlotKeyCatalog.All.ToDictionary(
                slotKey => slotKey,
                slotKey => ReadBool(entries, GetSnippetSlotEnabledKey(slotKey), true))
        };
    }

    public void EnsureDefaults()
    {
        using var dbContext = _dbContextFactory.Create();
        AddIfMissing(dbContext, BringWindowToFrontOnHotkeyKey, true.ToString());
        AddIfMissing(dbContext, AutoHideAfterPasteKey, true.ToString());
        AddIfMissing(dbContext, RestoreClipboardAfterPasteKey, true.ToString());
        AddIfMissing(dbContext, HomeHotkeyKey, "Ctrl + Numpad0");
        AddIfMissing(dbContext, DirectCategoryHotkeysKey, "Ctrl + Numpad1~9, /, *, -, +, .");
        AddIfMissing(dbContext, LastWindowLeftKey, string.Empty);
        AddIfMissing(dbContext, LastWindowTopKey, string.Empty);
        AddIfMissing(dbContext, LastWindowScreenDeviceNameKey, string.Empty);

        foreach (var slotKey in SlotKeyCatalog.All)
        {
            AddIfMissing(dbContext, GetCategorySlotEnabledKey(slotKey), true.ToString());
            AddIfMissing(dbContext, GetSnippetSlotEnabledKey(slotKey), true.ToString());
        }

        dbContext.SaveChanges();
    }

    public void SetCategorySlotEnabled(SlotKey slotKey, bool enabled)
    {
        using var dbContext = _dbContextFactory.Create();
        Upsert(dbContext, GetCategorySlotEnabledKey(slotKey), enabled.ToString());
        dbContext.SaveChanges();
    }

    public void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled)
    {
        using var dbContext = _dbContextFactory.Create();
        Upsert(dbContext, GetSnippetSlotEnabledKey(slotKey), enabled.ToString());
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
        Upsert(dbContext, LastWindowLeftKey, FormatNullableDouble(settings.LastWindowLeft));
        Upsert(dbContext, LastWindowTopKey, FormatNullableDouble(settings.LastWindowTop));
        Upsert(dbContext, LastWindowScreenDeviceNameKey, settings.LastWindowScreenDeviceName ?? string.Empty);

        foreach (var slotKey in SlotKeyCatalog.All)
        {
            var categoryEnabled = !settings.EnabledCategorySlotKeys.TryGetValue(slotKey, out var categoryValue)
                || categoryValue;
            var snippetEnabled = !settings.EnabledSnippetSlotKeys.TryGetValue(slotKey, out var snippetValue)
                || snippetValue;
            Upsert(dbContext, GetCategorySlotEnabledKey(slotKey), categoryEnabled.ToString());
            Upsert(dbContext, GetSnippetSlotEnabledKey(slotKey), snippetEnabled.ToString());
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

    private static double? ReadNullableDouble(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadNullableString(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string FormatNullableDouble(double? value)
    {
        return value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string GetCategorySlotEnabledKey(SlotKey slotKey)
    {
        return $"{CategorySlotEnabledPrefix}{slotKey}";
    }

    private static string GetSnippetSlotEnabledKey(SlotKey slotKey)
    {
        return $"{SnippetSlotEnabledPrefix}{slotKey}";
    }
}
