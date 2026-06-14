using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.ViewModels;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class CategoryEditViewModelTests
{
    [Fact]
    public void CopyCategoryOverwritesTargetAfterConfirmAndCopiesSlotEnabled()
    {
        var services = CreateServices();
        var source = services.CategoryService.Create(SlotKey.Numpad4, "Writing", null);
        services.SnippetService.Create(source.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        services.CategoryService.Create(SlotKey.Numpad5, "Old", null);
        services.SettingsService.SetCategorySlotEnabled(SlotKey.Numpad4, false);
        var dialogService = new StubDialogService();
        var statusMessages = new List<string>();
        var viewModel = CreateViewModel(services, source, dialogService, statusMessages.Add);
        viewModel.SelectedTransferTarget = GetTargetSlot(viewModel, SlotKey.Numpad5);

        viewModel.CopyCategoryCommand.Execute(null);

        var copiedCategory = services.CategoryService.GetBySlotKey(SlotKey.Numpad5);
        var copiedSnippet = Assert.Single(services.SnippetService.GetByCategoryId(copiedCategory!.Id));
        var settings = services.SettingsService.Load();
        Assert.Equal(1, dialogService.ConfirmCount);
        Assert.Equal("Writing", copiedCategory.Name);
        Assert.Equal("Structure", copiedSnippet.Title);
        Assert.False(settings.EnabledCategorySlotKeys[SlotKey.Numpad4]);
        Assert.False(settings.EnabledCategorySlotKeys[SlotKey.Numpad5]);
        Assert.Equal("슬롯 5에 카테고리를 복사했습니다.", statusMessages.Last());
    }

    [Fact]
    public void MoveCategoryMovesSlotEnabledAndResetsSourceSlot()
    {
        var services = CreateServices();
        var source = services.CategoryService.Create(SlotKey.Numpad4, "Writing", null);
        services.SnippetService.Create(source.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        services.SettingsService.SetCategorySlotEnabled(SlotKey.Numpad4, false);
        var statusMessages = new List<string>();
        var viewModel = CreateViewModel(services, source, new StubDialogService(), statusMessages.Add);
        viewModel.SelectedTransferTarget = GetTargetSlot(viewModel, SlotKey.Numpad5);

        viewModel.MoveCategoryCommand.Execute(null);

        var movedCategory = services.CategoryService.GetBySlotKey(SlotKey.Numpad5);
        var settings = services.SettingsService.Load();
        Assert.Null(services.CategoryService.GetBySlotKey(SlotKey.Numpad4));
        Assert.Equal(source.Id, movedCategory!.Id);
        Assert.False(settings.EnabledCategorySlotKeys[SlotKey.Numpad5]);
        Assert.True(settings.EnabledCategorySlotKeys[SlotKey.Numpad4]);
        Assert.Equal("슬롯 5로 카테고리를 이동했습니다.", statusMessages.Last());
    }

    [Fact]
    public void TransferCancelDoesNotOverwriteTarget()
    {
        var services = CreateServices();
        var source = services.CategoryService.Create(SlotKey.Numpad4, "Writing", null);
        services.CategoryService.Create(SlotKey.Numpad5, "Old", null);
        var dialogService = new StubDialogService { ConfirmResult = false };
        var viewModel = CreateViewModel(services, source, dialogService, _ => { });
        viewModel.SelectedTransferTarget = GetTargetSlot(viewModel, SlotKey.Numpad5);

        viewModel.MoveCategoryCommand.Execute(null);

        Assert.Equal(1, dialogService.ConfirmCount);
        Assert.Equal("Writing", services.CategoryService.GetBySlotKey(SlotKey.Numpad4)!.Name);
        Assert.Equal("Old", services.CategoryService.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void SaveRequestsAutoBackupAfterCategoryChange()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad4, "Writing", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            new StubDialogService(),
            _ => { },
            autoBackup);
        viewModel.Name = "Writing Updated";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Writing Updated", services.CategoryService.GetById(category.Id)!.Name);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void DeleteRequestsAutoBackupAfterCategoryDelete()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad4, "Writing", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            new StubDialogService(),
            _ => { },
            autoBackup);

        viewModel.DeleteCommand.Execute(null);

        Assert.Null(services.CategoryService.GetById(category.Id));
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void DroppingImageFileUpdatesThumbnailPreview()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad4, "Writing", null);
        var sourcePath = CreateTinyBmp(services.Storage.TempPath);
        var viewModel = CreateViewModel(services, category, new StubDialogService(), _ => { });

        var thumbnailPath = RunInSta(() =>
        {
            viewModel.DropImageFiles([sourcePath]);
            return viewModel.ThumbnailPath;
        });

        Assert.True(viewModel.HasImage);
        Assert.NotNull(thumbnailPath);
        Assert.StartsWith("images/thumbnails/", thumbnailPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(services.Storage.ToAbsolutePath(thumbnailPath)));
    }

    private static CategoryEditViewModel CreateViewModel(
        TestServices services,
        Category category,
        DialogService dialogService,
        Action<string> showStatus,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        var saveCategoryUseCase = new SaveCategoryUseCase(
            services.CategoryService,
            services.SettingsService,
            autoBackupCoordinator);
        var deleteCategoryUseCase = new DeleteCategoryUseCase(
            services.CategoryService,
            services.ThumbnailService,
            autoBackupCoordinator);
        var transferCategoryUseCase = new TransferCategoryUseCase(
            services.CategoryService,
            services.SettingsService,
            saveCategoryUseCase,
            services.ThumbnailService,
            autoBackupCoordinator);

        return new CategoryEditViewModel(
            category.SlotKey,
            category,
            new LoadCategoryEditorStateUseCase(
                services.CategoryService,
                services.SettingsService)
                .Execute(new LoadCategoryEditorStateRequest(category.SlotKey, category.Id)),
            saveCategoryUseCase,
            deleteCategoryUseCase,
            transferCategoryUseCase,
            dialogService,
            () => { },
            _ => { },
            () => { },
            showStatus,
            services.ThumbnailService,
            services.LoggingService);
    }

    private static CategoryTransferTargetSlot GetTargetSlot(
        CategoryEditViewModel viewModel,
        SlotKey slotKey)
    {
        return viewModel.TransferTargetSlots.First(targetSlot => targetSlot.SlotKey == slotKey);
    }

    private sealed class StubDialogService : DialogService
    {
        public bool ConfirmResult { get; init; } = true;

        public int ConfirmCount { get; private set; }

        public override bool Confirm(string title, string message)
        {
            ConfirmCount++;
            return ConfirmResult;
        }
    }
}
