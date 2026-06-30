using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class SettingsRepositoryTests
{
    [Fact]
    public void SettingsDefaultsAreCreated()
    {
        var services = CreateServices();

        var settings = services.SettingsRepository.Load();

        Assert.True(settings.BringWindowToFrontOnHotkey);
        Assert.True(settings.AutoHideAfterPaste);
        Assert.True(settings.RestoreClipboardAfterPaste);
        Assert.False(settings.AutoBackupEnabled);
        Assert.Equal(string.Empty, settings.BackupFolderPath);
        Assert.Equal(10, settings.AutoBackupRetentionCount);
        Assert.Null(settings.LastBackupCreatedAt);
        Assert.Equal("Ctrl + Numpad1~9, /, *, -, +, .", settings.DirectCategoryHotkeys);
        Assert.Null(settings.LastWindowLeft);
        Assert.Null(settings.LastWindowTop);
        Assert.Null(settings.LastWindowScreenDeviceName);
        Assert.All(SlotKeyCatalog.All, slotKey => Assert.True(settings.EnabledCategorySlotKeys[slotKey]));
        Assert.All(SlotKeyCatalog.All, slotKey => Assert.True(settings.EnabledSnippetSlotKeys[slotKey]));
    }

    [Fact]
    public void EnsureDefaultsAddsMissingDefaultWithoutOverwritingExistingSettings()
    {
        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        settings.AutoHideAfterPaste = false;
        services.SettingsRepository.Save(settings);
        DeleteSettingValue(services.Storage.DatabasePath, SettingsKeys.BringWindowToFrontOnHotkey);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();

        Assert.True(reloaded.BringWindowToFrontOnHotkey);
        Assert.False(reloaded.AutoHideAfterPaste);
    }

    [Fact]
    public void CategoryAndSnippetSlotEnabledSettingsAreIndependent()
    {
        var services = CreateServices();

        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad2, false);
        services.SettingsRepository.SetSnippetSlotEnabled(SlotKey.Numpad4, false);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();
        Assert.False(reloaded.EnabledCategorySlotKeys[SlotKey.Numpad2]);
        Assert.True(reloaded.EnabledSnippetSlotKeys[SlotKey.Numpad2]);
        Assert.True(reloaded.EnabledCategorySlotKeys[SlotKey.Numpad4]);
        Assert.False(reloaded.EnabledSnippetSlotKeys[SlotKey.Numpad4]);
    }

    [Fact]
    public void SettingsSavePersistsAutoHideAndClipboardRestore()
    {
        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        settings.BringWindowToFrontOnHotkey = false;
        settings.AutoHideAfterPaste = false;
        settings.RestoreClipboardAfterPaste = false;

        services.SettingsRepository.Save(settings);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();
        Assert.False(reloaded.BringWindowToFrontOnHotkey);
        Assert.False(reloaded.AutoHideAfterPaste);
        Assert.False(reloaded.RestoreClipboardAfterPaste);
    }

    [Fact]
    public void SettingsSavePersistsLastWindowPlacement()
    {
        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        settings.LastWindowLeft = 120.5;
        settings.LastWindowTop = 240.25;
        settings.LastWindowScreenDeviceName = "Monitor2";

        services.SettingsRepository.Save(settings);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();
        Assert.Equal(120.5, reloaded.LastWindowLeft);
        Assert.Equal(240.25, reloaded.LastWindowTop);
        Assert.Equal("Monitor2", reloaded.LastWindowScreenDeviceName);
    }

    [Fact]
    public void LoadReturnsCopyIsolatedFromCachedSettings()
    {
        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        settings.AutoHideAfterPaste = false;
        settings.EnabledCategorySlotKeys[SlotKey.Numpad1] = false;

        var reloaded = services.SettingsRepository.Load();

        Assert.True(reloaded.AutoHideAfterPaste);
        Assert.True(reloaded.EnabledCategorySlotKeys[SlotKey.Numpad1]);
    }

    [Fact]
    public void SaveWindowPlacementPreservesOtherSettings()
    {
        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        settings.AutoHideAfterPaste = false;
        services.SettingsRepository.Save(settings);

        services.SettingsRepository.SaveWindowPlacement(10, 20, "Monitor1");

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();
        Assert.False(reloaded.AutoHideAfterPaste);
        Assert.Equal(10, reloaded.LastWindowLeft);
        Assert.Equal(20, reloaded.LastWindowTop);
        Assert.Equal("Monitor1", reloaded.LastWindowScreenDeviceName);
    }

    [Fact]
    public void SettingsSavePersistsBackupSettings()
    {
        var services = CreateServices();
        var createdAt = DateTimeOffset.Parse("2026-06-04T12:30:00+09:00");
        var settings = services.SettingsRepository.Load();
        settings.AutoBackupEnabled = true;
        settings.BackupFolderPath = @"C:\tmp\deckdeckdeck-backups";
        settings.AutoBackupRetentionCount = 7;
        settings.LastBackupCreatedAt = createdAt;

        services.SettingsRepository.Save(settings);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();
        Assert.True(reloaded.AutoBackupEnabled);
        Assert.Equal(@"C:\tmp\deckdeckdeck-backups", reloaded.BackupFolderPath);
        Assert.Equal(7, reloaded.AutoBackupRetentionCount);
        Assert.Equal(createdAt, reloaded.LastBackupCreatedAt);
    }

    [Fact]
    public void SettingsSavePersistsSpotifySettingsWithProtectedTokens()
    {
        var services = CreateServices();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var settings = services.SettingsRepository.Load();
        settings.SpotifyClientId = "client-id";
        settings.SpotifyAccessToken = "access-token";
        settings.SpotifyRefreshToken = "refresh-token";
        settings.SpotifyTokenExpiresAt = expiresAt;
        settings.SpotifyConnectedUserDisplayName = "Spotify 계정";

        services.SettingsRepository.Save(settings);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();
        Assert.Equal("client-id", reloaded.SpotifyClientId);
        Assert.Equal("access-token", reloaded.SpotifyAccessToken);
        Assert.Equal("refresh-token", reloaded.SpotifyRefreshToken);
        Assert.Equal(expiresAt, reloaded.SpotifyTokenExpiresAt);
        Assert.Equal("Spotify 계정", reloaded.SpotifyConnectedUserDisplayName);
        Assert.NotEqual("access-token", ReadSettingValue(services.Storage.DatabasePath, "spotifyAccessToken"));
        Assert.NotEqual("refresh-token", ReadSettingValue(services.Storage.DatabasePath, "spotifyRefreshToken"));
    }

    [Fact]
    public void SetLastBackupCreatedAtUpdatesOnlyBackupTimestamp()
    {
        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        settings.AutoBackupEnabled = true;
        settings.BackupFolderPath = @"C:\tmp\deckdeckdeck-backups";
        services.SettingsRepository.Save(settings);
        var createdAt = DateTimeOffset.Parse("2026-06-04T12:30:00+00:00");

        services.SettingsRepository.SetLastBackupCreatedAt(createdAt);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsRepository.Load();
        Assert.True(reloaded.AutoBackupEnabled);
        Assert.Equal(@"C:\tmp\deckdeckdeck-backups", reloaded.BackupFolderPath);
        Assert.Equal(createdAt, reloaded.LastBackupCreatedAt);
    }

    private static string ReadSettingValue(string databasePath, string key)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);

        return Assert.IsType<string>(command.ExecuteScalar());
    }

    private static void DeleteSettingValue(string databasePath, string key)
    {
        SqliteConnection.ClearAllPools();
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Settings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);
        command.ExecuteNonQuery();
    }
}
