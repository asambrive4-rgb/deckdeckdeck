using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.IO;
using Microsoft.Data.Sqlite;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class BackupGateway : IBackupGateway
{
    private const string AutomaticBackupSearchPattern = "DeckDeckDeck-auto-*.zip";
    private const string RestoreSafetyBackupKind = "restore-safety";
    private readonly BackupArchive _backupArchiveService;
    private readonly BackupRestoreFileStore _backupRestoreFileService;
    private readonly AppStoragePaths _fileStorageService;
    private readonly Func<DateTimeOffset> _getNow;
    private readonly FileLogger? _loggingService;
    private readonly SettingsRepository _settingsService;

    public BackupGateway(
        AppStoragePaths fileStorageService,
        SettingsRepository settingsService,
        FileLogger? loggingService = null,
        Func<DateTimeOffset>? getNow = null)
    {
        _fileStorageService = fileStorageService;
        _settingsService = settingsService;
        _loggingService = loggingService;
        _backupArchiveService = new BackupArchive();
        _backupRestoreFileService = new BackupRestoreFileStore(fileStorageService, loggingService);
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

    public RestoreBackupResult RestoreBackup(string backupZipPath)
    {
        var backupZipValidationError = ValidateBackupZipPath(backupZipPath, out var backupZipFullPath);
        if (backupZipValidationError is not null)
        {
            return RestoreBackupResult.Failure(backupZipValidationError);
        }

        var tempDirectory = Path.Combine(_fileStorageService.TempPath, $"restore-{Guid.NewGuid():N}");
        var extractedDirectory = Path.Combine(tempDirectory, "extracted");
        var currentSnapshotDirectory = Path.Combine(tempDirectory, "current");
        string? safetyBackupPath = null;

        try
        {
            Directory.CreateDirectory(extractedDirectory);

            var extractedBackup = _backupArchiveService.ExtractRestoreArchive(
                backupZipFullPath!,
                extractedDirectory);
            if (!extractedBackup.Succeeded)
            {
                return RestoreBackupResult.Failure(extractedBackup.ErrorMessage!);
            }

            ValidateRestoredDatabase(extractedBackup.DatabasePath!);

            var safetyBackupFolder = ResolveRestoreSafetyBackupFolder(backupZipFullPath!);
            if (safetyBackupFolder is null)
            {
                return RestoreBackupResult.Failure(
                    "복원 전 안전 백업을 저장할 폴더를 찾지 못했습니다. 백업 폴더 설정을 확인해 주세요.");
            }

            var safetyBackup = CreateBackup(
                safetyBackupFolder,
                RestoreSafetyBackupKind,
                retentionCount: null);
            if (!safetyBackup.Succeeded)
            {
                return RestoreBackupResult.Failure(
                    safetyBackup.ErrorMessage ?? "복원 전 안전 백업을 만들지 못해 복원을 중단했습니다.");
            }

            safetyBackupPath = safetyBackup.BackupPath;
            _backupRestoreFileService.ReplaceCurrentData(extractedDirectory, currentSnapshotDirectory);
            NormalizeRestoredStoredPaths();
            _settingsService.ReloadAfterExternalDataChange();

            return RestoreBackupResult.Success(safetyBackupPath!);
        }
        catch (InvalidDataException ex)
        {
            _loggingService?.Log("Restore backup validation failed.", ex);

            return RestoreBackupResult.Failure(
                "올바른 DeckDeckDeck 백업 ZIP이 아닙니다.",
                safetyBackupPath);
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Restore backup failed.", ex);

            return RestoreBackupResult.Failure(
                safetyBackupPath is null
                    ? "백업 ZIP을 복원하지 못했습니다."
                    : "백업 ZIP을 복원하지 못했습니다. 현재 데이터는 복원 전 안전 백업으로 보관되었습니다.",
                safetyBackupPath);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
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

            _backupArchiveService.CreateBackupArchive(
                backupPath,
                databaseSnapshotPath,
                _fileStorageService.ImagesPath,
                _fileStorageService.IconCachePath);

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

    private static string? ValidateBackupZipPath(string backupZipPath, out string? backupZipFullPath)
    {
        backupZipFullPath = null;
        if (string.IsNullOrWhiteSpace(backupZipPath))
        {
            return "복원할 백업 ZIP을 선택해 주세요.";
        }

        try
        {
            backupZipFullPath = Path.GetFullPath(backupZipPath);
        }
        catch
        {
            return "백업 ZIP 경로가 올바르지 않습니다.";
        }

        return File.Exists(backupZipFullPath)
            ? null
            : "선택한 백업 ZIP을 찾을 수 없습니다.";
    }

    private static void ValidateRestoredDatabase(string databasePath)
    {
        try
        {
            if (new FileInfo(databasePath).Length == 0)
            {
                throw new InvalidDataException("Restored database is empty.");
            }

            var dbContextFactory = new AppDbContextFactory(databasePath);
            dbContextFactory.EnsureCreated();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Restored database is not readable.", ex);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private string? ResolveRestoreSafetyBackupFolder(string backupZipPath)
    {
        var configuredBackupFolder = _settingsService.Load().BackupFolderPath;
        if (IsUsableRestoreSafetyFolder(configuredBackupFolder))
        {
            return configuredBackupFolder.Trim();
        }

        var selectedZipFolder = Path.GetDirectoryName(backupZipPath);
        return IsUsableRestoreSafetyFolder(selectedZipFolder)
            ? selectedZipFolder
            : null;
    }

    private bool IsUsableRestoreSafetyFolder(string? folderPath)
    {
        return !string.IsNullOrWhiteSpace(folderPath)
            && ValidateBackupFolder(folderPath) is null
            && !File.Exists(folderPath);
    }

    private void NormalizeRestoredStoredPaths()
    {
        var dbContextFactory = new AppDbContextFactory(_fileStorageService.DatabasePath);
        dbContextFactory.EnsureCreated();
        new StoredPathMigration(dbContextFactory, _fileStorageService).NormalizeManagedPaths();
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

    BackupGatewayResult IBackupGateway.CreateManualBackup(string backupFolderPath)
    {
        var result = CreateManualBackup(backupFolderPath);
        return new BackupGatewayResult(result.Succeeded, result.BackupPath, result.ErrorMessage);
    }

    RestoreBackupGatewayResult IBackupGateway.RestoreBackup(string backupZipPath)
    {
        var result = RestoreBackup(backupZipPath);
        return new RestoreBackupGatewayResult(
            result.Succeeded,
            result.SafetyBackupPath,
            result.ErrorMessage);
    }
}

