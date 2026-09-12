// 역할: 이미지 파일 저장소 및 저장된 파일 경로 변환에 필요한 입출력 규격을 정의합니다.
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
