using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class MainViewModelPasteTests
{
    [Fact]
    public void ExistingSnippetSlotPastesInsteadOfOpeningEditor()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        var pasteService = new RecordingClipboardPasteService();
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        var call = Assert.Single(pasteService.Calls);
        Assert.Equal("Make this clearer.", call.Snippet.Content);
        Assert.Equal(new IntPtr(123), call.TargetWindowHandle);
        Assert.True(hidden);
        Assert.True(completedPasteSelection);
    }

    [Fact]
    public void EmptySnippetSlotStillOpensEditor()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var pasteService = new RecordingClipboardPasteService();
        var enteredEditMode = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(123),
            () => { },
            enterEditMode: () => enteredEditMode = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad4);

        Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        Assert.Empty(pasteService.Calls);
        Assert.True(enteredEditMode);
    }

    [Fact]
    public void PasteSelectionIsCompletedWhenPasteServiceThrows()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            new ThrowingClipboardPasteService(),
            () => new IntPtr(123),
            () => { },
            completePasteSelection: () => completedPasteSelection = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.True(completedPasteSelection);
    }

    [Fact]
    public void LaunchFileSnippetLaunchesInsteadOfPasting()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open notes",
            string.Empty,
            null,
            actionType: SnippetActionType.LaunchFile,
            launchPath: @"C:\notes");
        var pasteService = new RecordingClipboardPasteService();
        var launchService = new RecordingFileLaunchService();
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true,
            fileLaunchService: launchService);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.Empty(pasteService.Calls);
        Assert.Equal([@"C:\notes"], launchService.Paths);
        Assert.True(hidden);
        Assert.True(completedPasteSelection);
    }

    [Fact]
    public void LaunchFileFailureShowsStatusAndKeepsWindowVisible()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Missing file",
            string.Empty,
            null,
            actionType: SnippetActionType.LaunchFile,
            launchPath: @"C:\missing.exe");
        var launchService = new RecordingFileLaunchService { Result = false };
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            new RecordingClipboardPasteService(),
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true,
            fileLaunchService: launchService);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.False(hidden);
        Assert.True(completedPasteSelection);
        Assert.Contains("실행 실패", viewModel.StatusMessage);
        Assert.Contains("Launch failed", File.ReadAllText(Path.Combine(services.Storage.LogsPath, "app.log")));
    }

    [Fact]
    public void LaunchUrlSnippetLaunchesInsteadOfPasting()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Web", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open docs",
            string.Empty,
            null,
            actionType: SnippetActionType.LaunchUrl,
            launchUrl: "https://example.com/docs");
        var pasteService = new RecordingClipboardPasteService();
        var urlLaunchService = new RecordingUrlLaunchService();
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true,
            urlLaunchService: urlLaunchService);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.Empty(pasteService.Calls);
        Assert.Equal(["https://example.com/docs"], urlLaunchService.Urls);
        Assert.True(hidden);
        Assert.True(completedPasteSelection);
    }

    [Fact]
    public void MediaActionSnippetExecutesInsteadOfPasting()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Media", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Next track",
            string.Empty,
            null,
            actionType: SnippetActionType.MediaAction,
            mediaProvider: SnippetMediaProvider.System,
            mediaCommand: SnippetMediaCommand.NextTrack);
        var pasteService = new RecordingClipboardPasteService();
        var mediaActionService = new RecordingMediaActionService();
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true,
            mediaActionService: mediaActionService);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.Empty(pasteService.Calls);
        Assert.Equal([SnippetMediaCommand.NextTrack], mediaActionService.Commands);
        Assert.True(hidden);
        Assert.True(completedPasteSelection);
    }

    [Fact]
    public void MediaActionFailureShowsStatusAndKeepsWindowVisible()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Media", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Mute",
            string.Empty,
            null,
            actionType: SnippetActionType.MediaAction,
            mediaProvider: SnippetMediaProvider.System,
            mediaCommand: SnippetMediaCommand.Mute);
        var mediaActionService = new RecordingMediaActionService { Result = false };
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            new RecordingClipboardPasteService(),
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true,
            mediaActionService: mediaActionService);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.False(hidden);
        Assert.True(completedPasteSelection);
        Assert.Contains("미디어 제어 실패", viewModel.StatusMessage);
        Assert.Contains("Media action failed", File.ReadAllText(Path.Combine(services.Storage.LogsPath, "app.log")));
    }

    [Fact]
    public void LaunchUrlFailureShowsStatusAndKeepsWindowVisible()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Web", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Broken site",
            string.Empty,
            null,
            actionType: SnippetActionType.LaunchUrl,
            launchUrl: "https://example.com");
        var urlLaunchService = new RecordingUrlLaunchService { Result = false };
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            new RecordingClipboardPasteService(),
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true,
            urlLaunchService: urlLaunchService);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.False(hidden);
        Assert.True(completedPasteSelection);
        Assert.Contains("웹 주소 열기 실패", viewModel.StatusMessage);
        Assert.Contains("Launch URL failed", File.ReadAllText(Path.Combine(services.Storage.LogsPath, "app.log")));
    }

    [Fact]
    public void EmptyLaunchUrlShowsStatusAndDoesNotCallLauncher()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Web", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Empty site",
            string.Empty,
            null,
            actionType: SnippetActionType.LaunchUrl);
        var urlLaunchService = new RecordingUrlLaunchService();
        var hidden = false;
        var viewModel = CreateMainViewModel(
            services,
            new RecordingClipboardPasteService(),
            () => new IntPtr(123),
            () => hidden = true,
            urlLaunchService: urlLaunchService);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.False(hidden);
        Assert.Empty(urlLaunchService.Urls);
        Assert.Contains("웹 주소 열기 실패", viewModel.StatusMessage);
    }
}
