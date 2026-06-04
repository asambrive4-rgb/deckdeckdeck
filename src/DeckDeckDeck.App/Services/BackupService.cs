using System.IO;
using System.IO.Compression;
using DeckDeckDeck.App.Models;
using Microsoft.Data.Sqlite;

namespace DeckDeckDeck.App.Services;

public sealed class BackupService
{
    private const string AutomaticBackupSearchPattern = "DeckDeckDeck-auto-*.zip";
    private readonly FileStorageService _fileStorageService;
    private readonly Func<DateTimeOffset> _getNow;
    private readonly LoggingService? _loggingService;
    private readonly SettingsService _settingsService;

    public BackupService(
        FileStorageService fileStorageService,
        SettingsService settingsService,
        LoggingService? loggingService = null,
        Func<DateTimeOffset>? getNow = null)
    {
        _fileStorageService = fileStorageService;
        _settingsService = settingsService;
        _loggingService = loggingService;
        _getNow = getNow ?? (() => DateTimeOffset.UtcNow);
    }

    public BackupResult CreateAutomaticBackup(AppSettings settings)
    {
        if (!settings.AutoBackupEnabled || string.IsNullOrWhiteSpace(settings.BackupFolderPath))
        {
            return BackupResult.Skip();
        }

        return CreateBackup(
            settings.BackupFolderPath,
            "auto",
            Math.Max(1, settings.AutoBackupRetentionCount));
    }

    public BackupResult CreateManualBackup(string backupFolderPath)
    {
        return CreateBackup(backupFolderPath, "manual", retentionCount: null);
    }

    public string? ValidateBackupFolder(string? backupFolderPath)
    {
        if (string.IsNullOrWhiteSpace(backupFolderPath))
        {
            return "백업 폴더를 선택해 주세요.";
        }

        string backupFolderFullPath;
        string appDataFullPath;

        try
        {
            backupFolderFullPath = Path.GetFullPath(backupFolderPath);
            appDataFullPath = Path.GetFullPath(_fileStorageService.AppDataPath);
        }
        catch (Exception)
        {
            return "백업 폴더 경로가 올바르지 않습니다.";
        }

        return IsSameOrChildDirectory(backupFolderFullPath, appDataFullPath)
            ? "앱 데이터 폴더 안에는 백업 폴더를 둘 수 없습니다."
            : null;
    }

    private BackupResult CreateBackup(string backupFolderPath, string backupKind, int? retentionCount)
    {
        var validationError = ValidateBackupFolder(backupFolderPath);
        if (validationError is not null)
        {
            _loggingService?.Log($"Backup folder validation failed. {validationError}");

            return BackupResult.Failure(validationError);
        }

        var tempDirectory = Path.Combine(_fileStorageService.TempPath, $"backup-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(backupFolderPath);
            Directory.CreateDirectory(tempDirectory);

            var now = _getNow();
            var backupPath = GetBackupPath(backupFolderPath, backupKind, now);
            var databaseSnapshotPath = CreateDatabaseSnapshot(tempDirectory);

            using (var zipStream = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(databaseSnapshotPath, "launcher.db", CompressionLevel.Optimal);
                AddDirectoryToArchive(archive, _fileStorageService.ImagesPath, "images");
                AddDirectoryToArchive(archive, _fileStorageService.IconCachePath, "icon-cache");
            }

            retentionCount ??= 0;
            if (retentionCount > 0)
            {
                PruneAutomaticBackups(backupFolderPath, retentionCount.Value);
            }

            SaveLastBackupCreatedAt(now);

            return BackupResult.Success(backupPath);
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Backup failed.", ex);

            return BackupResult.Failure("백업을 만들지 못했습니다.");
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    private string CreateDatabaseSnapshot(string tempDirectory)
    {
        var snapshotPath = Path.Combine(tempDirectory, "launcher.db");
        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _fileStorageService.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = snapshotPath,
            Pooling = false
        };

        using var sourceConnection = new SqliteConnection(sourceBuilder.ToString());
        using var destinationConnection = new SqliteConnection(destinationBuilder.ToString());
        sourceConnection.Open();
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection);
        destinationConnection.Close();
        sourceConnection.Close();
        SqliteConnection.ClearAllPools();

        return snapshotPath;
    }

    private static void AddDirectoryToArchive(ZipArchive archive, string directoryPath, string entryRoot)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(directoryPath, filePath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            var entryName = $"{entryRoot}/{relativePath}";
            archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
        }
    }

    private static string GetBackupPath(string backupFolderPath, string backupKind, DateTimeOffset createdAt)
    {
        var timestamp = createdAt.ToLocalTime().ToString("yyyyMMdd-HHmmss-fff");
        var backupPath = Path.Combine(backupFolderPath, $"DeckDeckDeck-{backupKind}-{timestamp}.zip");
        var suffix = 2;

        while (File.Exists(backupPath))
        {
            backupPath = Path.Combine(backupFolderPath, $"DeckDeckDeck-{backupKind}-{timestamp}-{suffix}.zip");
            suffix++;
        }

        return backupPath;
    }

    private static void PruneAutomaticBackups(string backupFolderPath, int retentionCount)
    {
        var backupsToDelete = Directory
            .EnumerateFiles(backupFolderPath, AutomaticBackupSearchPattern)
            .OrderByDescending(Path.GetFileName)
            .Skip(retentionCount)
            .ToList();

        foreach (var backupPath in backupsToDelete)
        {
            File.Delete(backupPath);
        }
    }

    private void SaveLastBackupCreatedAt(DateTimeOffset createdAt)
    {
        try
        {
            _settingsService.SetLastBackupCreatedAt(createdAt);
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Last backup timestamp save failed.", ex);
        }
    }

    private static bool IsSameOrChildDirectory(string childPath, string parentPath)
    {
        var normalizedChild = TrimDirectorySeparators(childPath);
        var normalizedParent = TrimDirectorySeparators(parentPath);

        return string.Equals(normalizedChild, normalizedParent, StringComparison.OrdinalIgnoreCase)
            || normalizedChild.StartsWith(
                normalizedParent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimDirectorySeparators(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void DeleteTempDirectory(string tempDirectory)
    {
        try
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Temporary backup files should not interrupt the app flow.
        }
    }
}
