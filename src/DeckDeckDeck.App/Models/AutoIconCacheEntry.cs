// 역할: 파일이나 프로그램에서 추출한 아이콘의 캐시 정보를 담는 데이터 모델입니다.
using System.IO;

namespace DeckDeckDeck.App.Models;

public sealed record AutoIconCacheEntry(
    string IconPath,
    string SourcePath,
    DateTime SourceLastWriteTimeUtc,
    long SourceLength)
{
    public static AutoIconCacheEntry? FromSnippet(Snippet? snippet)
    {
        if (snippet is null
            || string.IsNullOrWhiteSpace(snippet.AutoIconPath)
            || string.IsNullOrWhiteSpace(snippet.AutoIconSourcePath)
            || !snippet.AutoIconSourceLastWriteTimeUtc.HasValue
            || !snippet.AutoIconSourceLength.HasValue)
        {
            return null;
        }

        return new AutoIconCacheEntry(
            snippet.AutoIconPath,
            snippet.AutoIconSourcePath,
            snippet.AutoIconSourceLastWriteTimeUtc.Value,
            snippet.AutoIconSourceLength.Value);
    }

    public static AutoIconCacheEntry? FromHotkeyAction(HotkeyAction? action)
    {
        if (action is null
            || string.IsNullOrWhiteSpace(action.AutoIconPath)
            || string.IsNullOrWhiteSpace(action.AutoIconSourcePath)
            || !action.AutoIconSourceLastWriteTimeUtc.HasValue
            || !action.AutoIconSourceLength.HasValue)
        {
            return null;
        }

        return new AutoIconCacheEntry(
            action.AutoIconPath,
            action.AutoIconSourcePath,
            action.AutoIconSourceLastWriteTimeUtc.Value,
            action.AutoIconSourceLength.Value);
    }

    public bool Matches(FileInfo fileInfo)
    {
        return string.Equals(
                Path.GetFullPath(SourcePath),
                fileInfo.FullName,
                StringComparison.OrdinalIgnoreCase)
            && SourceLastWriteTimeUtc == fileInfo.LastWriteTimeUtc
            && SourceLength == fileInfo.Length;
    }
}
