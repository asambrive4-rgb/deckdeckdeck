// 역할: 프로그램의 개별 설정 키와 값을 데이터베이스에 저장하고 조회합니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;

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
        var existingKeys = dbContext.Settings
            .Select(setting => setting.Key)
            .ToHashSet(StringComparer.Ordinal);
        var added = false;

        foreach (var setting in defaults)
        {
            if (existingKeys.Contains(setting.Key))
            {
                continue;
            }

            dbContext.Settings.Add(new SettingEntry { Key = setting.Key, Value = setting.Value });
            existingKeys.Add(setting.Key);
            added = true;
        }

        if (added)
        {
            dbContext.SaveChanges();
        }
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
        var existingSettings = dbContext.Settings.ToDictionary(setting => setting.Key);

        foreach (var value in values)
        {
            if (existingSettings.TryGetValue(value.Key, out var setting))
            {
                setting.Value = value.Value;
            }
            else
            {
                var newSetting = new SettingEntry { Key = value.Key, Value = value.Value };
                dbContext.Settings.Add(newSetting);
                existingSettings[value.Key] = newSetting;
            }
        }

        dbContext.SaveChanges();
    }

    private static void Upsert(AppDbContext dbContext, string key, string value)
    {
        if (dbContext.Settings.FirstOrDefault(item => item.Key == key) is { } setting)
        {
            setting.Value = value;
            return;
        }

        dbContext.Settings.Add(new SettingEntry { Key = key, Value = value });
    }
}
