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
    string? AdbDeviceIp = null)
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
            snippet.AdbDeviceIp);
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
            action.AdbDeviceIp);
    }
}
