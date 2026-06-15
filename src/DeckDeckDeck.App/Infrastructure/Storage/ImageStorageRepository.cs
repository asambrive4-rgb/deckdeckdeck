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

internal sealed class ImageStorageRepository
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif"
    };

    private readonly AppStoragePaths _fileStorageService;

    public ImageStorageRepository(AppStoragePaths fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public StoredImage PrepareStoredImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new InvalidOperationException("이미지 파일이 존재하지 않습니다.");
        }

        var extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("지원하지 않는 이미지 형식입니다. PNG, JPG, BMP, GIF를 사용해 주세요.");
        }

        Directory.CreateDirectory(_fileStorageService.ImageOriginalsPath);
        Directory.CreateDirectory(_fileStorageService.ImageThumbnailsPath);

        var fileId = Guid.NewGuid().ToString("N");
        var originalPath = Path.Combine(
            _fileStorageService.ImageOriginalsPath,
            $"{fileId}{extension.ToLowerInvariant()}");
        var thumbnailPath = Path.Combine(_fileStorageService.ImageThumbnailsPath, $"{fileId}.png");

        return new StoredImage(originalPath, thumbnailPath);
    }

    public void CopyOriginal(string sourcePath, string originalPath)
    {
        File.Copy(sourcePath, originalPath);
    }

    public void DeleteImageFiles(ImageFileSet imageFiles)
    {
        DeleteImageFiles(imageFiles.ImagePath, imageFiles.ThumbnailPath);
    }

    public void DeleteImageFiles(string? imagePath, string? thumbnailPath)
    {
        DeleteImageFile(imagePath);
        DeleteImageFile(thumbnailPath);
    }

    private void DeleteImageFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var absolutePath = _fileStorageService.ToAbsolutePath(path);
        if (!IsAppImagePath(absolutePath))
        {
            return;
        }

        try
        {
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
        catch
        {
            // Image cleanup is best-effort; a locked thumbnail should not interrupt the app.
        }
    }

    private bool IsAppImagePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return IsUnderDirectory(fullPath, _fileStorageService.ImageOriginalsPath)
            || IsUnderDirectory(fullPath, _fileStorageService.ImageThumbnailsPath);
    }

    private static bool IsUnderDirectory(string fullPath, string directory)
    {
        var fullDirectory = Path.GetFullPath(directory);
        if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            fullDirectory += Path.DirectorySeparatorChar;
        }

        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }
}

