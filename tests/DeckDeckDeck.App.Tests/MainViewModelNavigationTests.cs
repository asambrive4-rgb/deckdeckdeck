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
public sealed class MainViewModelNavigationTests
{
    [Fact]
    public void HomeHotkeyAlwaysReturnsToHome()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        viewModel.OpenHomeFromHotkey();

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void DirectCategoryHotkeyOpensExistingCategory()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing 카테고리", viewModel.StatusMessage);
    }

    [Fact]
    public void DirectCategoryHotkeyOpensExistingSymbolCategory()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.NumpadAdd, "Symbols", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.NumpadAdd);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Symbols 카테고리", viewModel.StatusMessage);
    }

    [Fact]
    public void DirectCategoryHotkeyOpensCategoryEditorForEmptySlot()
    {
        var services = CreateServices();
        var enteredEditMode = false;
        var viewModel = CreateMainViewModel(
            services,
            enterEditMode: () => enteredEditMode = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad2);

        Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("슬롯 2에 새 카테고리 만들기", viewModel.StatusMessage);
        Assert.True(enteredEditMode);
    }

    [Fact]
    public void DirectCategoryHotkeyDoesNotNavigateWhenSlotIsDisabled()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SettingsService.SetCategorySlotEnabled(SlotKey.Numpad1, false);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("슬롯 1은 사용 안 함 상태입니다.", viewModel.StatusMessage);
    }

    [Fact]
    public void HomeSettingsCommandOpensSettingsViewModel()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        home.SettingsCommand.Execute(null);

        Assert.IsType<SettingsViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("설정", viewModel.StatusMessage);
    }

    [Fact]
    public void HomeSlotEditCommandOpensCategoryEditor()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        home.NumpadGrid.Numpad1.EditCommand.Execute(null);

        Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing 편집", viewModel.StatusMessage);
    }

    [Fact]
    public void CategoryEditorCancelReturnsHome()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        home.NumpadGrid.Numpad1.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);

        editor.CancelCommand.Execute(null);

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void CategoryEditorSaveReturnsHome()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        home.NumpadGrid.Numpad1.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        editor.Name = "Writing Updated";

        editor.SaveCommand.Execute(null);

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing Updated", Assert.Single(services.CategoryService.GetAll()).Name);
    }

    [Fact]
    public void CategorySlotEditCommandOpensSnippetEditor()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var categoryViewModel = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        categoryViewModel.NumpadGrid.Numpad3.EditCommand.Execute(null);

        Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Structure 편집", viewModel.StatusMessage);
    }

    [Fact]
    public void DisabledSlotEditCommandStillOpensCategoryEditor()
    {
        var services = CreateServices();
        services.SettingsService.SetCategorySlotEnabled(SlotKey.Numpad2, false);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        Assert.False(home.NumpadGrid.Numpad2.SelectCommand.CanExecute(null));
        home.NumpadGrid.Numpad2.EditCommand.Execute(null);

        Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("슬롯 2에 새 카테고리 만들기", viewModel.StatusMessage);
    }

    [Fact]
    public void EmptyCategorySlotEditorCanSaveSlotEnabledOnly()
    {
        var services = CreateServices();
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateMainViewModel(services, autoBackupCoordinator: autoBackup);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        home.NumpadGrid.Numpad2.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        editor.IsSlotEnabled = false;
        editor.SaveCommand.Execute(null);

        var settings = services.SettingsService.Load();
        Assert.False(settings.EnabledCategorySlotKeys[SlotKey.Numpad2]);
        Assert.True(settings.EnabledSnippetSlotKeys[SlotKey.Numpad2]);
        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void EmptySnippetSlotEditorCanSaveSlotEnabledOnly()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateMainViewModel(services, autoBackupCoordinator: autoBackup);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var categoryViewModel = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        categoryViewModel.NumpadGrid.Numpad4.EditCommand.Execute(null);
        var editor = Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        editor.IsSlotEnabled = false;
        editor.SaveCommand.Execute(null);

        var settings = services.SettingsService.Load();
        Assert.True(settings.EnabledCategorySlotKeys[SlotKey.Numpad4]);
        Assert.False(settings.EnabledSnippetSlotKeys[SlotKey.Numpad4]);
        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void DisabledCategorySlotDoesNotDisableSameSnippetSlot()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SettingsService.SetCategorySlotEnabled(SlotKey.Numpad4, false);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var categoryViewModel = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);

        Assert.True(categoryViewModel.NumpadGrid.Numpad4.SelectCommand.CanExecute(null));
        viewModel.SelectSlot(SlotKey.Numpad4);

        Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void DisabledSnippetSlotDoesNotDisableSameCategorySlot()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad4, "Writing", null);
        services.SettingsService.SetSnippetSlotEnabled(SlotKey.Numpad4, false);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad4);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing 카테고리", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveWindowPlacementDoesNotRequestAutoBackup()
    {
        var services = CreateServices();
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateMainViewModel(services, autoBackupCoordinator: autoBackup);

        viewModel.SaveWindowPlacement(10, 20, "Monitor1");

        var settings = services.SettingsService.Load();
        Assert.Equal(10, settings.LastWindowLeft);
        Assert.Equal(20, settings.LastWindowTop);
        Assert.Equal("Monitor1", settings.LastWindowScreenDeviceName);
        Assert.Equal(0, autoBackup.RequestCount);
    }
}
