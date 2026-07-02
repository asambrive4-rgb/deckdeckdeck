using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class CategoryEditDraftTests
{
    [Fact]
    public void DeleteCurrentUnsavedImageDeletesReplacementOnCancel()
    {
        var imageRepository = new RecordingImageFileRepository();
        var draft = CategoryEditDraft.FromCategory(null, imageRepository);

        draft.ReplaceImageFromPath("source.png");
        draft.DeleteCurrentUnsavedImage();

        var deleted = Assert.Single(imageRepository.DeletedImages);
        Assert.Equal("stored-source.png", deleted.ImagePath);
        Assert.Equal("stored-source-thumb.png", deleted.ThumbnailPath);
    }

    [Fact]
    public void MarkSavedDeletesOriginalImageAfterReplacement()
    {
        var imageRepository = new RecordingImageFileRepository();
        var category = new Category
        {
            ImagePath = "original.png",
            ThumbnailPath = "original-thumb.png"
        };
        var draft = CategoryEditDraft.FromCategory(category, imageRepository);

        draft.ReplaceImageFromPath("source.png");
        draft.MarkSaved();
        draft.DeleteCurrentUnsavedImage();

        var deleted = Assert.Single(imageRepository.DeletedImages);
        Assert.Equal("original.png", deleted.ImagePath);
        Assert.Equal("original-thumb.png", deleted.ThumbnailPath);
    }

    [Fact]
    public void PreviewThumbnailPathUsesDisplayPathResolver()
    {
        var category = new Category
        {
            ImagePath = "image.png",
            ThumbnailPath = "thumbnail.png"
        };
        var draft = CategoryEditDraft.FromCategory(
            category,
            imageFileRepository: null,
            new PrefixPathResolver("display:"));

        Assert.Equal("display:thumbnail.png", draft.PreviewThumbnailPath);
    }

    private sealed class RecordingImageFileRepository : IImageFileRepository
    {
        public List<ImageFileReference> DeletedImages { get; } = [];

        public StoredImageReference StoreImage(string sourcePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            return new StoredImageReference(
                $"stored-{fileName}.png",
                $"stored-{fileName}-thumb.png");
        }

        public void DeleteImageFiles(ImageFileReference imageFiles)
        {
            DeletedImages.Add(imageFiles);
        }
    }

    private sealed class PrefixPathResolver : IStoredImagePathResolver
    {
        private readonly string _prefix;

        public PrefixPathResolver(string prefix)
        {
            _prefix = prefix;
        }

        public string? ResolveDisplayPath(string? storedPath)
        {
            return storedPath is null ? null : _prefix + storedPath;
        }

        public bool FileExists(string? storedPath)
        {
            return !string.IsNullOrWhiteSpace(storedPath);
        }
    }
}
