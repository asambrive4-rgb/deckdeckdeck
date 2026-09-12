// 역할: 파일 실행, 웹 주소 열기, 미디어 제어 등 슬롯에서 실행할 수 있는 다양한 동작의 공통 데이터 모델입니다.
namespace DeckDeckDeck.App.Models;

public sealed record ExecutableAction(
    Guid Id,
    string Title,
    string Content,
    SnippetActionType ActionType,
    PasteShortcutMode PasteShortcutMode,
    string? LaunchPath,
    FileActionMode FileActionMode,
    string? LaunchUrl,
    SnippetMediaProvider? MediaProvider,
    SnippetMediaCommand? MediaCommand,
    string? TerminalCommand,
    SnippetTerminalShell? TerminalShell,
    bool RunAsAdministrator,
    bool OpenTerminalWindow = false,
    string? TerminalWorkingDirectory = null,
    string? AdbDeviceIp = null,
    SlotKey? TargetSlotKey = null)
{
    public static ExecutableAction FromSnippet(Snippet snippet)
    {
        return new ExecutableAction(
            snippet.Id,
            snippet.Title,
            snippet.Content,
            snippet.ActionType,
            snippet.PasteShortcutMode,
            snippet.LaunchPath,
            snippet.FileActionMode,
            snippet.LaunchUrl,
            snippet.MediaProvider,
            snippet.MediaCommand,
            snippet.TerminalCommand,
            snippet.TerminalShell,
            snippet.RunAsAdministrator,
            snippet.OpenTerminalWindow,
            snippet.TerminalWorkingDirectory,
            snippet.AdbDeviceIp,
            snippet.SlotKey);
    }

    public static ExecutableAction FromHotkeyAction(HotkeyAction action)
    {
        return new ExecutableAction(
            action.Id,
            action.Title,
            action.Content,
            action.ActionType,
            action.PasteShortcutMode,
            action.LaunchPath,
            action.FileActionMode,
            action.LaunchUrl,
            action.MediaProvider,
            action.MediaCommand,
            action.TerminalCommand,
            action.TerminalShell,
            action.RunAsAdministrator,
            action.OpenTerminalWindow,
            action.TerminalWorkingDirectory,
            action.AdbDeviceIp,
            TargetSlotKey: null);
    }
}
