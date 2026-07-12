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
        string? terminalWorkingDirectory = null)
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
                : null);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static SlotImageMode GetStoredSlotImageMode(SlotImageMode slotImageMode, string? imagePath)
    {
        return slotImageMode == SlotImageMode.Auto && !string.IsNullOrWhiteSpace(imagePath)
            ? SlotImageMode.Custom
            : slotImageMode;
    }
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
    string? TerminalWorkingDirectory);
