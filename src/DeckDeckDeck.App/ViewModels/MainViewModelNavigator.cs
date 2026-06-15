using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class MainViewModelNavigator
{
    private readonly Action _enterEditMode;
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly Func<Snippet, Task> _pasteSnippet;
    private readonly AppComposition _services;
    private readonly Action<string> _showStatus;
    private readonly Action<object> _showViewModel;

    public MainViewModelNavigator(
        AppComposition services,
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
            _services.CategoryRepository,
            _services.SettingsRepository,
            _services.SlotGridViewModelFactory,
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
            CreateLoadCategoryEditorStateUseCase()
                .Execute(new LoadCategoryEditorStateRequest(slotKey, CategoryId: null)),
            CreateSaveCategoryUseCase(),
            CreateDeleteCategoryUseCase(),
            CreateTransferCategoryUseCase(),
            _services.DialogAdapter,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            _showStatus,
            _services.ImageFileRepository,
            _services.FileLogger,
            _services.StoredImagePathResolver));
        _showStatus($"슬롯 {slotKey.GetDisplayText()}에 새 카테고리 만들기");
    }

    public void EditCategory(Category category)
    {
        _enterEditMode();
        _showViewModel(new CategoryEditViewModel(
            category.SlotKey,
            category,
            CreateLoadCategoryEditorStateUseCase()
                .Execute(new LoadCategoryEditorStateRequest(category.SlotKey, category.Id)),
            CreateSaveCategoryUseCase(),
            CreateDeleteCategoryUseCase(),
            CreateTransferCategoryUseCase(),
            _services.DialogAdapter,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            _showStatus,
            _services.ImageFileRepository,
            _services.FileLogger,
            _services.StoredImagePathResolver));
        _showStatus($"{category.Name} 편집");
    }

    public void OpenCategory(Category category)
    {
        _showViewModel(new CategoryViewModel(
            category,
            _services.SnippetRepository,
            _services.SettingsRepository,
            _services.SlotGridViewModelFactory,
            ShowHome,
            () => ShowSettings(() => OpenCategoryById(category.Id)),
            EditSnippet,
            _pasteSnippet));
        _showStatus($"{category.Name} 카테고리");
    }

    private void OpenCategoryById(Guid categoryId)
    {
        var category = _services.CategoryRepository.GetById(categoryId);

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
            CreateLoadSnippetEditorStateUseCase()
                .Execute(new LoadSnippetEditorStateRequest(category.Id, slotKey, snippet?.Id)),
            CreateSaveSnippetUseCase(),
            CreateDeleteSnippetUseCase(),
            CreateTransferSnippetUseCase(),
            _services.DialogAdapter,
            () => OpenCategoryById(category.Id),
            _ => OpenCategoryById(category.Id),
            () => OpenCategoryById(category.Id),
            _showStatus,
            _services.ImageFileRepository,
            _services.FileLogger,
            _services.SnippetImageResolver,
            _services.StoredImagePathResolver));
        _showStatus(snippet is null
            ? $"슬롯 {slotKey.GetDisplayText()}에 새 실행 항목 만들기"
            : $"{snippet.Title} 편집");
    }

    private SaveCategoryUseCase CreateSaveCategoryUseCase()
    {
        return new SaveCategoryUseCase(
            _services.CategoryRepository,
            _services.SettingsRepository,
            _autoBackupCoordinator);
    }

    private LoadCategoryEditorStateUseCase CreateLoadCategoryEditorStateUseCase()
    {
        return new LoadCategoryEditorStateUseCase(
            _services.CategoryRepository,
            _services.SettingsRepository);
    }

    private DeleteCategoryUseCase CreateDeleteCategoryUseCase()
    {
        return new DeleteCategoryUseCase(
            _services.CategoryRepository,
            _services.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private TransferCategoryUseCase CreateTransferCategoryUseCase()
    {
        return new TransferCategoryUseCase(
            _services.CategoryRepository,
            _services.SettingsRepository,
            CreateSaveCategoryUseCase(),
            _services.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private SaveSnippetUseCase CreateSaveSnippetUseCase()
    {
        return new SaveSnippetUseCase(
            _services.SnippetRepository,
            _services.SettingsRepository,
            _autoBackupCoordinator);
    }

    private LoadSnippetEditorStateUseCase CreateLoadSnippetEditorStateUseCase()
    {
        return new LoadSnippetEditorStateUseCase(
            _services.SnippetRepository,
            _services.SettingsRepository);
    }

    private DeleteSnippetUseCase CreateDeleteSnippetUseCase()
    {
        return new DeleteSnippetUseCase(
            _services.SnippetRepository,
            _services.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private TransferSnippetUseCase CreateTransferSnippetUseCase()
    {
        return new TransferSnippetUseCase(
            _services.SnippetRepository,
            _services.SettingsRepository,
            CreateSaveSnippetUseCase(),
            _services.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private void ShowSettings(Action returnTo)
    {
        _enterEditMode();
        _showViewModel(new SettingsViewModel(
            _services.SettingsRepository,
            returnTo,
            returnTo,
            _showStatus,
            _services.FileLogger,
            _services.BackupGateway,
            _autoBackupCoordinator,
            _services.DialogAdapter,
            _services.SpotifyConnectionUseCase,
            _services.ClipboardAdapter));
        _showStatus("설정");
    }
}

