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
}
