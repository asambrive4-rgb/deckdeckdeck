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
}
