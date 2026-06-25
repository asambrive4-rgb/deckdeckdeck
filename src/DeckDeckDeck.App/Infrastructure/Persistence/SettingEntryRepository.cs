using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Persistence;

internal sealed class SettingEntryRepository
{
    private readonly AppDbContextFactory _dbContextFactory;

    public SettingEntryRepository(AppDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyDictionary<string, string> LoadAll()
    {
        using var dbContext = _dbContextFactory.Create();

        return dbContext.Settings.ToDictionary(setting => setting.Key, setting => setting.Value);
    }

    public void EnsureDefaults(IEnumerable<KeyValuePair<string, string>> defaults)
    {
        using var dbContext = _dbContextFactory.Create();

        foreach (var setting in defaults)
        {
            AddIfMissing(dbContext, setting.Key, setting.Value);
        }

        dbContext.SaveChanges();
    }

    public void Upsert(string key, string value)
    {
        using var dbContext = _dbContextFactory.Create();
        Upsert(dbContext, key, value);
        dbContext.SaveChanges();
    }

    public void UpsertMany(IEnumerable<KeyValuePair<string, string>> values)
    {
        using var dbContext = _dbContextFactory.Create();

        foreach (var value in values)
        {
            Upsert(dbContext, value.Key, value.Value);
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
}
