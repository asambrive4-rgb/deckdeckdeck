using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class EditableImageState
{
    private readonly ImageFileRepository? _thumbnailService;
    private string? _originalImagePath;
    private string? _originalThumbnailPath;

    public EditableImageState(string? imagePath, string? thumbnailPath, ImageFileRepository? thumbnailService)
    {
        _thumbnailService = thumbnailService;
        _originalImagePath = imagePath;
        _originalThumbnailPath = thumbnailPath;
        ImagePath = imagePath;
        ThumbnailPath = thumbnailPath;
    }

    public string? ImagePath { get; private set; }

    public string? ThumbnailPath { get; private set; }

    public bool HasImage => !string.IsNullOrWhiteSpace(ThumbnailPath);

    public void ReplaceWithStoredImage(string sourcePath)
    {
        if (_thumbnailService is null)
        {
            throw new InvalidOperationException("Image storage is not available.");
        }

        var storedImage = _thumbnailService.StoreImage(sourcePath);
        DeleteCurrentUnsavedImage();
        ImagePath = storedImage.ImagePath;
        ThumbnailPath = storedImage.ThumbnailPath;
    }

    public void RemoveImage()
    {
        DeleteCurrentUnsavedImage();
        ImagePath = null;
        ThumbnailPath = null;
    }

    public void DeleteCurrentUnsavedImage()
    {
        if (_thumbnailService is null || IsCurrentOriginalImage())
        {
            return;
        }

        _thumbnailService.DeleteImageFiles(ImagePath, ThumbnailPath);
    }

    public void DeleteOriginalImageIfReplaced()
    {
        if (_thumbnailService is null || IsCurrentOriginalImage())
        {
            return;
        }

        _thumbnailService.DeleteImageFiles(_originalImagePath, _originalThumbnailPath);
    }

    public void MarkCurrentAsOriginal()
    {
        _originalImagePath = ImagePath;
        _originalThumbnailPath = ThumbnailPath;
    }

    private bool IsCurrentOriginalImage()
    {
        return SamePath(ImagePath, _originalImagePath)
            && SamePath(ThumbnailPath, _originalThumbnailPath);
    }

    private static bool SamePath(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
