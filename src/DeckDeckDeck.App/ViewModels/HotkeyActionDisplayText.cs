// 역할: 단축키 설정 목록에서 각 동작의 내용(파일 경로, URL 등)을 한눈에 알아보기 쉬운 요약 텍스트로 변환합니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

internal static class HotkeyActionDisplayText
{
    public static string GetActionTypeLabel(HotkeyAction action) =>
        ExecutableActionTypeCatalog.GetDisplayLabel(
            action.ActionType,
            action.FileActionMode,
            action.TerminalCommand);

    public static string GetSummary(HotkeyAction action)
    {
        return action.ActionType switch
        {
            SnippetActionType.LaunchFile => string.IsNullOrWhiteSpace(action.LaunchPath)
                ? (action.FileActionMode == FileActionMode.Paste ? "붙여넣을 파일 없음" : "실행 대상 없음")
                : action.LaunchPath,
            SnippetActionType.LaunchUrl => string.IsNullOrWhiteSpace(action.LaunchUrl)
                ? "웹 주소 없음"
                : action.LaunchUrl,
            SnippetActionType.MediaAction => GetMediaSummary(action),
            SnippetActionType.TerminalCommand when TerminalCommandParameterRules.IsTurnOffDisplayCommand(action.TerminalCommand) =>
                "모니터 화면 끄기",
            SnippetActionType.TerminalCommand when TerminalCommandParameterRules.TryParseBluetoothAudioCommand(
                    action.TerminalCommand,
                    out var btCommand,
                    allowEmptyTarget: true) =>
                GetBluetoothSummary(btCommand),
            SnippetActionType.TerminalCommand when TerminalCommandParameterRules.IsAdbWirelessConnectCommand(action.TerminalCommand) =>
                string.IsNullOrWhiteSpace(action.AdbDeviceIp)
                    ? "무선 ADB 연결 (실행 시 IP·포트 입력)"
                    : $"무선 ADB 연결 ({action.AdbDeviceIp})",
            SnippetActionType.TerminalCommand => string.IsNullOrWhiteSpace(action.TerminalCommand)
                ? "터미널 명령 없음"
                : action.TerminalCommand,
            _ => GetPasteSummary(action)
        };
    }

    private static string GetBluetoothSummary(BluetoothAudioCommand command)
    {
        var verb = command.Action == BluetoothAudioActionKind.Disconnect ? "연결 해제" : "연결";
        return string.IsNullOrWhiteSpace(command.Target)
            ? $"블루투스 {verb} (대상 미지정)"
            : $"블루투스 {verb}: {command.Target}";
    }

    private static string GetPasteSummary(HotkeyAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Content))
        {
            return "붙여넣을 문구 없음";
        }

        var normalized = action.Content.ReplaceLineEndings(" ");
        return normalized.Length <= 42 ? normalized : normalized[..42] + "...";
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
