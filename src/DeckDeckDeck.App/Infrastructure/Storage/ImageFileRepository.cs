using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using UseCaseImageFileReference = DeckDeckDeck.App.UseCases.Ports.ImageFileReference;

namespace DeckDeckDeck.App.Infrastructure.Storage;

public sealed class ImageFileRepository : IImageFileRepository
{
    private readonly AppStoragePaths _fileStorageService;
    private readonly ThumbnailGenerator _thumbnailGenerator = new();
    private readonly ImageStorageRepository _imageStorageService;

    public ImageFileRepository(AppStoragePaths fileStorageService)
    {
        _fileStorageService = fileStorageService;
        _imageStorageService = new ImageStorageRepository(fileStorageService);
    }

    public StoredImage StoreImage(string sourcePath)
    {
        var storedImage = _imageStorageService.PrepareStoredImage(sourcePath);

        try
        {
            _imageStorageService.CopyOriginal(sourcePath, storedImage.ImagePath);
            _thumbnailGenerator.CreateThumbnail(storedImage.ImagePath, storedImage.ThumbnailPath);

            return new StoredImage(
                _fileStorageService.ToStoredPath(storedImage.ImagePath),
                _fileStorageService.ToStoredPath(storedImage.ThumbnailPath));
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

    StoredImageReference IImageFileRepository.StoreImage(string sourcePath)
    {
        var storedImage = StoreImage(sourcePath);
        return new StoredImageReference(storedImage.ImagePath, storedImage.ThumbnailPath);
    }

    void IImageFileRepository.DeleteImageFiles(UseCaseImageFileReference imageFiles)
    {
        DeleteImageFiles(imageFiles.ImagePath, imageFiles.ThumbnailPath);
    }
}
