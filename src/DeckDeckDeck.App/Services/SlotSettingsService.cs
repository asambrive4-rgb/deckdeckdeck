using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

internal sealed class SlotSettingsService
{
    private readonly SettingEntryStore _entryStore;

    public SlotSettingsService(SettingEntryStore entryStore)
    {
        _entryStore = entryStore;
    }

    public IEnumerable<KeyValuePair<string, string>> GetDefaultEntries()
    {
        foreach (var slotKey in SlotKeyCatalog.All)
        {
            yield return new KeyValuePair<string, string>(
                SettingsKeys.GetCategorySlotEnabledKey(slotKey),
                true.ToString());
            yield return new KeyValuePair<string, string>(
                SettingsKeys.GetSnippetSlotEnabledKey(slotKey),
                true.ToString());
        }
    }

    public Dictionary<SlotKey, bool> ReadCategorySlotStates(IReadOnlyDictionary<string, string> entries)
    {
        return SlotKeyCatalog.All.ToDictionary(
            slotKey => slotKey,
            slotKey => SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.GetCategorySlotEnabledKey(slotKey),
                true));
    }

    public Dictionary<SlotKey, bool> ReadSnippetSlotStates(IReadOnlyDictionary<string, string> entries)
    {
        return SlotKeyCatalog.All.ToDictionary(
            slotKey => slotKey,
            slotKey => SettingsValueParser.ReadBool(
                entries,
                SettingsKeys.GetSnippetSlotEnabledKey(slotKey),
                true));
    }

    public void SetCategorySlotEnabled(SlotKey slotKey, bool enabled)
    {
        _entryStore.Upsert(SettingsKeys.GetCategorySlotEnabledKey(slotKey), enabled.ToString());
    }

    public void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled)
    {
        _entryStore.Upsert(SettingsKeys.GetSnippetSlotEnabledKey(slotKey), enabled.ToString());
    }

    public IEnumerable<KeyValuePair<string, string>> ToSettingEntries(AppSettings settings)
    {
        foreach (var slotKey in SlotKeyCatalog.All)
        {
            var categoryEnabled = !settings.EnabledCategorySlotKeys.TryGetValue(slotKey, out var categoryValue)
                || categoryValue;
            var snippetEnabled = !settings.EnabledSnippetSlotKeys.TryGetValue(slotKey, out var snippetValue)
                || snippetValue;

            yield return new KeyValuePair<string, string>(
                SettingsKeys.GetCategorySlotEnabledKey(slotKey),
                categoryEnabled.ToString());
            yield return new KeyValuePair<string, string>(
                SettingsKeys.GetSnippetSlotEnabledKey(slotKey),
                snippetEnabled.ToString());
        }
    }
}
