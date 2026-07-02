using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class CategoryEditDraft
{
    private readonly EditableImageDraft _imageDraft;
    private readonly IStoredImagePathResolver? _storedImagePathResolver;

    private CategoryEditDraft(
        string? imagePath,
        string? thumbnailPath,
        IImageFileRepository? imageFileRepository,
        IStoredImagePathResolver? storedImagePathResolver)
    {
        _imageDraft = new EditableImageDraft(imagePath, thumbnailPath, imageFileRepository);
        _storedImagePathResolver = storedImagePathResolver;
        CanStoreImages = imageFileRepository is not null;
    }

    public string? ImagePath => _imageDraft.ImagePath;

    public string? ThumbnailPath => _imageDraft.ThumbnailPath;

    public string? PreviewThumbnailPath => ResolveDisplayPath(ThumbnailPath);

    public bool HasImage => _imageDraft.HasImage;

    public bool CanStoreImages { get; }

    public static CategoryEditDraft FromCategory(
        Category? category,
        IImageFileRepository? imageFileRepository,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        return new CategoryEditDraft(
            category?.ImagePath,
            category?.ThumbnailPath,
            imageFileRepository,
            storedImagePathResolver);
    }

    public void ReplaceImageFromPath(string sourcePath)
    {
        _imageDraft.ReplaceWithStoredImage(sourcePath);
    }

    public void RemoveImage()
    {
        _imageDraft.RemoveImage();
    }

    public void DeleteCurrentUnsavedImage()
    {
        _imageDraft.DeleteCurrentUnsavedImage();
    }

    public void MarkSaved()
    {
        _imageDraft.DeleteOriginalImageIfReplaced();
        _imageDraft.MarkCurrentAsOriginal();
    }

    private string? ResolveDisplayPath(string? storedPath)
    {
        return _storedImagePathResolver?.ResolveDisplayPath(storedPath) ?? storedPath;
    }
}
