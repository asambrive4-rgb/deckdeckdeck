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
        Assert.Equal("실행할 파일 또는 폴더를 선택해 주세요.", viewModel.ErrorMessage);
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
    public void LaunchFileAndFolderPickerUpdateLaunchPath()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
        var dialogService = new StubDialogService
        {
            LaunchFile = @"C:\tools\app.exe",
            LaunchFolder = @"C:\tools"
        };
        var viewModel = CreateViewModel(services, category, _ => { }, dialogService);

        viewModel.ChooseLaunchFileCommand.Execute(null);
        Assert.Equal(@"C:\tools\app.exe", viewModel.LaunchPath);

        viewModel.ChooseLaunchFolderCommand.Execute(null);
        Assert.Equal(@"C:\tools", viewModel.LaunchPath);
    }

    private static SnippetEditViewModel CreateViewModel(
        TestServices services,
        Category category,
        Action<Snippet> afterSave,
        DialogService? dialogService = null)
    {
        return new SnippetEditViewModel(
            category,
            SlotKey.Numpad3,
            snippet: null,
            services.SnippetService,
            dialogService ?? new StubDialogService(),
            () => { },
            afterSave,
            () => { },
            _ => { },
            settingsService: services.SettingsService,
            loggingService: services.LoggingService);
    }

    private sealed class StubDialogService : DialogService
    {
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
    }
}
