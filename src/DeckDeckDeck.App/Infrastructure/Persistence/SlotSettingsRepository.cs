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

internal sealed class SlotSettingsRepository
{
    private readonly SettingEntryRepository _entryStore;

    public SlotSettingsRepository(SettingEntryRepository entryStore)
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
