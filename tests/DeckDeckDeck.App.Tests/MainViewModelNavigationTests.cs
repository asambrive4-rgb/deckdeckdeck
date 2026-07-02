using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
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
    public void MainViewModelLoadsHomeOnlyAfterExplicitInitialization()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services, initializeHome: false);

        Assert.Null(viewModel.CurrentViewModel);

        viewModel.InitializeHome();

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void HomeHotkeyAlwaysReturnsToHome()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        viewModel.OpenHomeFromHotkey();

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void HomeHotkeyDoesNotRebuildHomeWhenAlreadyVisible()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        viewModel.OpenHomeFromHotkey();

        Assert.Same(home, viewModel.CurrentViewModel);
    }

    [Fact]
    public void DirectCategoryHotkeyOpensExistingCategory()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing 카테고리", viewModel.StatusMessage);
    }

    [Fact]
    public void DirectCategoryHotkeyDoesNotRebuildCurrentCategory()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var category = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        Assert.Same(category, viewModel.CurrentViewModel);
    }

    [Fact]
    public void DirectCategoryHotkeyOpensExistingSymbolCategory()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.NumpadAdd, "Symbols", null);
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
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad1, false);
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
    public void HomeHotkeyTileOpensHotkeyList()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        Assert.True(home.NumpadGrid.HotkeyTile.IsEnabled);
        home.NumpadGrid.HotkeyTile.SelectCommand.Execute(null);

        var list = Assert.IsType<HotkeyListViewModel>(viewModel.CurrentViewModel);
        Assert.True(list.IsEmpty);
        Assert.True(viewModel.ShowTopBarBackButton);
        Assert.Same(list.BackCommand, viewModel.TopBarBackCommand);
    }

    [Fact]
    public void CategoryHotkeyTileIsDisabled()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var category = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);

        Assert.False(category.NumpadGrid.HotkeyTile.IsEnabled);
        Assert.False(category.NumpadGrid.HotkeyTile.SelectCommand.CanExecute(null));
    }

    [Fact]
    public void HomeTopBarShowsSettingsWithoutTitleOrBack()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        Assert.Equal(string.Empty, viewModel.TopBarTitle);
        Assert.Equal("준비됨", viewModel.TopBarStatusMessage);
        Assert.False(viewModel.ShowTopBarTitle);
        Assert.False(viewModel.ShowTopBarBackButton);
        Assert.True(viewModel.ShowTopBarSettingsButton);
        Assert.Null(viewModel.TopBarBackCommand);
        Assert.Same(home.SettingsCommand, viewModel.TopBarSettingsCommand);
    }

    [Fact]
    public void CategoryTopBarShowsCategoryNameBackAndSettings()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var category = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);

        Assert.Equal("Writing", viewModel.TopBarTitle);
        Assert.True(viewModel.ShowTopBarTitle);
        Assert.True(viewModel.ShowTopBarBackButton);
        Assert.True(viewModel.ShowTopBarSettingsButton);
        Assert.Same(category.BackCommand, viewModel.TopBarBackCommand);
        Assert.Same(category.SettingsCommand, viewModel.TopBarSettingsCommand);
    }

    [Fact]
    public void SettingsTopBarShowsTitleAndBackWithoutSettings()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);

        viewModel.TopBarSettingsCommand!.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(viewModel.CurrentViewModel);

        Assert.Equal("설정", viewModel.TopBarTitle);
        Assert.True(viewModel.ShowTopBarTitle);
        Assert.True(viewModel.ShowTopBarBackButton);
        Assert.False(viewModel.ShowTopBarSettingsButton);
        Assert.Same(settings.BackCommand, viewModel.TopBarBackCommand);
        Assert.Null(viewModel.TopBarSettingsCommand);
    }

    [Fact]
    public void CategoryEditorTopBarShowsSlotTitleAndBackWithoutSettings()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        home.NumpadGrid.Numpad2.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);

        Assert.Equal("카테고리 편집 / 슬롯 2", viewModel.TopBarTitle);
        Assert.True(viewModel.ShowTopBarTitle);
        Assert.True(viewModel.ShowTopBarBackButton);
        Assert.False(viewModel.ShowTopBarSettingsButton);
        Assert.Same(editor.CancelCommand, viewModel.TopBarBackCommand);
        Assert.Null(viewModel.TopBarSettingsCommand);
    }

    [Fact]
    public void SnippetEditorTopBarShowsSlotTitleAndBackWithoutSettings()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var category = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        category.NumpadGrid.Numpad3.EditCommand.Execute(null);
        var editor = Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);

        Assert.Equal("실행 항목 편집 / 슬롯 3", viewModel.TopBarTitle);
        Assert.True(viewModel.ShowTopBarTitle);
        Assert.True(viewModel.ShowTopBarBackButton);
        Assert.False(viewModel.ShowTopBarSettingsButton);
        Assert.Same(editor.CancelCommand, viewModel.TopBarBackCommand);
        Assert.Null(viewModel.TopBarSettingsCommand);
    }

    [Fact]
    public void HomeSlotEditCommandOpensCategoryEditor()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
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
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
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
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        home.NumpadGrid.Numpad1.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        editor.Name = "Writing Updated";

        editor.SaveCommand.Execute(null);

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing Updated", Assert.Single(services.CategoryRepository.GetAll()).Name);
    }

    [Fact]
    public void CategorySlotEditCommandOpensSnippetEditor()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
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
        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad2, false);
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

        var settings = services.SettingsRepository.Load();
        Assert.False(settings.EnabledCategorySlotKeys[SlotKey.Numpad2]);
        Assert.True(settings.EnabledSnippetSlotKeys[SlotKey.Numpad2]);
        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void EmptySnippetSlotEditorCanSaveSlotEnabledOnly()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateMainViewModel(services, autoBackupCoordinator: autoBackup);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var categoryViewModel = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        categoryViewModel.NumpadGrid.Numpad4.EditCommand.Execute(null);
        var editor = Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        editor.IsSlotEnabled = false;
        editor.SaveCommand.Execute(null);

        var settings = services.SettingsRepository.Load();
        Assert.True(settings.EnabledCategorySlotKeys[SlotKey.Numpad4]);
        Assert.False(settings.EnabledSnippetSlotKeys[SlotKey.Numpad4]);
        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void DisabledCategorySlotDoesNotDisableSameSnippetSlot()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad4, false);
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
        services.CategoryRepository.Create(SlotKey.Numpad4, "Writing", null);
        services.SettingsRepository.SetSnippetSlotEnabled(SlotKey.Numpad4, false);
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

        var settings = services.SettingsRepository.Load();
        Assert.Equal(10, settings.LastWindowLeft);
        Assert.Equal(20, settings.LastWindowTop);
        Assert.Equal("Monitor1", settings.LastWindowScreenDeviceName);
        Assert.Equal(0, autoBackup.RequestCount);
    }
}

