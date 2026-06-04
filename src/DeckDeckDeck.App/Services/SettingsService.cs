using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class SettingsService
{
    private readonly SettingEntryStore _entryStore;
    private readonly SlotSettingsService _slotSettingsService;

    public SettingsService(AppDbContextFactory dbContextFactory)
    {
        _entryStore = new SettingEntryStore(dbContextFactory);
        _slotSettingsService = new SlotSettingsService(_entryStore);
    }

    public AppSettings Load()
    {
        EnsureDefaults();

        var entries = _entryStore.LoadAll();

        return new AppSettings
        {
            BringWindowToFrontOnHotkey = SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.BringWindowToFrontOnHotkey,
                true),
            AutoHideAfterPaste = SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.AutoHideAfterPaste,
                true),
            RestoreClipboardAfterPaste = SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.RestoreClipboardAfterPaste,
                true),
            HomeHotkey = SettingsValueParser.ReadString(
                entries,
                SettingsKeys.HomeHotkey,
                "Ctrl + Numpad0"),
            DirectCategoryHotkeys = SettingsValueParser.ReadString(
                entries,
                SettingsKeys.DirectCategoryHotkeys,
                "Ctrl + Numpad1~9, /, *, -, +, ."),
            LastWindowLeft = SettingsValueParser.ReadNullableDouble(entries, SettingsKeys.LastWindowLeft),
            LastWindowTop = SettingsValueParser.ReadNullableDouble(entries, SettingsKeys.LastWindowTop),
            LastWindowScreenDeviceName = SettingsValueParser.ReadNullableString(
                entries,
                SettingsKeys.LastWindowScreenDeviceName),
            EnabledCategorySlotKeys = _slotSettingsService.ReadCategorySlotStates(entries),
            EnabledSnippetSlotKeys = _slotSettingsService.ReadSnippetSlotStates(entries)
        };
    }

    public void EnsureDefaults()
    {
        _entryStore.EnsureDefaults(SettingsKeys.Defaults.Concat(_slotSettingsService.GetDefaultEntries()));
    }

    public void SetCategorySlotEnabled(SlotKey slotKey, bool enabled)
    {
        _slotSettingsService.SetCategorySlotEnabled(slotKey, enabled);
    }

    public void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled)
    {
        _slotSettingsService.SetSnippetSlotEnabled(slotKey, enabled);
    }

    public void Save(AppSettings settings)
    {
        var values = new[]
        {
            new KeyValuePair<string, string>(
                SettingsKeys.BringWindowToFrontOnHotkey,
                settings.BringWindowToFrontOnHotkey.ToString()),
            new KeyValuePair<string, string>(
                SettingsKeys.AutoHideAfterPaste,
                settings.AutoHideAfterPaste.ToString()),
            new KeyValuePair<string, string>(
                SettingsKeys.RestoreClipboardAfterPaste,
                settings.RestoreClipboardAfterPaste.ToString()),
            new KeyValuePair<string, string>(SettingsKeys.HomeHotkey, settings.HomeHotkey),
            new KeyValuePair<string, string>(SettingsKeys.DirectCategoryHotkeys, settings.DirectCategoryHotkeys),
            new KeyValuePair<string, string>(
                SettingsKeys.LastWindowLeft,
                SettingsValueParser.FormatNullableDouble(settings.LastWindowLeft)),
            new KeyValuePair<string, string>(
                SettingsKeys.LastWindowTop,
                SettingsValueParser.FormatNullableDouble(settings.LastWindowTop)),
            new KeyValuePair<string, string>(
                SettingsKeys.LastWindowScreenDeviceName,
                settings.LastWindowScreenDeviceName ?? string.Empty)
        };

        _entryStore.UpsertMany(values.Concat(_slotSettingsService.ToSettingEntries(settings)));
    }
}
