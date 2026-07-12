using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.ViewModels;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class SlotLayoutUseCaseTests
{
    [Fact]
    public void MoveCategorySlotMovesToEmptyTargetAndRequestsBackup()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Writing", null);
        services.SnippetRepository.Create(source.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new MoveCategorySlotUseCase(
            services.CategoryRepository,
            services.SettingsRepository,
            services.ImageFileRepository,
            autoBackup);

        var result = useCase.Execute(new MoveCategorySlotRequest(
            SlotKey.Numpad4,
            SlotKey.Numpad5,
            OverwriteConfirmed: false));

        var moved = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5);
        Assert.True(result.Succeeded);
        Assert.Null(services.CategoryRepository.GetBySlotKey(SlotKey.Numpad4));
        Assert.Equal(source.Id, moved!.Id);
        Assert.Equal("Writing", moved.Name);
        Assert.Single(services.SnippetRepository.GetByCategoryId(source.Id));
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void MoveCategorySlotRequiresConfirmationWhenTargetOccupied()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad4, "Writing", null);
        services.CategoryRepository.Create(SlotKey.Numpad5, "Old", null);
        var useCase = new MoveCategorySlotUseCase(
            services.CategoryRepository,
            services.SettingsRepository);

        var result = useCase.Execute(new MoveCategorySlotRequest(
            SlotKey.Numpad4,
            SlotKey.Numpad5,
            OverwriteConfirmed: false));

        Assert.False(result.Succeeded);
        Assert.True(result.NeedsOverwriteConfirmation);
        Assert.Equal("Old", result.ExistingTargetName);
        Assert.Equal("Writing", services.CategoryRepository.GetBySlotKey(SlotKey.Numpad4)!.Name);
        Assert.Equal("Old", services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void MoveCategorySlotOverwritesTargetAfterConfirmation()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Writing", null);
        services.CategoryRepository.Create(
            SlotKey.Numpad5,
            "Old",
            null,
            "old-category.png",
            "old-category-thumbnail.png");
        var useCase = new MoveCategorySlotUseCase(
            services.CategoryRepository,
            services.SettingsRepository,
            services.ImageFileRepository);

        var result = useCase.Execute(new MoveCategorySlotRequest(
            SlotKey.Numpad4,
            SlotKey.Numpad5,
            OverwriteConfirmed: true));

        Assert.True(result.Succeeded);
        Assert.Null(services.CategoryRepository.GetBySlotKey(SlotKey.Numpad4));
        Assert.Equal(source.Id, services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Id);
    }

    [Fact]
    public void MoveCategorySlotRejectsDisabledSourceOrTarget()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad4, "Writing", null);
        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad4, false);
        var useCase = new MoveCategorySlotUseCase(
            services.CategoryRepository,
            services.SettingsRepository);

        var disabledSource = useCase.Execute(new MoveCategorySlotRequest(
            SlotKey.Numpad4,
            SlotKey.Numpad5,
            OverwriteConfirmed: false));

        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad4, true);
        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad5, false);
        var disabledTarget = useCase.Execute(new MoveCategorySlotRequest(
            SlotKey.Numpad4,
            SlotKey.Numpad5,
            OverwriteConfirmed: false));

        Assert.False(disabledSource.Succeeded);
        Assert.Contains("사용 안 함", disabledSource.ErrorMessage);
        Assert.False(disabledTarget.Succeeded);
        Assert.Contains("사용 안 함", disabledTarget.ErrorMessage);
    }

    [Fact]
    public void MoveSnippetSlotMovesToEmptyTargetAndRequestsBackup()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        var source = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Paste",
            "Hello",
            null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new MoveSnippetSlotUseCase(
            services.SnippetRepository,
            services.SettingsRepository,
            services.ImageFileRepository,
            autoBackup);

        var result = useCase.Execute(new MoveSnippetSlotRequest(
            category.Id,
            SlotKey.Numpad3,
            SlotKey.Numpad7,
            OverwriteConfirmed: false));

        Assert.True(result.Succeeded);
        Assert.Null(services.SnippetRepository.GetByCategoryId(category.Id)
            .FirstOrDefault(snippet => snippet.SlotKey == SlotKey.Numpad3));
        Assert.Equal(source.Id, services.SnippetRepository.GetByCategoryId(category.Id)
            .Single(snippet => snippet.SlotKey == SlotKey.Numpad7).Id);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void MoveSnippetSlotRequiresConfirmationWhenTargetOccupied()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Paste", "Hello", null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad7, "Old", "Bye", null);
        var useCase = new MoveSnippetSlotUseCase(
            services.SnippetRepository,
            services.SettingsRepository);

        var result = useCase.Execute(new MoveSnippetSlotRequest(
            category.Id,
            SlotKey.Numpad3,
            SlotKey.Numpad7,
            OverwriteConfirmed: false));

        Assert.False(result.Succeeded);
        Assert.True(result.NeedsOverwriteConfirmation);
        Assert.Equal("Old", result.ExistingTargetName);
    }

    [Fact]
    public void MoveSnippetSlotOverwritesTargetAfterConfirmation()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        var source = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Paste",
            "Hello",
            null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad7, "Old", "Bye", null);
        var useCase = new MoveSnippetSlotUseCase(
            services.SnippetRepository,
            services.SettingsRepository);

        var result = useCase.Execute(new MoveSnippetSlotRequest(
            category.Id,
            SlotKey.Numpad3,
            SlotKey.Numpad7,
            OverwriteConfirmed: true));

        var remaining = services.SnippetRepository.GetByCategoryId(category.Id);
        Assert.True(result.Succeeded);
        Assert.Single(remaining);
        Assert.Equal(source.Id, remaining[0].Id);
        Assert.Equal(SlotKey.Numpad7, remaining[0].SlotKey);
    }

    [Fact]
    public void SlotViewModelCanStartDragOnlyWhenFilledAndEnabled()
    {
        var filledEnabled = new SlotViewModel(
            SlotKey.Numpad1,
            "Writing",
            isEnabledSlot: true,
            _ => { });
        var emptyEnabled = new SlotViewModel(
            SlotKey.Numpad2,
            title: null,
            isEnabledSlot: true,
            _ => { });
        var filledDisabled = new SlotViewModel(
            SlotKey.Numpad3,
            "Disabled",
            isEnabledSlot: false,
            _ => { });

        Assert.True(filledEnabled.CanStartDrag);
        Assert.True(filledEnabled.CanAcceptDrop);
        Assert.False(emptyEnabled.CanStartDrag);
        Assert.True(emptyEnabled.CanAcceptDrop);
        Assert.False(filledDisabled.CanStartDrag);
        Assert.False(filledDisabled.CanAcceptDrop);
    }

    [Fact]
    public void SlotViewModelSuppressNextSelectSkipsOneSelection()
    {
        var selected = 0;
        var slot = new SlotViewModel(
            SlotKey.Numpad1,
            "Writing",
            isEnabledSlot: true,
            _ => selected++);

        slot.SuppressNextSelect();
        slot.SelectCommand.Execute(null);
        slot.SelectCommand.Execute(null);

        Assert.Equal(1, selected);
    }
}
