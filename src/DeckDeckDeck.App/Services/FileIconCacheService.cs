using System.IO;
using System.Security.Cryptography;
using System.Text;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class FileIconCacheService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".lnk"
    };

    private readonly IFileIconExtractor _fileIconExtractor;
    private readonly FileStorageService _fileStorageService;
    private readonly LoggingService? _loggingService;

    public FileIconCacheService(
        FileStorageService fileStorageService,
        IFileIconExtractor fileIconExtractor,
        LoggingService? loggingService = null)
    {
        _fileStorageService = fileStorageService;
        _fileIconExtractor = fileIconExtractor;
        _loggingService = loggingService;
    }

    public AutoIconCacheEntry? GetOrCreateIcon(string? sourcePath, AutoIconCacheEntry? current)
    {
        if (!TryGetSupportedFile(sourcePath, out var fileInfo))
        {
            return null;
        }

        if (current is not null
            && current.Matches(fileInfo)
            && File.Exists(_fileStorageService.ToAbsolutePath(current.IconPath)))
        {
            return new AutoIconCacheEntry(
                _fileStorageService.ToStoredPath(current.IconPath),
                current.SourcePath,
                current.SourceLastWriteTimeUtc,
                current.SourceLength);
        }

        Directory.CreateDirectory(_fileStorageService.IconCachePath);
        var cachePath = Path.Combine(_fileStorageService.IconCachePath, GetCacheFileName(fileInfo));
        var newEntry = new AutoIconCacheEntry(
            _fileStorageService.ToStoredPath(cachePath),
            fileInfo.FullName,
            fileInfo.LastWriteTimeUtc,
            fileInfo.Length);

        if (File.Exists(cachePath))
        {
            return newEntry;
        }

        try
        {
            if (_fileIconExtractor.TryExtractIcon(fileInfo.FullName, cachePath) && File.Exists(cachePath))
            {
                return newEntry;
            }
        }
        catch (Exception ex)
        {
            _loggingService?.Log($"File icon extraction failed for {fileInfo.FullName}.", ex);
        }

        DeletePartialCacheFile(cachePath);
        return null;
    }

    public static bool IsSupportedIconSource(string? sourcePath)
    {
        return TryGetSupportedFile(sourcePath, out _);
    }

    private static bool TryGetSupportedFile(string? sourcePath, out FileInfo fileInfo)
    {
        fileInfo = null!;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        try
        {
            var extension = Path.GetExtension(sourcePath);
            if (!SupportedExtensions.Contains(extension) || !File.Exists(sourcePath))
            {
                return false;
            }

            fileInfo = new FileInfo(sourcePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetCacheFileName(FileInfo fileInfo)
    {
        var key = string.Join(
            "|",
            fileInfo.FullName.ToUpperInvariant(),
            fileInfo.LastWriteTimeUtc.Ticks.ToString(),
            fileInfo.Length.ToString());
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        return $"{hash.ToLowerInvariant()}.png";
    }

    private static void DeletePartialCacheFile(string cachePath)
    {
        try
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
        catch
        {
            // Cache cleanup is best-effort; a stale partial file should not interrupt the app.
        }
    }
}
