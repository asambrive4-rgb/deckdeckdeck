using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class ExecutableActionStorageRulesTests
{
    [Theory]
    [InlineData(SnippetActionType.PasteText)]
    [InlineData(SnippetActionType.LaunchFile)]
    [InlineData(SnippetActionType.LaunchUrl)]
    [InlineData(SnippetActionType.MediaAction)]
    [InlineData(SnippetActionType.TerminalCommand)]
    public void NormalizeForStorageAppliesSameExecutableFieldsToSnippetsAndHotkeys(
        SnippetActionType actionType)
    {
        var autoIcon = new AutoIconCacheEntry(
            "icon.png",
            @"C:\tools\app.exe",
            new DateTime(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc),
            123);
        var snippet = CreateSnippetSaveData(actionType, autoIcon).NormalizeForStorage();
        var hotkey = CreateHotkeyActionSaveData(actionType, autoIcon).NormalizeForStorage();

        Assert.Equal(snippet.Title, hotkey.Title);
        Assert.Equal(snippet.Content, hotkey.Content);
        Assert.Equal(snippet.Description, hotkey.Description);
        Assert.Equal(snippet.ActionType, hotkey.ActionType);
        Assert.Equal(snippet.LaunchPath, hotkey.LaunchPath);
        Assert.Equal(snippet.SlotImageMode, hotkey.SlotImageMode);
        Assert.Equal(snippet.AutoIcon, hotkey.AutoIcon);
        Assert.Equal(snippet.LaunchUrl, hotkey.LaunchUrl);
        Assert.Equal(snippet.MediaProvider, hotkey.MediaProvider);
        Assert.Equal(snippet.MediaCommand, hotkey.MediaCommand);
        Assert.Equal(snippet.PasteShortcutMode, hotkey.PasteShortcutMode);
        Assert.Equal(snippet.TerminalCommand, hotkey.TerminalCommand);
        Assert.Equal(snippet.TerminalShell, hotkey.TerminalShell);
        Assert.Equal(snippet.RunAsAdministrator, hotkey.RunAsAdministrator);
        Assert.Equal(snippet.FileActionMode, hotkey.FileActionMode);

        Assert.Equal("Title", snippet.Title);
        Assert.Equal("description", snippet.Description);
        Assert.Equal(SlotImageMode.Custom, snippet.SlotImageMode);
        AssertExpectedExecutableFields(actionType, snippet, autoIcon);
    }

    private static SnippetSaveData CreateSnippetSaveData(
        SnippetActionType actionType,
        AutoIconCacheEntry autoIcon)
    {
        return new SnippetSaveData(
            "  Title  ",
            "content",
            "  description  ",
            "custom.png",
            "custom-thumb.png",
            actionType,
            "  C:\\tools\\app.exe  ",
            SlotImageMode.Auto,
            autoIcon,
            "  https://example.com/docs  ",
            SnippetMediaProvider.Spotify,
            SnippetMediaCommand.NextTrack,
            PasteShortcutMode.CtrlShiftV,
            "  echo hello  ",
            SnippetTerminalShell.PowerShell,
            RunAsAdministrator: false,
            FileActionMode: FileActionMode.Paste);
    }

    private static HotkeyActionSaveData CreateHotkeyActionSaveData(
        SnippetActionType actionType,
        AutoIconCacheEntry autoIcon)
    {
        return new HotkeyActionSaveData(
            "  Title  ",
            new HotkeyGesture(0x67, HotkeyModifiers.None),
            IsEnabled: true,
            "content",
            "  description  ",
            "custom.png",
            "custom-thumb.png",
            actionType,
            "  C:\\tools\\app.exe  ",
            SlotImageMode.Auto,
            autoIcon,
            "  https://example.com/docs  ",
            SnippetMediaProvider.Spotify,
            SnippetMediaCommand.NextTrack,
            PasteShortcutMode.CtrlShiftV,
            "  echo hello  ",
            SnippetTerminalShell.PowerShell,
            RunAsAdministrator: false,
            FileActionMode: FileActionMode.Paste);
    }

    private static void AssertExpectedExecutableFields(
        SnippetActionType actionType,
        SnippetSaveData data,
        AutoIconCacheEntry autoIcon)
    {
        switch (actionType)
        {
            case SnippetActionType.PasteText:
                Assert.Equal("content", data.Content);
                Assert.Equal(PasteShortcutMode.CtrlShiftV, data.PasteShortcutMode);
                Assert.Null(data.LaunchPath);
                Assert.Null(data.LaunchUrl);
                Assert.Null(data.MediaProvider);
                Assert.Null(data.MediaCommand);
                Assert.Null(data.TerminalCommand);
                Assert.Null(data.TerminalShell);
                Assert.False(data.RunAsAdministrator);
                Assert.Null(data.AutoIcon);
                Assert.Equal(FileActionMode.Launch, data.FileActionMode);
                break;
            case SnippetActionType.LaunchFile:
                Assert.Equal(string.Empty, data.Content);
                Assert.Equal(@"C:\tools\app.exe", data.LaunchPath);
                Assert.Equal(PasteShortcutMode.CtrlV, data.PasteShortcutMode);
                Assert.Null(data.LaunchUrl);
                Assert.Null(data.MediaProvider);
                Assert.Null(data.MediaCommand);
                Assert.Null(data.TerminalCommand);
                Assert.Null(data.TerminalShell);
                Assert.False(data.RunAsAdministrator);
                Assert.Equal(autoIcon, data.AutoIcon);
                Assert.Equal(FileActionMode.Paste, data.FileActionMode);
                break;
            case SnippetActionType.LaunchUrl:
                Assert.Equal(string.Empty, data.Content);
                Assert.Null(data.LaunchPath);
                Assert.Equal("https://example.com/docs", data.LaunchUrl);
                Assert.Null(data.MediaProvider);
                Assert.Null(data.MediaCommand);
                Assert.Null(data.TerminalCommand);
                Assert.Null(data.TerminalShell);
                Assert.False(data.RunAsAdministrator);
                Assert.Null(data.AutoIcon);
                Assert.Equal(FileActionMode.Launch, data.FileActionMode);
                break;
            case SnippetActionType.MediaAction:
                Assert.Equal(string.Empty, data.Content);
                Assert.Null(data.LaunchPath);
                Assert.Null(data.LaunchUrl);
                Assert.Equal(SnippetMediaProvider.Spotify, data.MediaProvider);
                Assert.Equal(SnippetMediaCommand.NextTrack, data.MediaCommand);
                Assert.Null(data.TerminalCommand);
                Assert.Null(data.TerminalShell);
                Assert.False(data.RunAsAdministrator);
                Assert.Null(data.AutoIcon);
                Assert.Equal(FileActionMode.Launch, data.FileActionMode);
                break;
            case SnippetActionType.TerminalCommand:
                Assert.Equal(string.Empty, data.Content);
                Assert.Null(data.LaunchPath);
                Assert.Null(data.LaunchUrl);
                Assert.Null(data.MediaProvider);
                Assert.Null(data.MediaCommand);
                Assert.Equal("echo hello", data.TerminalCommand);
                Assert.Equal(SnippetTerminalShell.PowerShell, data.TerminalShell);
                Assert.False(data.RunAsAdministrator);
                Assert.Null(data.AutoIcon);
                Assert.Equal(FileActionMode.Launch, data.FileActionMode);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null);
        }
    }
}
