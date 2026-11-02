// 역할: 파일, 환경변수 경로 또는 설치된 앱(shell:AppsFolder)에서 추출한 아이콘 이미지를 로컬에 캐시하여 빠르게 불러옵니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DeckDeckDeck.App.Infrastructure.Storage;

public sealed class FileIconCacheRepository
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".exe", ".lnk"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IFileIconExtractor _fileIconExtractor;
    private readonly AppStoragePaths _fileStorageService;
    private readonly FileLogger? _loggingService;

    public FileIconCacheRepository(
        AppStoragePaths fileStorageService,
        IFileIconExtractor fileIconExtractor,
        FileLogger? loggingService = null)
    {
        _fileStorageService = fileStorageService;
        _fileIconExtractor = fileIconExtractor;
        _loggingService = loggingService;
    }

    public AutoIconCacheEntry? GetOrCreateIcon(string? sourcePath, AutoIconCacheEntry? current)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        if (LaunchPathRules.IsShellAppsFolderPath(sourcePath))
        {
            return GetOrCreateShellAppIcon(sourcePath, current);
        }

        if (!TryGetSupportedFile(sourcePath, out var fileInfo))
        {
            return null;
        }

        if (current is not null
            && current.Matches(fileInfo.FullName, fileInfo.LastWriteTimeUtc, fileInfo.Length)
            && File.Exists(_fileStorageService.ToAbsolutePath(current.IconPath)))
        {
            return new AutoIconCacheEntry(
                _fileStorageService.ToStoredPath(current.IconPath),
                current.SourcePath,
                current.SourceLastWriteTimeUtc,
                current.SourceLength);
        }

        Directory.CreateDirectory(_fileStorageService.IconCachePath);
        var cachePath = Path.Combine(
            _fileStorageService.IconCachePath,
            GetCacheFileName(fileInfo.FullName, fileInfo.LastWriteTimeUtc, fileInfo.Length));
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

    private AutoIconCacheEntry? GetOrCreateShellAppIcon(string sourcePath, AutoIconCacheEntry? current)
    {
        var normalizedSourcePath = LaunchPathRules.NormalizeShellAppsFolderPath(sourcePath);
        var lastWriteTimeUtc = DateTime.UnixEpoch;
        const long sourceLength = 0L;

        if (current is not null
            && current.Matches(normalizedSourcePath, lastWriteTimeUtc, sourceLength)
            && File.Exists(_fileStorageService.ToAbsolutePath(current.IconPath)))
        {
            return new AutoIconCacheEntry(
                _fileStorageService.ToStoredPath(current.IconPath),
                current.SourcePath,
                current.SourceLastWriteTimeUtc,
                current.SourceLength);
        }

        Directory.CreateDirectory(_fileStorageService.IconCachePath);
        var cachePath = Path.Combine(
            _fileStorageService.IconCachePath,
            GetCacheFileName(normalizedSourcePath, lastWriteTimeUtc, sourceLength));
        var newEntry = new AutoIconCacheEntry(
            _fileStorageService.ToStoredPath(cachePath),
            normalizedSourcePath,
            lastWriteTimeUtc,
            sourceLength);

        if (File.Exists(cachePath))
        {
            return newEntry;
        }

        try
        {
            if (_fileIconExtractor.TryExtractIcon(normalizedSourcePath, cachePath) && File.Exists(cachePath))
            {
                return newEntry;
            }
        }
        catch (Exception ex)
        {
            _loggingService?.Log($"Shell app icon extraction failed for {normalizedSourcePath}.", ex);
        }

        DeletePartialCacheFile(cachePath);
        return null;
    }

    public static bool IsSupportedIconSource(string? sourcePath) =>
        LaunchPathRules.IsShellAppsFolderPath(sourcePath) || TryGetSupportedFile(sourcePath, out _);

    private static bool TryGetSupportedFile(string? sourcePath, out FileInfo fileInfo)
    {
        fileInfo = null!;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        try
        {
            var expandedPath = LaunchPathRules.ExpandPath(sourcePath);
            var extension = Path.GetExtension(expandedPath);
            if (!SupportedExtensions.Contains(extension) || !File.Exists(expandedPath))
            {
                return false;
            }

            fileInfo = new FileInfo(expandedPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetCacheFileName(string sourceKey, DateTime lastWriteTimeUtc, long length)
    {
        var key = string.Join(
            "|",
            sourceKey.ToUpperInvariant(),
            lastWriteTimeUtc.Ticks.ToString(),
            length.ToString());
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
