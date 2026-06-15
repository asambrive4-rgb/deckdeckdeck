using System.IO.Compression;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class BackupGatewayTests
{
    [Fact]
    public void AutomaticBackupCreatesZipWithAppDataAndExcludesLogsAndTemp()
    {
        var services = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        File.WriteAllText(Path.Combine(services.Storage.ImageOriginalsPath, "original.txt"), "original");
        File.WriteAllText(Path.Combine(services.Storage.ImageThumbnailsPath, "thumbnail.txt"), "thumbnail");
        File.WriteAllText(Path.Combine(services.Storage.IconCachePath, "icon.txt"), "icon");
        File.WriteAllText(Path.Combine(services.Storage.LogsPath, "app.log"), "log");
        File.WriteAllText(Path.Combine(services.Storage.TempPath, "scratch.txt"), "scratch");

        var result = services.BackupGateway.CreateAutomaticBackup(new AppSettings
        {
            AutoBackupEnabled = true,
            BackupFolderPath = backupFolder,
            AutoBackupRetentionCount = 10
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        using var archive = ZipFile.OpenRead(result.BackupPath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet();
        Assert.Contains("launcher.db", entries);
        Assert.Contains("images/originals/original.txt", entries);
        Assert.Contains("images/thumbnails/thumbnail.txt", entries);
        Assert.Contains("icon-cache/icon.txt", entries);
        Assert.DoesNotContain("logs/app.log", entries);
        Assert.DoesNotContain("temp/scratch.txt", entries);
        Assert.StartsWith("DeckDeckDeck-auto-", Path.GetFileName(result.BackupPath));
        Assert.NotNull(services.SettingsRepository.Load().LastBackupCreatedAt);
    }

    [Fact]
    public void BackupFolderInsideAppDataIsRejected()
    {
        var services = CreateServices();
        var backupFolder = Path.Combine(services.Storage.AppDataPath, "backups");

        var result = services.BackupGateway.CreateManualBackup(backupFolder);

        Assert.False(result.Succeeded);
        Assert.False(result.Skipped);
        Assert.False(Directory.Exists(backupFolder));
    }

    [Fact]
    public void AutomaticBackupKeepsRecentAutomaticBackupsAndLeavesManualBackups()
    {
        var services = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        for (var index = 0; index < 11; index++)
        {
            File.WriteAllText(
                Path.Combine(backupFolder, $"DeckDeckDeck-auto-20200101-000000-{index:000}.zip"),
                "old");
        }

        var manualBackupPath = Path.Combine(backupFolder, "DeckDeckDeck-manual-19990101-000000-000.zip");
        File.WriteAllText(manualBackupPath, "manual");

        var result = services.BackupGateway.CreateAutomaticBackup(new AppSettings
        {
            AutoBackupEnabled = true,
            BackupFolderPath = backupFolder,
            AutoBackupRetentionCount = 10
        });

        Assert.True(result.Succeeded);
        Assert.Equal(10, Directory.EnumerateFiles(backupFolder, "DeckDeckDeck-auto-*.zip").Count());
        Assert.True(File.Exists(manualBackupPath));
    }

    [Fact]
    public void BackupFailureReturnsFailureAndWritesLog()
    {
        var services = CreateServices();
        var backupFolder = Path.Combine(Path.GetTempPath(), $"deckdeckdeck-backups-{Guid.NewGuid():N}");
        File.WriteAllText(backupFolder, "not a directory");

        var result = services.BackupGateway.CreateManualBackup(backupFolder);

        Assert.False(result.Succeeded);
        Assert.False(result.Skipped);
        Assert.True(File.Exists(Path.Combine(services.Storage.LogsPath, "app.log")));
        Assert.Contains("Backup failed.", File.ReadAllText(Path.Combine(services.Storage.LogsPath, "app.log")));
    }

    [Fact]
    public void AutomaticBackupSkipsWhenDisabledOrFolderMissing()
    {
        var services = CreateServices();

        var disabledResult = services.BackupGateway.CreateAutomaticBackup(new AppSettings
        {
            AutoBackupEnabled = false,
            BackupFolderPath = CreateTempBackupFolder()
        });
        var missingFolderResult = services.BackupGateway.CreateAutomaticBackup(new AppSettings
        {
            AutoBackupEnabled = true,
            BackupFolderPath = string.Empty
        });

        Assert.True(disabledResult.Skipped);
        Assert.True(missingFolderResult.Skipped);
    }

    [Fact]
    public void RestoreBackupReplacesDatabaseImagesAndIconCacheAndCreatesSafetyBackup()
    {
        var backupSource = CreateServices(Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            AppStoragePaths.AppFolderName));
        var backupFolder = CreateTempBackupFolder();
        File.WriteAllText(Path.Combine(backupSource.Storage.ImageOriginalsPath, "restored.txt"), "restored image");
        File.WriteAllText(Path.Combine(backupSource.Storage.ImageThumbnailsPath, "restored-thumb.txt"), "restored thumbnail");
        File.WriteAllText(Path.Combine(backupSource.Storage.IconCachePath, "restored.txt"), "restored icon");
        var restoredCategory = backupSource.CategoryRepository.Create(
            SlotKey.Numpad4,
            "Restored",
            null,
            Path.Combine(backupSource.Storage.ImageOriginalsPath, "restored.txt"),
            Path.Combine(backupSource.Storage.ImageThumbnailsPath, "restored-thumb.txt"));
        backupSource.SnippetRepository.Create(
            restoredCategory.Id,
            SlotKey.Numpad3,
            "Restored Snippet",
            "from backup",
            null,
            actionType: SnippetActionType.LaunchFile,
            launchPath: @"C:\tools\restored.exe",
            autoIcon: new AutoIconCacheEntry(
                Path.Combine(backupSource.Storage.IconCachePath, "restored.txt"),
                @"C:\tools\restored.exe",
                DateTime.UtcNow,
                123));
        backupSource.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad4, false);
        var backup = backupSource.BackupGateway.CreateManualBackup(backupFolder);
        Assert.True(backup.Succeeded);

        var current = CreateServices();
        var safetyBackupFolder = CreateTempBackupFolder();
        var currentSettings = current.SettingsRepository.Load();
        currentSettings.BackupFolderPath = safetyBackupFolder;
        current.SettingsRepository.Save(currentSettings);
        current.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);
        File.WriteAllText(Path.Combine(current.Storage.ImageOriginalsPath, "current.txt"), "current image");
        File.WriteAllText(Path.Combine(current.Storage.IconCachePath, "current.txt"), "current icon");

        var result = current.BackupGateway.RestoreBackup(backup.BackupPath!);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.SafetyBackupPath);
        Assert.True(File.Exists(result.SafetyBackupPath));
        Assert.StartsWith("DeckDeckDeck-restore-safety-", Path.GetFileName(result.SafetyBackupPath));
        var restored = current.CategoryRepository.GetBySlotKey(SlotKey.Numpad4)!;
        var restoredSnippet = Assert.Single(current.SnippetRepository.GetByCategoryId(restored.Id));
        Assert.Equal("Restored", restored.Name);
        Assert.Equal("images/originals/restored.txt", restored.ImagePath);
        Assert.Equal("images/thumbnails/restored-thumb.txt", restored.ThumbnailPath);
        Assert.Equal("icon-cache/restored.txt", restoredSnippet.AutoIconPath);
        Assert.Null(current.CategoryRepository.GetBySlotKey(SlotKey.Numpad5));
        Assert.False(current.SettingsRepository.Load().EnabledCategorySlotKeys[SlotKey.Numpad4]);
        Assert.Equal("restored image", File.ReadAllText(Path.Combine(current.Storage.ImageOriginalsPath, "restored.txt")));
        Assert.Equal("restored icon", File.ReadAllText(Path.Combine(current.Storage.IconCachePath, "restored.txt")));
        Assert.False(File.Exists(Path.Combine(current.Storage.ImageOriginalsPath, "current.txt")));
        Assert.False(File.Exists(Path.Combine(current.Storage.IconCachePath, "current.txt")));

        using var safetyArchive = ZipFile.OpenRead(result.SafetyBackupPath);
        var safetyEntries = safetyArchive.Entries.Select(entry => entry.FullName).ToHashSet();
        Assert.Contains("launcher.db", safetyEntries);
        Assert.Contains("images/originals/current.txt", safetyEntries);
        Assert.Contains("icon-cache/current.txt", safetyEntries);
    }

    [Fact]
    public void RestoreBackupRejectsZipWithoutDatabaseAndKeepsCurrentData()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);
        var invalidZip = Path.Combine(CreateTempBackupFolder(), "invalid.zip");
        using (var archive = ZipFile.Open(invalidZip, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("images/originals/orphan.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("orphan");
        }

        var result = services.BackupGateway.RestoreBackup(invalidZip);

        Assert.False(result.Succeeded);
        Assert.Null(result.SafetyBackupPath);
        Assert.Equal(category.Name, services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void RestoreBackupRejectsUnreadableZipAndKeepsCurrentData()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);
        var invalidZip = Path.Combine(CreateTempBackupFolder(), "broken.zip");
        File.WriteAllText(invalidZip, "not a zip");

        var result = services.BackupGateway.RestoreBackup(invalidZip);

        Assert.False(result.Succeeded);
        Assert.Null(result.SafetyBackupPath);
        Assert.Equal(category.Name, services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void RestoreBackupRejectsUnsafeArchivePathAndKeepsCurrentData()
    {
        var backupSource = CreateServices();
        var backup = backupSource.BackupGateway.CreateManualBackup(CreateTempBackupFolder());
        Assert.True(backup.Succeeded);
        using (var archive = ZipFile.Open(backup.BackupPath!, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry("../escaped.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("escaped");
        }

        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);

        var result = services.BackupGateway.RestoreBackup(backup.BackupPath!);

        Assert.False(result.Succeeded);
        Assert.Null(result.SafetyBackupPath);
        Assert.Equal(category.Name, services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Name);
        Assert.False(File.Exists(Path.Combine(services.Storage.AppDataPath, "escaped.txt")));
    }

    [Fact]
    public void RestoreBackupStopsWhenSafetyBackupCannotBeCreated()
    {
        var backupSource = CreateServices();
        var restoredCategory = backupSource.CategoryRepository.Create(SlotKey.Numpad4, "Restored", null);
        var backup = backupSource.BackupGateway.CreateManualBackup(CreateTempBackupFolder());
        Assert.True(backup.Succeeded);

        var current = CreateServices();
        var currentCategory = current.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);
        var settings = current.SettingsRepository.Load();
        settings.BackupFolderPath = Path.Combine(current.Storage.AppDataPath, "blocked-safety-backups");
        current.SettingsRepository.Save(settings);
        var backupInsideAppData = Path.Combine(current.Storage.AppDataPath, "restore.zip");
        File.Copy(backup.BackupPath!, backupInsideAppData);

        var result = current.BackupGateway.RestoreBackup(backupInsideAppData);

        Assert.False(result.Succeeded);
        Assert.Null(result.SafetyBackupPath);
        Assert.Equal(currentCategory.Name, current.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Name);
        Assert.Null(current.CategoryRepository.GetBySlotKey(restoredCategory.SlotKey));
    }

    private static string CreateTempBackupFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deckdeckdeck-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }
}
