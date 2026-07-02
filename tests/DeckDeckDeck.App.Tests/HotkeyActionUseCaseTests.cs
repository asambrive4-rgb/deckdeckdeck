using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class HotkeyActionUseCaseTests
{
    [Fact]
    public void SaveHotkeyActionUseCaseSavesActionIndependentlyFromSnippetSlots()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var snippet = services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Slot Paste", "Hello", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new SaveHotkeyActionUseCase(services.HotkeyActionRepository, autoBackup);

        var result = useCase.Execute(CreateRequest(
            title: "Volume up",
            gesture: new HotkeyGesture(0x27, HotkeyModifiers.None),
            actionType: SnippetActionType.MediaAction,
            content: string.Empty,
            mediaCommand: SnippetMediaCommand.VolumeUp));

        Assert.True(result.Succeeded);
        Assert.Equal(1, autoBackup.RequestCount);
        var action = Assert.Single(services.HotkeyActionRepository.GetAll());
        Assert.Equal(result.HotkeyAction!.Id, action.Id);
        Assert.Equal("Volume up", action.Title);
        Assert.Equal(0x27, action.HotkeyVirtualKey);
        Assert.Equal(HotkeyModifiers.None, action.HotkeyModifiers);
        Assert.Equal(SnippetActionType.MediaAction, action.ActionType);
        Assert.Equal(SnippetMediaCommand.VolumeUp, action.MediaCommand);

        services.SnippetRepository.Delete(snippet.Id);

        Assert.Null(services.SnippetRepository.GetById(snippet.Id));
        Assert.NotNull(services.HotkeyActionRepository.GetById(action.Id));
    }

    [Fact]
    public void SaveHotkeyActionUseCaseRejectsEnabledActionWithoutHotkey()
    {
        var services = CreateServices();
        var useCase = new SaveHotkeyActionUseCase(services.HotkeyActionRepository);

        var result = useCase.Execute(CreateRequest(gesture: null));

        Assert.False(result.Succeeded);
        Assert.Equal(SaveHotkeyActionUseCase.HotkeyRequiredMessage, result.ErrorMessage);
        Assert.Empty(services.HotkeyActionRepository.GetAll());
    }

    [Fact]
    public void SaveHotkeyActionUseCaseRejectsModifierOnlyHotkey()
    {
        var services = CreateServices();
        var useCase = new SaveHotkeyActionUseCase(services.HotkeyActionRepository);

        var result = useCase.Execute(CreateRequest(
            gesture: new HotkeyGesture(Win32Constants.VkControl, HotkeyModifiers.Control)));

        Assert.False(result.Succeeded);
        Assert.Equal(SaveHotkeyActionUseCase.HotkeyModifierOnlyMessage, result.ErrorMessage);
        Assert.Empty(services.HotkeyActionRepository.GetAll());
    }

    [Fact]
    public void SaveHotkeyActionUseCaseAllowsDisabledActionWithoutHotkey()
    {
        var services = CreateServices();
        var useCase = new SaveHotkeyActionUseCase(services.HotkeyActionRepository);

        var result = useCase.Execute(CreateRequest(gesture: null, isEnabled: false));

        Assert.True(result.Succeeded);
        var action = Assert.Single(services.HotkeyActionRepository.GetAll());
        Assert.False(action.IsEnabled);
        Assert.Null(action.Gesture);
    }

    [Fact]
    public void SaveHotkeyActionUseCasePersistsFilePasteMode()
    {
        var services = CreateServices();
        var useCase = new SaveHotkeyActionUseCase(services.HotkeyActionRepository);

        var result = useCase.Execute(CreateRequest(
            title: "Paste notes",
            gesture: new HotkeyGesture(0x67, HotkeyModifiers.None),
            actionType: SnippetActionType.LaunchFile,
            content: string.Empty,
            launchPath: @"C:\notes\memo.md",
            fileActionMode: FileActionMode.Paste));

        Assert.True(result.Succeeded);
        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var action = Assert.Single(reloadedServices.HotkeyActionRepository.GetAll());
        Assert.Equal(FileActionMode.Paste, action.FileActionMode);
        Assert.Equal(@"C:\notes\memo.md", action.LaunchPath);
        Assert.Equal("파일 붙여넣기", HotkeyActionDisplayText.GetActionTypeLabel(action));
    }

    [Fact]
    public void SetHotkeyActionEnabledUseCaseDisablesActionAndRequestsBackup()
    {
        var services = CreateServices();
        var saveUseCase = new SaveHotkeyActionUseCase(services.HotkeyActionRepository);
        var saved = saveUseCase.Execute(CreateRequest(
            gesture: new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None))).HotkeyAction!;
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new SetHotkeyActionEnabledUseCase(services.HotkeyActionRepository, autoBackup);

        var result = useCase.Execute(saved.Id, isEnabled: false);

        Assert.True(result.Succeeded);
        Assert.False(result.HotkeyAction!.IsEnabled);
        Assert.False(services.HotkeyActionRepository.GetById(saved.Id)!.IsEnabled);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void SetHotkeyActionEnabledUseCaseRejectsEnablingActionWithoutHotkey()
    {
        var services = CreateServices();
        var saveUseCase = new SaveHotkeyActionUseCase(services.HotkeyActionRepository);
        var saved = saveUseCase.Execute(CreateRequest(gesture: null, isEnabled: false)).HotkeyAction!;
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new SetHotkeyActionEnabledUseCase(services.HotkeyActionRepository, autoBackup);

        var result = useCase.Execute(saved.Id, isEnabled: true);

        Assert.False(result.Succeeded);
        Assert.Equal(SaveHotkeyActionUseCase.HotkeyRequiredMessage, result.ErrorMessage);
        Assert.False(services.HotkeyActionRepository.GetById(saved.Id)!.IsEnabled);
        Assert.Equal(0, autoBackup.RequestCount);
    }

    [Fact]
    public void SetHotkeyActionEnabledUseCaseReturnsFailureForMissingAction()
    {
        var services = CreateServices();
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new SetHotkeyActionEnabledUseCase(services.HotkeyActionRepository, autoBackup);

        var result = useCase.Execute(Guid.NewGuid(), isEnabled: false);

        Assert.False(result.Succeeded);
        Assert.Equal(SetHotkeyActionEnabledUseCase.HotkeyNotFoundMessage, result.ErrorMessage);
        Assert.Equal(0, autoBackup.RequestCount);
    }

    [Fact]
    public void LoadDirectHotkeyRegistrationsUseCaseReturnsOnlyEnabledCompleteGestures()
    {
        var services = CreateServices();
        var enabled = services.HotkeyActionRepository.Create(CreateSaveData(
            "Enabled",
            new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None),
            isEnabled: true));
        services.HotkeyActionRepository.Create(CreateSaveData(
            "Disabled",
            new HotkeyGesture(Win32Constants.VkLeft, HotkeyModifiers.None),
            isEnabled: false));
        services.HotkeyActionRepository.Create(CreateSaveData(
            "Modifier only",
            new HotkeyGesture(Win32Constants.VkControl, HotkeyModifiers.Control),
            isEnabled: true));
        var useCase = new LoadDirectHotkeyRegistrationsUseCase(services.HotkeyActionRepository);

        var registrations = useCase.Execute();

        var registration = Assert.Single(registrations);
        Assert.Equal(enabled.Id, registration.HotkeyActionId);
        Assert.Equal(Win32Constants.VkRight, registration.Gesture.VirtualKey);
    }

    [Fact]
    public void ResolveExecutableHotkeyActionUseCaseReturnsOnlyEnabledAction()
    {
        var services = CreateServices();
        var enabled = services.HotkeyActionRepository.Create(CreateSaveData(
            "Enabled",
            new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None),
            isEnabled: true,
            content: "Run this"));
        var disabled = services.HotkeyActionRepository.Create(CreateSaveData(
            "Disabled",
            new HotkeyGesture(Win32Constants.VkLeft, HotkeyModifiers.None),
            isEnabled: false,
            content: "Skip this"));
        var useCase = new ResolveExecutableHotkeyActionUseCase(services.HotkeyActionRepository);

        var executable = useCase.Execute(enabled.Id);
        var skipped = useCase.Execute(disabled.Id);

        Assert.NotNull(executable);
        Assert.Equal("Run this", executable.Content);
        Assert.Null(skipped);
    }

    private static SaveHotkeyActionRequest CreateRequest(
        string title = "Paste",
        HotkeyGesture? gesture = null,
        bool isEnabled = true,
        SnippetActionType actionType = SnippetActionType.PasteText,
        string content = "Hello",
        SnippetMediaCommand mediaCommand = SnippetMediaCommand.PlayPause,
        string launchPath = "",
        FileActionMode fileActionMode = FileActionMode.Launch)
    {
        return new SaveHotkeyActionRequest(
            HotkeyActionId: null,
            Data: new HotkeyActionSaveData(
                title,
                gesture,
                isEnabled,
                content,
                Description: null,
                ImagePath: null,
                ThumbnailPath: null,
                actionType,
                LaunchPath: launchPath,
                SlotImageMode.Auto,
                AutoIcon: null,
                LaunchUrl: null,
                SnippetMediaProvider.System,
                mediaCommand,
                FileActionMode: fileActionMode));
    }

    private static HotkeyActionSaveData CreateSaveData(
        string title,
        HotkeyGesture gesture,
        bool isEnabled,
        string content = "Hello")
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
            SnippetMediaCommand.PlayPause);
    }
}
