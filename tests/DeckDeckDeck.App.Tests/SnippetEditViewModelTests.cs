using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.ViewModels;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class SnippetEditViewModelTests
{
    [Fact]
    public void PasteTextRequiresContent()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Paste";
        viewModel.Content = string.Empty;
        viewModel.SaveCommand.Execute(null);

        Assert.Null(savedSnippet);
        Assert.Equal("붙여넣을 문구를 입력해 주세요.", viewModel.ErrorMessage);
    }

    [Fact]
    public void PasteShortcutModeSavesWithPasteTextSnippet()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Terminal", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Terminal paste";
        viewModel.Content = "Run this";
        viewModel.PasteShortcutMode = PasteShortcutMode.CtrlShiftV;
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(PasteShortcutMode.CtrlShiftV, savedSnippet.PasteShortcutMode);
    }

    [Fact]
    public void LaunchFileRequiresLaunchPath()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Open";
        viewModel.IsLaunchFileAction = true;
        viewModel.SaveCommand.Execute(null);

        Assert.Null(savedSnippet);
        Assert.Equal("실행할 파일, 폴더 또는 바로 가기를 선택해 주세요.", viewModel.ErrorMessage);
    }

    [Fact]
    public void LaunchUrlRequiresValidUrl()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Web", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Open site";
        viewModel.IsLaunchUrlAction = true;
        viewModel.SaveCommand.Execute(null);

        Assert.Null(savedSnippet);
        Assert.Equal("열 웹페이지 주소를 http 또는 https 주소로 입력해 주세요.", viewModel.ErrorMessage);
    }

    [Fact]
    public void TerminalCommandRequiresCommand()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Reconnect Bluetooth";
        viewModel.IsTerminalCommandAction = true;
        viewModel.SaveCommand.Execute(null);

        Assert.Null(savedSnippet);
        Assert.Equal("실행할 터미널 명령을 입력해 주세요.", viewModel.ErrorMessage);
    }

    [Fact]
    public void LaunchFileSavesWithLaunchPathAndEmptyContent()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Open notes";
        viewModel.Content = "unused";
        viewModel.IsLaunchFileAction = true;
        viewModel.LaunchPath = @"C:\notes";
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SnippetActionType.LaunchFile, savedSnippet.ActionType);
        Assert.Equal(string.Empty, savedSnippet.Content);
        Assert.Equal(@"C:\notes", savedSnippet.LaunchPath);
    }

    [Fact]
    public void FilePasteModeSavesPathAndUpdatesFileControls()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        Snippet? savedSnippet = null;
        var dialogService = new StubDialogAdapter
        {
            PasteFile = @"C:\notes\memo.md"
        };
        var viewModel = CreateViewModel(
            services,
            category,
            snippet => savedSnippet = snippet,
            dialogService);

        viewModel.SnippetTitle = "Paste notes";
        viewModel.IsLaunchFileAction = true;
        viewModel.SelectedFileActionMode = FileActionMode.Paste;
        viewModel.ChooseLaunchFileCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(FileActionMode.Paste, savedSnippet.FileActionMode);
        Assert.Equal(@"C:\notes\memo.md", savedSnippet.LaunchPath);
        Assert.True(viewModel.IsFilePasteMode);
        Assert.False(viewModel.IsFileLaunchMode);
        Assert.Equal("붙여넣을 파일", viewModel.FilePathLabel);
        Assert.Equal("파일 선택", viewModel.FilePickerButtonText);
    }

    [Fact]
    public void LaunchUrlSavesWithNormalizedUrlAndEmptyContent()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Web", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Open docs";
        viewModel.Content = "unused";
        viewModel.LaunchPath = @"C:\unused";
        viewModel.IsLaunchUrlAction = true;
        viewModel.LaunchUrl = "example.com/docs";
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SnippetActionType.LaunchUrl, savedSnippet.ActionType);
        Assert.Equal(string.Empty, savedSnippet.Content);
        Assert.Null(savedSnippet.LaunchPath);
        Assert.Equal("https://example.com/docs", savedSnippet.LaunchUrl);
        Assert.Equal("https://example.com/docs", viewModel.LaunchUrl);
    }

    [Fact]
    public void MediaActionSavesWithSelectedCommandAndDefaultIcon()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Volume down";
        viewModel.Content = "unused";
        viewModel.LaunchPath = @"C:\unused";
        viewModel.LaunchUrl = "https://example.com";
        viewModel.IsMediaAction = true;
        viewModel.SelectedMediaCommand = SnippetMediaCommand.VolumeDown;

        Assert.Equal(MediaIconResources.GetIconResourcePath(SnippetMediaCommand.VolumeDown), viewModel.ThumbnailPath);

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SnippetActionType.MediaAction, savedSnippet.ActionType);
        Assert.Equal(string.Empty, savedSnippet.Content);
        Assert.Null(savedSnippet.LaunchPath);
        Assert.Null(savedSnippet.LaunchUrl);
        Assert.Equal(SnippetMediaProvider.System, savedSnippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.VolumeDown, savedSnippet.MediaCommand);
        Assert.Equal(SlotImageMode.Auto, savedSnippet.SlotImageMode);
    }

    [Fact]
    public void TerminalCommandSavesWithCmdAndAdminByDefault()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Reconnect Bluetooth";
        viewModel.Content = "unused";
        viewModel.LaunchPath = @"C:\unused";
        viewModel.LaunchUrl = "https://example.com";
        viewModel.IsTerminalCommandAction = true;
        viewModel.TerminalCommand = "echo reconnect";
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SnippetActionType.TerminalCommand, savedSnippet.ActionType);
        Assert.Equal(string.Empty, savedSnippet.Content);
        Assert.Null(savedSnippet.LaunchPath);
        Assert.Null(savedSnippet.LaunchUrl);
        Assert.Null(savedSnippet.MediaProvider);
        Assert.Null(savedSnippet.MediaCommand);
        Assert.Equal("echo reconnect", savedSnippet.TerminalCommand);
        Assert.Equal(SnippetTerminalShell.Cmd, savedSnippet.TerminalShell);
        Assert.True(savedSnippet.RunAsAdministrator);
    }

    [Fact]
    public void TerminalCommandCanDisableAdminAndUsePowerShell()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "List";
        viewModel.IsTerminalCommandAction = true;
        viewModel.SelectedTerminalShell = SnippetTerminalShell.PowerShell;
        viewModel.RunAsAdministrator = false;
        viewModel.TerminalCommand = "Get-ChildItem";
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SnippetTerminalShell.PowerShell, savedSnippet.TerminalShell);
        Assert.False(savedSnippet.RunAsAdministrator);
    }

    [Fact]
    public void SpotifyMediaActionSavesEvenWhenDisconnected()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Shuffle";
        viewModel.IsMediaAction = true;
        viewModel.SelectedMediaProvider = SnippetMediaProvider.Spotify;
        viewModel.SelectedMediaCommand = SnippetMediaCommand.ToggleShuffle;

        Assert.True(viewModel.ShowSpotifyMediaConnectionNotice);
        Assert.Contains(
            viewModel.MediaCommandOptions,
            option => option.Command == SnippetMediaCommand.ToggleShuffle);
        Assert.Contains(
            viewModel.MediaCommandOptions,
            option => option.Command == SnippetMediaCommand.OpenSpotifyAndResume);
        Assert.DoesNotContain(
            viewModel.MediaCommandOptions,
            option => option.Command == SnippetMediaCommand.Mute);

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SnippetActionType.MediaAction, savedSnippet.ActionType);
        Assert.Equal(SnippetMediaProvider.Spotify, savedSnippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.ToggleShuffle, savedSnippet.MediaCommand);
    }

    [Fact]
    public void ChangingMediaProviderResetsUnsupportedCommand()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        var viewModel = CreateViewModel(services, category, _ => { });

        viewModel.IsMediaAction = true;
        viewModel.SelectedMediaProvider = SnippetMediaProvider.Spotify;
        viewModel.SelectedMediaCommand = SnippetMediaCommand.CycleRepeat;
        viewModel.SelectedMediaProvider = SnippetMediaProvider.System;

        Assert.Equal(SnippetMediaCommand.PlayPause, viewModel.SelectedMediaCommand);
        Assert.Contains(
            viewModel.MediaCommandOptions,
            option => option.Command == SnippetMediaCommand.VolumeDown);
        Assert.DoesNotContain(
            viewModel.MediaCommandOptions,
            option => option.Command == SnippetMediaCommand.CycleRepeat);
        Assert.DoesNotContain(
            viewModel.MediaCommandOptions,
            option => option.Command == SnippetMediaCommand.OpenSpotifyAndResume);
    }

    [Fact]
    public void SpotifyOpenAndResumeCommandSavesWithPlayPauseIcon()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Open Spotify";
        viewModel.IsMediaAction = true;
        viewModel.SelectedMediaProvider = SnippetMediaProvider.Spotify;
        viewModel.SelectedMediaCommand = SnippetMediaCommand.OpenSpotifyAndResume;

        Assert.Equal(MediaIconResources.GetIconResourcePath(SnippetMediaCommand.PlayPause), viewModel.ThumbnailPath);

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SnippetMediaProvider.Spotify, savedSnippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.OpenSpotifyAndResume, savedSnippet.MediaCommand);
    }

    [Fact]
    public void LaunchFileSavesAutoIconForExe()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        var launchPath = CreateLaunchFile(services, "tool.exe");
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Open tool";
        viewModel.IsLaunchFileAction = true;
        viewModel.LaunchPath = launchPath;
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedSnippet);
        Assert.Equal(SlotImageMode.Auto, savedSnippet.SlotImageMode);
        Assert.NotNull(savedSnippet.AutoIconPath);
        Assert.StartsWith("icon-cache/", savedSnippet.AutoIconPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(services.Storage.ToAbsolutePath(savedSnippet.AutoIconPath)));
        Assert.Equal(launchPath, savedSnippet.AutoIconSourcePath);
        Assert.Equal(savedSnippet.AutoIconPath, viewModel.ThumbnailPath);
    }

    [Fact]
    public void RemovingCustomImageReturnsToAutoIcon()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        var launchPath = CreateLaunchFile(services, "shortcut.lnk");
        var autoIcon = services.FileIconCacheRepository.GetOrCreateIcon(launchPath, null);
        var snippet = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open shortcut",
            string.Empty,
            null,
            "custom.png",
            "custom-thumbnail.png",
            SnippetActionType.LaunchFile,
            launchPath,
            SlotImageMode.Custom,
            autoIcon);
        var viewModel = CreateViewModel(services, category, _ => { }, snippet: snippet);

        Assert.Equal("custom-thumbnail.png", viewModel.ThumbnailPath);

        viewModel.RemoveImageCommand.Execute(null);

        Assert.Equal(SlotImageMode.Auto, viewModel.SlotImageMode);
        Assert.False(viewModel.HasImage);
        Assert.Equal(autoIcon!.IconPath, viewModel.ThumbnailPath);
    }

    [Fact]
    public void LaunchFileShortcutAndFolderPickerUpdateLaunchPath()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        var dialogService = new StubDialogAdapter
        {
            LaunchFile = @"C:\Users\Public\Desktop\App.lnk",
            LaunchFolder = @"C:\tools"
        };
        var viewModel = CreateViewModel(services, category, _ => { }, dialogService);

        viewModel.ChooseLaunchFileCommand.Execute(null);
        Assert.Equal(@"C:\Users\Public\Desktop\App.lnk", viewModel.LaunchPath);

        viewModel.ChooseLaunchFolderCommand.Execute(null);
        Assert.Equal(@"C:\tools", viewModel.LaunchPath);
    }

    [Fact]
    public void SaveRequestsAutoBackupAfterSnippetChange()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            _ => { },
            autoBackupCoordinator: autoBackup);
        viewModel.SnippetTitle = "Paste";
        viewModel.Content = "Hello";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(1, autoBackup.RequestCount);
        Assert.Single(services.SnippetRepository.GetByCategoryId(category.Id));
    }

    [Fact]
    public void NewSnippetWithOnlySlotEnabledChangeSavesSlotSettingWithoutCreatingSnippet()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        services.SettingsRepository.SetSnippetSlotEnabled(SlotKey.Numpad3, false);
        Snippet? savedSnippet = null;
        var statusMessages = new List<string>();
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            snippet => savedSnippet = snippet,
            autoBackupCoordinator: autoBackup,
            showStatus: statusMessages.Add);

        viewModel.IsSlotEnabled = true;
        viewModel.SaveCommand.Execute(null);

        var settings = services.SettingsRepository.Load();
        Assert.Null(savedSnippet);
        Assert.Empty(services.SnippetRepository.GetByCategoryId(category.Id));
        Assert.True(settings.EnabledSnippetSlotKeys[SlotKey.Numpad3]);
        Assert.Equal(1, autoBackup.RequestCount);
        Assert.Equal("슬롯 3 설정을 저장했습니다.", statusMessages.Last());
    }

    [Fact]
    public void DeleteRequestsAutoBackupAfterSnippetDelete()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var snippet = services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Paste", "Hello", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            _ => { },
            snippet: snippet,
            autoBackupCoordinator: autoBackup);

        viewModel.DeleteCommand.Execute(null);

        Assert.Null(services.SnippetRepository.GetById(snippet.Id));
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void DroppingImageFileUsesCustomImagePreview()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var sourcePath = CreateTinyBmp(services.Storage.TempPath);
        var viewModel = CreateViewModel(services, category, _ => { });

        var thumbnailPath = RunInSta(() =>
        {
            viewModel.DropImageFiles([sourcePath]);
            return viewModel.ThumbnailPath;
        });

        Assert.Equal(SlotImageMode.Custom, viewModel.SlotImageMode);
        Assert.True(viewModel.HasImage);
        Assert.NotNull(thumbnailPath);
        Assert.StartsWith("images/thumbnails/", thumbnailPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(services.Storage.ToAbsolutePath(thumbnailPath)));
    }

    [Fact]
    public void CopySnippetOverwritesTargetAfterConfirmAndCopiesSlotEnabled()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var source = services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Paste", "Hello", null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad5, "Old", "Bye", null);
        services.SettingsRepository.SetSnippetSlotEnabled(SlotKey.Numpad3, false);
        var statusMessages = new List<string>();
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            _ => { },
            snippet: source,
            autoBackupCoordinator: autoBackup,
            showStatus: statusMessages.Add);
        viewModel.SelectedTransferTarget = GetTargetSlot(viewModel, SlotKey.Numpad5);

        viewModel.CopySnippetCommand.Execute(null);

        var snippets = services.SnippetRepository.GetByCategoryId(category.Id);
        var copiedSnippet = snippets.Single(snippet => snippet.SlotKey == SlotKey.Numpad5);
        var settings = services.SettingsRepository.Load();

        Assert.Equal(2, snippets.Count);
        Assert.NotEqual(source.Id, copiedSnippet.Id);
        Assert.Equal("Paste", copiedSnippet.Title);
        Assert.Equal("Hello", copiedSnippet.Content);
        Assert.False(settings.EnabledSnippetSlotKeys[SlotKey.Numpad3]);
        Assert.False(settings.EnabledSnippetSlotKeys[SlotKey.Numpad5]);
        Assert.Equal(1, autoBackup.RequestCount);
        Assert.Equal("슬롯 5에 실행 항목을 복사했습니다.", statusMessages.Last());
    }

    [Fact]
    public void MoveSnippetMovesSlotEnabledAndResetsSourceSlot()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var source = services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Paste", "Hello", null);
        services.SettingsRepository.SetSnippetSlotEnabled(SlotKey.Numpad3, false);
        var statusMessages = new List<string>();
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            _ => { },
            snippet: source,
            autoBackupCoordinator: autoBackup,
            showStatus: statusMessages.Add);
        viewModel.SelectedTransferTarget = GetTargetSlot(viewModel, SlotKey.Numpad5);

        viewModel.MoveSnippetCommand.Execute(null);

        var snippets = services.SnippetRepository.GetByCategoryId(category.Id);
        var movedSnippet = Assert.Single(snippets);
        var settings = services.SettingsRepository.Load();

        Assert.Equal(source.Id, movedSnippet.Id);
        Assert.Equal(SlotKey.Numpad5, movedSnippet.SlotKey);
        Assert.False(settings.EnabledSnippetSlotKeys[SlotKey.Numpad5]);
        Assert.True(settings.EnabledSnippetSlotKeys[SlotKey.Numpad3]);
        Assert.Equal(1, autoBackup.RequestCount);
        Assert.Equal("슬롯 5로 실행 항목을 이동했습니다.", statusMessages.Last());
    }

    [Fact]
    public void SnippetTransferCancelDoesNotOverwriteTarget()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var source = services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Paste", "Hello", null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad5, "Old", "Bye", null);
        var dialogService = new StubDialogAdapter { ConfirmResult = false };
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            _ => { },
            dialogService,
            source,
            autoBackup);
        viewModel.SelectedTransferTarget = GetTargetSlot(viewModel, SlotKey.Numpad5);

        viewModel.MoveSnippetCommand.Execute(null);

        var snippets = services.SnippetRepository.GetByCategoryId(category.Id);

        Assert.Equal("Paste", snippets.Single(snippet => snippet.SlotKey == SlotKey.Numpad3).Title);
        Assert.Equal("Old", snippets.Single(snippet => snippet.SlotKey == SlotKey.Numpad5).Title);
        Assert.Equal(0, autoBackup.RequestCount);
    }

    private static SnippetEditViewModel CreateViewModel(
        TestServices services,
        Category category,
        Action<Snippet> afterSave,
        DialogAdapter? dialogService = null,
        Snippet? snippet = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null,
        Action<string>? showStatus = null)
    {
        return new SnippetEditViewModel(
            category,
            SlotKey.Numpad3,
            snippet,
            new LoadSnippetEditorStateUseCase(
                services.SnippetRepository,
                services.SettingsRepository)
                .Execute(new LoadSnippetEditorStateRequest(category.Id, SlotKey.Numpad3, snippet?.Id)),
            new SaveSnippetUseCase(
                services.SnippetRepository,
                services.SettingsRepository,
                autoBackupCoordinator),
            new DeleteSnippetUseCase(
                services.SnippetRepository,
                services.ImageFileRepository,
                autoBackupCoordinator),
            new TransferSnippetUseCase(
                services.SnippetRepository,
                services.SettingsRepository,
                new SaveSnippetUseCase(
                    services.SnippetRepository,
                    services.SettingsRepository,
                    autoBackupCoordinator),
                services.ImageFileRepository,
                autoBackupCoordinator),
            dialogService ?? new StubDialogAdapter(),
            () => { },
            afterSave,
            () => { },
            showStatus ?? (_ => { }),
            thumbnailService: services.ImageFileRepository,
            loggingService: services.FileLogger,
            snippetImageService: services.SnippetImageResolver);
    }

    private static SnippetTransferTargetSlot GetTargetSlot(
        SnippetEditViewModel viewModel,
        SlotKey slotKey)
    {
        return viewModel.TransferTargetSlots.First(targetSlot => targetSlot.SlotKey == slotKey);
    }

    private static string CreateLaunchFile(TestServices services, string fileName)
    {
        var path = Path.Combine(services.Storage.TempPath, fileName);
        File.WriteAllText(path, "launch");

        return path;
    }

    private sealed class StubDialogAdapter : DialogAdapter
    {
        public bool ConfirmResult { get; init; } = true;

        public string? LaunchFile { get; init; }

        public string? PasteFile { get; init; }

        public string? LaunchFolder { get; init; }

        public override string? SelectLaunchFile()
        {
            return LaunchFile;
        }

        public override string? SelectPasteFile()
        {
            return PasteFile;
        }

        public override string? SelectLaunchFolder()
        {
            return LaunchFolder;
        }

        public override bool Confirm(string title, string message)
        {
            return ConfirmResult;
        }
    }
}

