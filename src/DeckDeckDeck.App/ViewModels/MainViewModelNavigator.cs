using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class MainViewModelNavigator
{
    private readonly Action _enterEditMode;
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly Func<Snippet, Task> _pasteSnippet;
    private readonly AppServices _services;
    private readonly Action<string> _showStatus;
    private readonly Action<object> _showViewModel;

    public MainViewModelNavigator(
        AppServices services,
        Action<object> showViewModel,
        Action<string> showStatus,
        Action enterEditMode,
        Func<Snippet, Task> pasteSnippet,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        _services = services;
        _showViewModel = showViewModel;
        _showStatus = showStatus;
        _enterEditMode = enterEditMode;
        _pasteSnippet = pasteSnippet;
        _autoBackupCoordinator = autoBackupCoordinator;
    }

    public void ShowHome()
    {
        _showViewModel(new HomeViewModel(
            _services.CategoryService,
            _services.SettingsService,
            _services.SlotService,
            OpenCategory,
            EditCategory,
            CreateCategory,
            () => ShowSettings(ShowHome)));
        _showStatus("홈");
    }

    public void CreateCategory(SlotKey slotKey)
    {
        _enterEditMode();
        _showViewModel(new CategoryEditViewModel(
            slotKey,
            category: null,
            _services.CategoryService,
            _services.CategoryTransferService,
            _services.DialogService,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            _showStatus,
            _services.ThumbnailService,
            _services.SettingsService,
            _services.LoggingService,
            _autoBackupCoordinator));
        _showStatus($"슬롯 {slotKey.GetDisplayText()}에 새 카테고리 만들기");
    }

    public void EditCategory(Category category)
    {
        _enterEditMode();
        _showViewModel(new CategoryEditViewModel(
            category.SlotKey,
            category,
            _services.CategoryService,
            _services.CategoryTransferService,
            _services.DialogService,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            _showStatus,
            _services.ThumbnailService,
            _services.SettingsService,
            _services.LoggingService,
            _autoBackupCoordinator));
        _showStatus($"{category.Name} 편집");
    }

    public void OpenCategory(Category category)
    {
        _showViewModel(new CategoryViewModel(
            category,
            _services.SnippetService,
            _services.SettingsService,
            _services.SlotService,
            ShowHome,
            () => ShowSettings(() => OpenCategoryById(category.Id)),
            EditSnippet,
            _pasteSnippet));
        _showStatus($"{category.Name} 카테고리");
    }

    private void OpenCategoryById(Guid categoryId)
    {
        var category = _services.CategoryService.GetById(categoryId);

        if (category is null)
        {
            ShowHome();
            return;
        }

        OpenCategory(category);
    }

    private void EditSnippet(Category category, SlotKey slotKey, Snippet? snippet)
    {
        _enterEditMode();
        _showViewModel(new SnippetEditViewModel(
            category,
            slotKey,
            snippet,
            _services.SnippetService,
            _services.SnippetTransferService,
            _services.DialogService,
            () => OpenCategoryById(category.Id),
            _ => OpenCategoryById(category.Id),
            () => OpenCategoryById(category.Id),
            _showStatus,
            _services.ThumbnailService,
            _services.SettingsService,
            _services.LoggingService,
            _services.SnippetImageService,
            _autoBackupCoordinator));
        _showStatus(snippet is null
            ? $"슬롯 {slotKey.GetDisplayText()}에 새 실행 항목 만들기"
            : $"{snippet.Title} 편집");
    }

    private void ShowSettings(Action returnTo)
    {
        _enterEditMode();
        _showViewModel(new SettingsViewModel(
            _services.SettingsService,
            returnTo,
            returnTo,
            _showStatus,
            _services.LoggingService,
            _services.BackupService,
            _autoBackupCoordinator,
            _services.DialogService,
            _services.SpotifyConnectionService,
            _services.UrlLaunchService));
        _showStatus("설정");
    }
}
