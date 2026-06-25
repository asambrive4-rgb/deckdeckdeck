using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.IO;
using System.IO.Compression;

namespace DeckDeckDeck.App.Infrastructure.Storage;

internal sealed class BackupArchive
{
    public const string DatabaseEntryName = "launcher.db";
    public const string IconCacheEntryRoot = "icon-cache";
    public const string ImagesEntryRoot = "images";

    public void CreateBackupArchive(
        string backupPath,
        string databaseSnapshotPath,
        string imagesPath,
        string iconCachePath)
    {
        using var zipStream = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(databaseSnapshotPath, DatabaseEntryName, CompressionLevel.Optimal);
        AddDirectoryToArchive(archive, imagesPath, ImagesEntryRoot);
        AddDirectoryToArchive(archive, iconCachePath, IconCacheEntryRoot);
    }

    public RestoreArchiveResult ExtractRestoreArchive(string backupZipPath, string extractedDirectory)
    {
        var databasePath = Path.Combine(extractedDirectory, DatabaseEntryName);
        var hasDatabase = false;

        try
        {
            using var archive = ZipFile.OpenRead(backupZipPath);
            foreach (var entry in archive.Entries)
            {
                var normalizedEntryName = NormalizeArchiveEntryName(entry.FullName);
                if (normalizedEntryName is null)
                {
                    return RestoreArchiveResult.Failure("백업 ZIP 안에 안전하지 않은 파일 경로가 있습니다.");
                }

                if (normalizedEntryName.Length == 0)
                {
                    continue;
                }

                if (IsArchiveDirectory(entry))
                {
                    CreateRestorableDirectoryIfNeeded(normalizedEntryName, extractedDirectory);
                    continue;
                }

                if (string.Equals(normalizedEntryName, DatabaseEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    ExtractEntryToFile(entry, databasePath, extractedDirectory);
                    hasDatabase = true;
                    continue;
                }

                if (IsRestorableArchiveFile(normalizedEntryName))
                {
                    var destinationPath = Path.Combine(
                        extractedDirectory,
                        normalizedEntryName.Replace('/', Path.DirectorySeparatorChar));
                    ExtractEntryToFile(entry, destinationPath, extractedDirectory);
                }
            }
        }
        catch (InvalidDataException)
        {
            return RestoreArchiveResult.Failure("백업 ZIP을 열 수 없습니다.");
        }

        return hasDatabase
            ? RestoreArchiveResult.Success(databasePath)
            : RestoreArchiveResult.Failure("백업 ZIP에 launcher.db가 없습니다.");
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

    private static void ExtractEntryToFile(
        ZipArchiveEntry entry,
        string destinationPath,
        string extractedDirectory)
    {
        var destinationFullPath = Path.GetFullPath(destinationPath);
        EnsurePathInsideDirectory(destinationFullPath, extractedDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFullPath)!);
        entry.ExtractToFile(destinationFullPath, overwrite: false);
    }

    private static void CreateRestorableDirectoryIfNeeded(string normalizedEntryName, string extractedDirectory)
    {
        if (!IsRestorableArchiveDirectory(normalizedEntryName))
        {
            return;
        }

        var destinationPath = Path.Combine(
            extractedDirectory,
            normalizedEntryName.Replace('/', Path.DirectorySeparatorChar));
        var destinationFullPath = Path.GetFullPath(destinationPath);
        EnsurePathInsideDirectory(destinationFullPath, extractedDirectory);
        Directory.CreateDirectory(destinationFullPath);
    }

    private static string? NormalizeArchiveEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(entryName) || entryName.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            return null;
        }

        return string.Join('/', segments);
    }

    private static bool IsArchiveDirectory(ZipArchiveEntry entry)
    {
        return entry.FullName.EndsWith("/", StringComparison.Ordinal)
            || entry.FullName.EndsWith("\\", StringComparison.Ordinal)
            || string.IsNullOrEmpty(entry.Name);
    }

    private static bool IsRestorableArchiveDirectory(string normalizedEntryName)
    {
        return string.Equals(normalizedEntryName, ImagesEntryRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedEntryName, IconCacheEntryRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedEntryName.StartsWith(ImagesEntryRoot + '/', StringComparison.OrdinalIgnoreCase)
            || normalizedEntryName.StartsWith(IconCacheEntryRoot + '/', StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRestorableArchiveFile(string normalizedEntryName)
    {
        return normalizedEntryName.StartsWith(ImagesEntryRoot + '/', StringComparison.OrdinalIgnoreCase)
            || normalizedEntryName.StartsWith(IconCacheEntryRoot + '/', StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsurePathInsideDirectory(string fullPath, string directory)
    {
        var fullDirectory = Path.GetFullPath(directory);
        if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            fullDirectory += Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Archive entry escaped the restore directory.");
        }
    }
}

internal sealed record RestoreArchiveResult(
    bool Succeeded,
    string? DatabasePath,
    string? ErrorMessage)
{
    public static RestoreArchiveResult Success(string databasePath)
    {
        return new RestoreArchiveResult(true, databasePath, null);
    }

    public static RestoreArchiveResult Failure(string errorMessage)
    {
        return new RestoreArchiveResult(false, null, errorMessage);
    }
}

