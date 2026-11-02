// 역할: 파일, 프로그램 또는 설치된 앱(shell:AppsFolder)에서 추출한 아이콘의 캐시 정보를 담는 데이터 모델입니다.
using DeckDeckDeck.App.Domain;
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

    public bool Matches(string sourcePath, DateTime lastWriteTimeUtc, long sourceLength)
    {
        if (LaunchPathRules.IsShellAppsFolderPath(SourcePath) || LaunchPathRules.IsShellAppsFolderPath(sourcePath))
        {
            return LaunchPathRules.IsShellAppsFolderPath(SourcePath)
                && LaunchPathRules.IsShellAppsFolderPath(sourcePath)
                && string.Equals(
                    LaunchPathRules.NormalizeShellAppsFolderPath(SourcePath),
                    LaunchPathRules.NormalizeShellAppsFolderPath(sourcePath),
                    StringComparison.OrdinalIgnoreCase)
                && SourceLastWriteTimeUtc == lastWriteTimeUtc
                && SourceLength == sourceLength;
        }

        return TryGetFullPath(SourcePath, out var fullCurrent)
            && TryGetFullPath(sourcePath, out var fullTarget)
            && string.Equals(fullCurrent, fullTarget, StringComparison.OrdinalIgnoreCase)
            && SourceLastWriteTimeUtc == lastWriteTimeUtc
            && SourceLength == sourceLength;
    }

    public bool Matches(FileInfo fileInfo)
    {
        return Matches(fileInfo.FullName, fileInfo.LastWriteTimeUtc, fileInfo.Length);
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(LaunchPathRules.ExpandPath(path));
            return true;
        }
        catch
        {
            fullPath = string.Empty;
            return false;
        }
    }
}
