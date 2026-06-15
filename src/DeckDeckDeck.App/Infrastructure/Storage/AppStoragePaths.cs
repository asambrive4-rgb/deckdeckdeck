using DeckDeckDeck.App.Composition;
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

namespace DeckDeckDeck.App.Infrastructure.Storage;

public sealed class AppStoragePaths
{
    public const string AppFolderName = "NumpadPromptLauncher";
    public const string IconCacheRelativeRoot = "icon-cache";
    public const string ImagesRelativeRoot = "images";

    public AppStoragePaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName))
    {
    }

    public AppStoragePaths(string appDataPath)
    {
        AppDataPath = appDataPath;
        DatabasePath = Path.Combine(AppDataPath, "launcher.db");
        ImagesPath = Path.Combine(AppDataPath, "images");
        ImageOriginalsPath = Path.Combine(ImagesPath, "originals");
        ImageThumbnailsPath = Path.Combine(ImagesPath, "thumbnails");
        IconCachePath = Path.Combine(AppDataPath, "icon-cache");
        LogsPath = Path.Combine(AppDataPath, "logs");
        TempPath = Path.Combine(AppDataPath, "temp");
    }

    public string AppDataPath { get; }

    public string DatabasePath { get; }

    public string ImagesPath { get; }

    public string ImageOriginalsPath { get; }

    public string ImageThumbnailsPath { get; }

    public string IconCachePath { get; }

    public string LogsPath { get; }

    public string TempPath { get; }

    public string ToStoredPath(string path)
    {
        return TryGetManagedRelativePath(path, out var relativePath)
            ? relativePath
            : path;
    }

    public string ToAbsolutePath(string path)
    {
        if (TryGetManagedRelativePath(path, out var relativePath))
        {
            return Path.Combine(AppDataPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        return path;
    }

    public bool ManagedFileExists(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(ToAbsolutePath(path));
    }

    public bool TryGetManagedRelativePath(string? path, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmedPath = path.Trim();
        if (!Path.IsPathRooted(trimmedPath))
        {
            return TryNormalizeManagedRelativePath(trimmedPath, out relativePath);
        }

        try
        {
            var fullPath = Path.GetFullPath(trimmedPath);
            var appDataRelative = Path.GetRelativePath(AppDataPath, fullPath);
            if (TryNormalizeManagedRelativePath(appDataRelative, out relativePath))
            {
                return true;
            }

            return TryGetLegacyManagedRelativePath(fullPath, out relativePath);
        }
        catch
        {
            return false;
        }
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(ImageOriginalsPath);
        Directory.CreateDirectory(ImageThumbnailsPath);
        Directory.CreateDirectory(IconCachePath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(TempPath);
    }

    private static bool TryGetLegacyManagedRelativePath(string fullPath, out string relativePath)
    {
        relativePath = string.Empty;
        var parts = fullPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var appFolderIndex = Array.FindLastIndex(
            parts,
            part => string.Equals(part, AppFolderName, StringComparison.OrdinalIgnoreCase));
        if (appFolderIndex < 0 || appFolderIndex >= parts.Length - 1)
        {
            return false;
        }

        return TryNormalizeManagedRelativePath(
            string.Join('/', parts.Skip(appFolderIndex + 1)),
            out relativePath);
    }

    private static bool TryNormalizeManagedRelativePath(string path, out string relativePath)
    {
        relativePath = string.Empty;
        var normalized = path.Replace('\\', '/').TrimStart('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Any(part => part is "." or ".."))
        {
            return false;
        }

        if (!string.Equals(parts[0], ImagesRelativeRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parts[0], IconCacheRelativeRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativePath = string.Join('/', parts);
        return true;
    }
}
