using System.IO;
using Microsoft.Data.Sqlite;

namespace DeckDeckDeck.App.Services;

internal sealed class BackupRestoreFileService
{
    private readonly FileStorageService _fileStorageService;
    private readonly LoggingService? _loggingService;

    public BackupRestoreFileService(
        FileStorageService fileStorageService,
        LoggingService? loggingService = null)
    {
        _fileStorageService = fileStorageService;
        _loggingService = loggingService;
    }

    public void ReplaceCurrentData(string extractedDirectory, string currentSnapshotDirectory)
    {
        Directory.CreateDirectory(currentSnapshotDirectory);
        SqliteConnection.ClearAllPools();

        try
        {
            MoveCurrentDataToSnapshot(currentSnapshotDirectory);
            RestoreExtractedData(extractedDirectory);
        }
        catch
        {
            TryRestoreCurrentData(currentSnapshotDirectory);
            throw;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private void MoveCurrentDataToSnapshot(string currentSnapshotDirectory)
    {
        MoveFileIfExists(
            _fileStorageService.DatabasePath,
            Path.Combine(currentSnapshotDirectory, BackupArchiveService.DatabaseEntryName));
        MoveDirectoryIfExists(
            _fileStorageService.ImagesPath,
            Path.Combine(currentSnapshotDirectory, BackupArchiveService.ImagesEntryRoot));
        MoveDirectoryIfExists(
            _fileStorageService.IconCachePath,
            Path.Combine(currentSnapshotDirectory, BackupArchiveService.IconCacheEntryRoot));
    }

    private void RestoreExtractedData(string extractedDirectory)
    {
        Directory.CreateDirectory(_fileStorageService.AppDataPath);
        File.Copy(
            Path.Combine(extractedDirectory, BackupArchiveService.DatabaseEntryName),
            _fileStorageService.DatabasePath,
            overwrite: false);

        MoveDirectoryIfExists(
            Path.Combine(extractedDirectory, BackupArchiveService.ImagesEntryRoot),
            _fileStorageService.ImagesPath);
        MoveDirectoryIfExists(
            Path.Combine(extractedDirectory, BackupArchiveService.IconCacheEntryRoot),
            _fileStorageService.IconCachePath);

        _fileStorageService.EnsureCreated();
    }

    private void TryRestoreCurrentData(string currentSnapshotDirectory)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            DeleteFileIfExists(_fileStorageService.DatabasePath);
            DeleteDirectoryIfExists(_fileStorageService.ImagesPath);
            DeleteDirectoryIfExists(_fileStorageService.IconCachePath);

            MoveFileIfExists(
                Path.Combine(currentSnapshotDirectory, BackupArchiveService.DatabaseEntryName),
                _fileStorageService.DatabasePath);
            MoveDirectoryIfExists(
                Path.Combine(currentSnapshotDirectory, BackupArchiveService.ImagesEntryRoot),
                _fileStorageService.ImagesPath);
            MoveDirectoryIfExists(
                Path.Combine(currentSnapshotDirectory, BackupArchiveService.IconCacheEntryRoot),
                _fileStorageService.IconCachePath);
            _fileStorageService.EnsureCreated();
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Restore rollback failed.", ex);
        }
    }

    private static void MoveFileIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Move(sourcePath, destinationPath);
    }

    private static void MoveDirectoryIfExists(string sourcePath, string destinationPath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        Directory.Move(sourcePath, destinationPath);
    }

    private static void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
