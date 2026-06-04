using System.IO.Compression;
using DeckDeckDeck.App.Models;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class BackupServiceTests
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

        var result = services.BackupService.CreateAutomaticBackup(new AppSettings
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
        Assert.NotNull(services.SettingsService.Load().LastBackupCreatedAt);
    }

    [Fact]
    public void BackupFolderInsideAppDataIsRejected()
    {
        var services = CreateServices();
        var backupFolder = Path.Combine(services.Storage.AppDataPath, "backups");

        var result = services.BackupService.CreateManualBackup(backupFolder);

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

        var result = services.BackupService.CreateAutomaticBackup(new AppSettings
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

        var result = services.BackupService.CreateManualBackup(backupFolder);

        Assert.False(result.Succeeded);
        Assert.False(result.Skipped);
        Assert.True(File.Exists(Path.Combine(services.Storage.LogsPath, "app.log")));
        Assert.Contains("Backup failed.", File.ReadAllText(Path.Combine(services.Storage.LogsPath, "app.log")));
    }

    [Fact]
    public void AutomaticBackupSkipsWhenDisabledOrFolderMissing()
    {
        var services = CreateServices();

        var disabledResult = services.BackupService.CreateAutomaticBackup(new AppSettings
        {
            AutoBackupEnabled = false,
            BackupFolderPath = CreateTempBackupFolder()
        });
        var missingFolderResult = services.BackupService.CreateAutomaticBackup(new AppSettings
        {
            AutoBackupEnabled = true,
            BackupFolderPath = string.Empty
        });

        Assert.True(disabledResult.Skipped);
        Assert.True(missingFolderResult.Skipped);
    }

    private static string CreateTempBackupFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deckdeckdeck-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }
}
