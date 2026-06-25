using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases.Ports;

public interface IImageFileRepository
{
    StoredImageReference StoreImage(string sourcePath);

    void DeleteImageFiles(ImageFileReference imageFiles);
}

public interface IStoredImagePathResolver
{
    string? ResolveDisplayPath(string? storedPath);

    bool FileExists(string? storedPath);
}

public interface ISnippetImageResolver
{
    string? GetDisplayImagePath(Snippet? snippet);

    AutoIconCacheEntry? PrepareAutoIcon(
        SnippetActionType actionType,
        string? launchPath,
        AutoIconCacheEntry? current);
}
