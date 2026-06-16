using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

internal sealed class EditableImageDraft
{
    private readonly IImageFileRepository? _imageFileRepository;
    private string? _originalImagePath;
    private string? _originalThumbnailPath;

    public EditableImageDraft(
        string? imagePath,
        string? thumbnailPath,
        IImageFileRepository? imageFileRepository)
    {
        _imageFileRepository = imageFileRepository;
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
        if (_imageFileRepository is null)
        {
            throw new InvalidOperationException("Image storage is not available.");
        }

        var storedImage = _imageFileRepository.StoreImage(sourcePath);
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
        if (_imageFileRepository is null || IsCurrentOriginalImage())
        {
            return;
        }

        _imageFileRepository.DeleteImageFiles(new ImageFileReference(ImagePath, ThumbnailPath));
    }

    public void DeleteOriginalImageIfReplaced()
    {
        if (_imageFileRepository is null || IsCurrentOriginalImage())
        {
            return;
        }

        _imageFileRepository.DeleteImageFiles(
            new ImageFileReference(_originalImagePath, _originalThumbnailPath));
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
