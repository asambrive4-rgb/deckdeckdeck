using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeckDeckDeck.App.Services;

public sealed record StoredImage(string ImagePath, string ThumbnailPath);

public sealed record ImageFileSet(string? ImagePath, string? ThumbnailPath);

public sealed class ThumbnailService
{
    private const int ThumbnailMaxPixels = 96;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif"
    };

    private readonly FileStorageService _fileStorageService;

    public ThumbnailService(FileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public StoredImage StoreImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new InvalidOperationException("Image file does not exist.");
        }

        var extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported image type. Use PNG, JPG, BMP, or GIF.");
        }

        Directory.CreateDirectory(_fileStorageService.ImageOriginalsPath);
        Directory.CreateDirectory(_fileStorageService.ImageThumbnailsPath);

        var fileId = Guid.NewGuid().ToString("N");
        var originalPath = Path.Combine(
            _fileStorageService.ImageOriginalsPath,
            $"{fileId}{extension.ToLowerInvariant()}");
        var thumbnailPath = Path.Combine(_fileStorageService.ImageThumbnailsPath, $"{fileId}.png");

        try
        {
            File.Copy(sourcePath, originalPath);
            CreateThumbnail(originalPath, thumbnailPath);
            return new StoredImage(originalPath, thumbnailPath);
        }
        catch
        {
            DeleteImageFiles(originalPath, thumbnailPath);
            throw;
        }
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

    private static void CreateThumbnail(string sourcePath, string thumbnailPath)
    {
        var source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.UriSource = new Uri(sourcePath, UriKind.Absolute);
        source.EndInit();
        source.Freeze();

        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            throw new InvalidOperationException("Image could not be loaded.");
        }

        var longestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        var scale = (double)ThumbnailMaxPixels / longestSide;
        var thumbnail = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        thumbnail.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(thumbnail));

        using var stream = File.Create(thumbnailPath);
        encoder.Save(stream);
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
