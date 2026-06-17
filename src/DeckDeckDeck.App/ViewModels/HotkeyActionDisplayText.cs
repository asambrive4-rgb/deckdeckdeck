using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

internal static class HotkeyActionDisplayText
{
    public static string GetActionTypeLabel(SnippetActionType actionType)
    {
        return actionType switch
        {
            SnippetActionType.LaunchFile => "파일/바로 가기 실행",
            SnippetActionType.LaunchUrl => "웹 주소 열기",
            SnippetActionType.MediaAction => "음악/미디어 제어",
            SnippetActionType.TerminalCommand => "터미널 명령 실행",
            _ => "문구 붙여넣기"
        };
    }

    public static string GetSummary(HotkeyAction action)
    {
        return action.ActionType switch
        {
            SnippetActionType.LaunchFile => string.IsNullOrWhiteSpace(action.LaunchPath)
                ? "실행 대상 없음"
                : action.LaunchPath,
            SnippetActionType.LaunchUrl => string.IsNullOrWhiteSpace(action.LaunchUrl)
                ? "웹 주소 없음"
                : action.LaunchUrl,
            SnippetActionType.MediaAction => GetMediaSummary(action),
            SnippetActionType.TerminalCommand => string.IsNullOrWhiteSpace(action.TerminalCommand)
                ? "터미널 명령 없음"
                : action.TerminalCommand,
            _ => GetPasteSummary(action)
        };
    }

    private static string GetPasteSummary(HotkeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Content))
        {
            return "붙여넣을 문구 없음";
        }

        var normalized = action.Content.ReplaceLineEndings(" ");
        return normalized.Length <= 42
            ? normalized
            : normalized[..42] + "...";
    }

    private static string GetMediaSummary(HotkeyAction action)
    {
        var provider = action.MediaProvider == SnippetMediaProvider.Spotify
            ? "Spotify"
            : "Windows";
        var command = SnippetMediaCommandOption.All
            .FirstOrDefault(option => option.Command == action.MediaCommand)
            ?.Label
            ?? "재생/일시정지";

        return $"{provider} / {command}";
    }
}
