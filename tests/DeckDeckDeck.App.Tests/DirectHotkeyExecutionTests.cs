using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class DirectHotkeyExecutionTests
{
    [Fact]
    public async Task DirectHotkeyExecutesStoredActionWithoutNavigating()
    {
        var services = CreateServices();
        var hotkey = services.HotkeyActionRepository.Create(CreatePasteAction(
            "Direct Paste",
            new HotkeyGesture(0x67, HotkeyModifiers.None),
            isEnabled: true,
            content: "Paste from hotkey"));
        var pasteService = new RecordingClipboardPasteGateway();
        var hidden = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(456),
            () => hidden = true);

        var registrations = viewModel.LoadActiveDirectHotkeys();
        await viewModel.ExecuteDirectHotkeyAsync(hotkey.Id);

        var registration = Assert.Single(registrations);
        Assert.Equal(hotkey.Id, registration.HotkeyActionId);
        Assert.Equal(0x67u, registration.Gesture.VirtualKey);
        var call = Assert.Single(pasteService.Calls);
        Assert.Equal("Paste from hotkey", call.Action.Content);
        Assert.Equal(new IntPtr(456), call.TargetWindowHandle);
        Assert.True(hidden);
        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public async Task DisabledDirectHotkeyDoesNotRegisterOrExecute()
    {
        var services = CreateServices();
        var hotkey = services.HotkeyActionRepository.Create(CreatePasteAction(
            "Disabled Paste",
            new HotkeyGesture(0x67, HotkeyModifiers.None),
            isEnabled: false,
            content: "Do not paste"));
        var pasteService = new RecordingClipboardPasteGateway();
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(456),
            () => { });

        var registrations = viewModel.LoadActiveDirectHotkeys();
        await viewModel.ExecuteDirectHotkeyAsync(hotkey.Id);

        Assert.Empty(registrations);
        Assert.Empty(pasteService.Calls);
    }

    [Fact]
    public async Task DirectHotkeyIgnoresSecondActionWhileFirstActionIsRunning()
    {
        var services = CreateServices();
        var firstHotkey = services.HotkeyActionRepository.Create(CreatePasteAction(
            "First Direct Paste",
            new HotkeyGesture(0x67, HotkeyModifiers.None),
            isEnabled: true,
            content: "First paste"));
        var secondHotkey = services.HotkeyActionRepository.Create(CreatePasteAction(
            "Second Direct Paste",
            new HotkeyGesture(0x68, HotkeyModifiers.None),
            isEnabled: true,
            content: "Second paste"));
        var pasteService = new BlockingClipboardPasteGateway();
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(456),
            () => { });

        var firstTask = viewModel.ExecuteDirectHotkeyAsync(firstHotkey.Id);
        await pasteService.Started;

        await viewModel.ExecuteDirectHotkeyAsync(secondHotkey.Id);

        var call = Assert.Single(pasteService.Calls);
        Assert.Equal("First paste", call.Action.Content);
        Assert.Equal("Action is already running.", viewModel.StatusMessage);

        pasteService.Complete();
        await firstTask;
    }

    private static HotkeyActionSaveData CreatePasteAction(
        string title,
        HotkeyGesture gesture,
        bool isEnabled,
        string content)
    {
        return new HotkeyActionSaveData(
            title,
            gesture,
            isEnabled,
            content,
            Description: null,
            ImagePath: null,
            ThumbnailPath: null,
            SnippetActionType.PasteText,
            LaunchPath: string.Empty,
            SlotImageMode.Auto,
            AutoIcon: null,
            LaunchUrl: null,
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause,
            PasteShortcutMode.CtrlV,
            TerminalCommand: string.Empty,
            SnippetTerminalShell.Cmd,
            RunAsAdministrator: false);
    }
}
