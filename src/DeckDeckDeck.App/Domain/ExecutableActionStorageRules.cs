// 역할: 다양한 실행 동작 데이터를 저장하고 복원할 때 지켜야 할 형식과 규칙을 정의합니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public static class ExecutableActionStorageRules
{
    public static ExecutableActionStorageData Normalize(
        string title,
        string content,
        string? description,
        string? imagePath,
        SnippetActionType actionType,
        string? launchPath,
        SlotImageMode slotImageMode,
        AutoIconCacheEntry? autoIcon,
        string? launchUrl,
        SnippetMediaProvider? mediaProvider,
        SnippetMediaCommand? mediaCommand,
        PasteShortcutMode pasteShortcutMode,
        string? terminalCommand,
        SnippetTerminalShell? terminalShell,
        bool runAsAdministrator,
        FileActionMode fileActionMode,
        bool openTerminalWindow = false,
        string? terminalWorkingDirectory = null,
        string? adbDeviceIp = null)
    {
        var storedImageMode = GetStoredSlotImageMode(slotImageMode, imagePath);
        var isTerminal = actionType == SnippetActionType.TerminalCommand;

        return new ExecutableActionStorageData(
            title.Trim(),
            actionType == SnippetActionType.PasteText ? content : string.Empty,
            NormalizeOptionalText(description),
            actionType,
            actionType == SnippetActionType.LaunchFile ? NormalizeOptionalText(launchPath) : null,
            storedImageMode,
            actionType == SnippetActionType.LaunchFile && storedImageMode != SlotImageMode.None
                ? autoIcon
                : null,
            actionType == SnippetActionType.LaunchUrl ? NormalizeOptionalText(launchUrl) : null,
            actionType == SnippetActionType.MediaAction
                ? mediaProvider ?? SnippetMediaProvider.System
                : null,
            actionType == SnippetActionType.MediaAction
                ? mediaCommand ?? SnippetMediaCommand.PlayPause
                : null,
            actionType == SnippetActionType.PasteText
                ? pasteShortcutMode
                : PasteShortcutMode.CtrlV,
            isTerminal
                ? NormalizeOptionalText(terminalCommand)
                : null,
            isTerminal
                ? terminalShell ?? SnippetTerminalShell.Cmd
                : null,
            isTerminal && runAsAdministrator,
            actionType == SnippetActionType.LaunchFile
                ? fileActionMode
                : FileActionMode.Launch,
            isTerminal && openTerminalWindow,
            isTerminal
                ? NormalizeOptionalText(terminalWorkingDirectory)
                : null,
            isTerminal
                ? NormalizeOptionalText(adbDeviceIp)
                : null);
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SlotImageMode GetStoredSlotImageMode(SlotImageMode slotImageMode, string? imagePath) =>
        slotImageMode == SlotImageMode.Auto && !string.IsNullOrWhiteSpace(imagePath)
            ? SlotImageMode.Custom
            : slotImageMode;
}

public sealed record ExecutableActionStorageData(
    string Title,
    string Content,
    string? Description,
    SnippetActionType ActionType,
    string? LaunchPath,
    SlotImageMode SlotImageMode,
    AutoIconCacheEntry? AutoIcon,
    string? LaunchUrl,
    SnippetMediaProvider? MediaProvider,
    SnippetMediaCommand? MediaCommand,
    PasteShortcutMode PasteShortcutMode,
    string? TerminalCommand,
    SnippetTerminalShell? TerminalShell,
    bool RunAsAdministrator,
    FileActionMode FileActionMode,
    bool OpenTerminalWindow,
    string? TerminalWorkingDirectory,
    string? AdbDeviceIp);
