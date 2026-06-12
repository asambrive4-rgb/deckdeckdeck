using DeckDeckDeck.App.UseCases.Ports;
using UseCaseImageFileReference = DeckDeckDeck.App.UseCases.Ports.ImageFileReference;

namespace DeckDeckDeck.App.Services;

public sealed record StoredImage(string ImagePath, string ThumbnailPath);

public sealed record ImageFileSet(string? ImagePath, string? ThumbnailPath);

public sealed class ThumbnailService : IImageFileManager
{
    private readonly ThumbnailGenerator _thumbnailGenerator = new();
    private readonly ImageStorageService _imageStorageService;

    public ThumbnailService(FileStorageService fileStorageService)
    {
        _imageStorageService = new ImageStorageService(fileStorageService);
    }

    public StoredImage StoreImage(string sourcePath)
    {
        var storedImage = _imageStorageService.PrepareStoredImage(sourcePath);

        try
        {
            _imageStorageService.CopyOriginal(sourcePath, storedImage.ImagePath);
            _thumbnailGenerator.CreateThumbnail(storedImage.ImagePath, storedImage.ThumbnailPath);

            return storedImage;
        }
        catch
        {
            DeleteImageFiles(storedImage.ImagePath, storedImage.ThumbnailPath);
            throw;
        }
    }

    public void DeleteImageFiles(ImageFileSet imageFiles)
    {
        _imageStorageService.DeleteImageFiles(imageFiles);
    }

    public void DeleteImageFiles(string? imagePath, string? thumbnailPath)
    {
        _imageStorageService.DeleteImageFiles(imagePath, thumbnailPath);
    }

    StoredImageReference IImageFileManager.StoreImage(string sourcePath)
    {
        var storedImage = StoreImage(sourcePath);
        return new StoredImageReference(storedImage.ImagePath, storedImage.ThumbnailPath);
    }

    void IImageFileManager.DeleteImageFiles(UseCaseImageFileReference imageFiles)
    {
        DeleteImageFiles(imageFiles.ImagePath, imageFiles.ThumbnailPath);
    }
}
