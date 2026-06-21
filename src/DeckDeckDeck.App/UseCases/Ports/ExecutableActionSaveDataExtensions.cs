using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.UseCases.Ports;

public static class ExecutableActionSaveDataExtensions
{
    public static SnippetSaveData NormalizeForStorage(this SnippetSaveData data)
    {
        var normalized = ExecutableActionStorageRules.Normalize(
            data.Title,
            data.Content,
            data.Description,
            data.ImagePath,
            data.ActionType,
            data.LaunchPath,
            data.SlotImageMode,
            data.AutoIcon,
            data.LaunchUrl,
            data.MediaProvider,
            data.MediaCommand,
            data.PasteShortcutMode,
            data.TerminalCommand,
            data.TerminalShell,
            data.RunAsAdministrator,
            data.FileActionMode);

        return data with
        {
            Title = normalized.Title,
            Content = normalized.Content,
            Description = normalized.Description,
            ActionType = normalized.ActionType,
            LaunchPath = normalized.LaunchPath,
            SlotImageMode = normalized.SlotImageMode,
            AutoIcon = normalized.AutoIcon,
            LaunchUrl = normalized.LaunchUrl,
            MediaProvider = normalized.MediaProvider,
            MediaCommand = normalized.MediaCommand,
            PasteShortcutMode = normalized.PasteShortcutMode,
            TerminalCommand = normalized.TerminalCommand,
            TerminalShell = normalized.TerminalShell,
            RunAsAdministrator = normalized.RunAsAdministrator,
            FileActionMode = normalized.FileActionMode
        };
    }

    public static HotkeyActionSaveData NormalizeForStorage(this HotkeyActionSaveData data)
    {
        var normalized = ExecutableActionStorageRules.Normalize(
            data.Title,
            data.Content,
            data.Description,
            data.ImagePath,
            data.ActionType,
            data.LaunchPath,
            data.SlotImageMode,
            data.AutoIcon,
            data.LaunchUrl,
            data.MediaProvider,
            data.MediaCommand,
            data.PasteShortcutMode,
            data.TerminalCommand,
            data.TerminalShell,
            data.RunAsAdministrator,
            data.FileActionMode);

        return data with
        {
            Title = normalized.Title,
            Content = normalized.Content,
            Description = normalized.Description,
            ActionType = normalized.ActionType,
            LaunchPath = normalized.LaunchPath,
            SlotImageMode = normalized.SlotImageMode,
            AutoIcon = normalized.AutoIcon,
            LaunchUrl = normalized.LaunchUrl,
            MediaProvider = normalized.MediaProvider,
            MediaCommand = normalized.MediaCommand,
            PasteShortcutMode = normalized.PasteShortcutMode,
            TerminalCommand = normalized.TerminalCommand,
            TerminalShell = normalized.TerminalShell,
            RunAsAdministrator = normalized.RunAsAdministrator,
            FileActionMode = normalized.FileActionMode
        };
    }
}
