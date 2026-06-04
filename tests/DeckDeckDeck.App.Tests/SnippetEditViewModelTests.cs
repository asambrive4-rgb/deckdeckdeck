using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class SnippetEditViewModelTests
{
    [Fact]
    public void PasteTextRequiresContent()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Paste";
        viewModel.Content = string.Empty;
        viewModel.SaveCommand.Execute(null);

        Assert.Null(savedSnippet);
        Assert.Equal("붙여넣을 문구를 입력해 주세요.", viewModel.ErrorMessage);
    }

    [Fact]
    public void LaunchFileRequiresLaunchPath()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
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
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Web", null);
        Snippet? savedSnippet = null;
        var viewModel = CreateViewModel(services, category, snippet => savedSnippet = snippet);

        viewModel.SnippetTitle = "Open site";
        viewModel.IsLaunchUrlAction = true;
        viewModel.SaveCommand.Execute(null);

        Assert.Null(savedSnippet);
        Assert.Equal("열 웹페이지 주소를 http 또는 https 주소로 입력해 주세요.", viewModel.ErrorMessage);
    }

    [Fact]
    public void LaunchFileSavesWithLaunchPathAndEmptyContent()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
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
    public void LaunchUrlSavesWithNormalizedUrlAndEmptyContent()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Web", null);
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
    public void LaunchFileSavesAutoIconForExe()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
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
        Assert.True(File.Exists(savedSnippet.AutoIconPath));
        Assert.Equal(launchPath, savedSnippet.AutoIconSourcePath);
        Assert.Equal(savedSnippet.AutoIconPath, viewModel.ThumbnailPath);
    }

    [Fact]
    public void RemovingCustomImageReturnsToAutoIcon()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
        var launchPath = CreateLaunchFile(services, "shortcut.lnk");
        var autoIcon = services.FileIconCacheService.GetOrCreateIcon(launchPath, null);
        var snippet = services.SnippetService.Create(
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
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
        var dialogService = new StubDialogService
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
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
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
        Assert.Single(services.SnippetService.GetByCategoryId(category.Id));
    }

    [Fact]
    public void DeleteRequestsAutoBackupAfterSnippetDelete()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var snippet = services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Paste", "Hello", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = CreateViewModel(
            services,
            category,
            _ => { },
            snippet: snippet,
            autoBackupCoordinator: autoBackup);

        viewModel.DeleteCommand.Execute(null);

        Assert.Null(services.SnippetService.GetById(snippet.Id));
        Assert.Equal(1, autoBackup.RequestCount);
    }

    private static SnippetEditViewModel CreateViewModel(
        TestServices services,
        Category category,
        Action<Snippet> afterSave,
        DialogService? dialogService = null,
        Snippet? snippet = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        return new SnippetEditViewModel(
            category,
            SlotKey.Numpad3,
            snippet,
            services.SnippetService,
            dialogService ?? new StubDialogService(),
            () => { },
            afterSave,
            () => { },
            _ => { },
            thumbnailService: services.ThumbnailService,
            settingsService: services.SettingsService,
            loggingService: services.LoggingService,
            snippetImageService: services.SnippetImageService,
            autoBackupCoordinator: autoBackupCoordinator);
    }

    private static string CreateLaunchFile(TestServices services, string fileName)
    {
        var path = Path.Combine(services.Storage.TempPath, fileName);
        File.WriteAllText(path, "launch");

        return path;
    }

    private sealed class StubDialogService : DialogService
    {
        public bool ConfirmResult { get; init; } = true;

        public string? LaunchFile { get; init; }

        public string? LaunchFolder { get; init; }

        public override string? SelectLaunchFile()
        {
            return LaunchFile;
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
