using System.IO;

namespace DeckDeckDeck.App.Services;

internal sealed class ImageStorageService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif"
    };

    private readonly FileStorageService _fileStorageService;

    public ImageStorageService(FileStorageService fileStorageService)
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
        if (string.IsNullOrWhiteSpace(path) || !IsAppImagePath(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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
