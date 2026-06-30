using System.Globalization;
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

public sealed class StartupMaintenanceStateRepository : IStartupMaintenanceStateRepository
{
    private const string KeyPrefix = "startupMaintenance.";

    private readonly SettingEntryRepository _entryRepository;

    public StartupMaintenanceStateRepository(AppDbContextFactory dbContextFactory)
    {
        _entryRepository = new SettingEntryRepository(dbContextFactory);
    }

    public int GetCompletedVersion(string maintenanceKey)
    {
        var entries = _entryRepository.LoadAll();
        var key = GetSettingKey(maintenanceKey);

        return entries.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
                ? version
                : 0;
    }

    public void SetCompletedVersion(string maintenanceKey, int version)
    {
        _entryRepository.Upsert(
            GetSettingKey(maintenanceKey),
            version.ToString(CultureInfo.InvariantCulture));
    }

    private static string GetSettingKey(string maintenanceKey)
    {
        return $"{KeyPrefix}{maintenanceKey}";
    }
}
