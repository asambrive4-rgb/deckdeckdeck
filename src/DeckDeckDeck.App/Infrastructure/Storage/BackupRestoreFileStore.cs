using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.IO;
using Microsoft.Data.Sqlite;

namespace DeckDeckDeck.App.Infrastructure.Storage;

internal sealed class BackupRestoreFileStore
{
    private readonly AppStoragePaths _fileStorageService;
    private readonly FileLogger? _loggingService;

    public BackupRestoreFileStore(
        AppStoragePaths fileStorageService,
        FileLogger? loggingService = null)
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
            Path.Combine(currentSnapshotDirectory, BackupArchive.DatabaseEntryName));
        MoveDirectoryIfExists(
            _fileStorageService.ImagesPath,
            Path.Combine(currentSnapshotDirectory, BackupArchive.ImagesEntryRoot));
        MoveDirectoryIfExists(
            _fileStorageService.IconCachePath,
            Path.Combine(currentSnapshotDirectory, BackupArchive.IconCacheEntryRoot));
    }

    private void RestoreExtractedData(string extractedDirectory)
    {
        Directory.CreateDirectory(_fileStorageService.AppDataPath);
        File.Copy(
            Path.Combine(extractedDirectory, BackupArchive.DatabaseEntryName),
            _fileStorageService.DatabasePath,
            overwrite: false);

        MoveDirectoryIfExists(
            Path.Combine(extractedDirectory, BackupArchive.ImagesEntryRoot),
            _fileStorageService.ImagesPath);
        MoveDirectoryIfExists(
            Path.Combine(extractedDirectory, BackupArchive.IconCacheEntryRoot),
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
                Path.Combine(currentSnapshotDirectory, BackupArchive.DatabaseEntryName),
                _fileStorageService.DatabasePath);
            MoveDirectoryIfExists(
                Path.Combine(currentSnapshotDirectory, BackupArchive.ImagesEntryRoot),
                _fileStorageService.ImagesPath);
            MoveDirectoryIfExists(
                Path.Combine(currentSnapshotDirectory, BackupArchive.IconCacheEntryRoot),
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
