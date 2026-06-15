using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class MainViewModelNavigator
{
    private readonly MainViewModelNavigatorDependencies _dependencies;
    private readonly Action _enterEditMode;
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly Func<Snippet, Task> _pasteSnippet;
    private readonly Action<string> _showStatus;
    private readonly Action<object> _showViewModel;

    public MainViewModelNavigator(
        MainViewModelNavigatorDependencies dependencies,
        Action<object> showViewModel,
        Action<string> showStatus,
        Action enterEditMode,
        Func<Snippet, Task> pasteSnippet,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        _dependencies = dependencies;
        _showViewModel = showViewModel;
        _showStatus = showStatus;
        _enterEditMode = enterEditMode;
        _pasteSnippet = pasteSnippet;
        _autoBackupCoordinator = autoBackupCoordinator;
    }

    public void ShowHome()
    {
        _showViewModel(new HomeViewModel(
            _dependencies.CategoryRepository,
            _dependencies.SettingsRepository,
            _dependencies.SlotGridViewModelFactory,
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
            _dependencies.DialogAdapter,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            _showStatus,
            _dependencies.ImageFileRepository,
            _dependencies.Logger,
            _dependencies.StoredImagePathResolver));
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
            _dependencies.DialogAdapter,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            _showStatus,
            _dependencies.ImageFileRepository,
            _dependencies.Logger,
            _dependencies.StoredImagePathResolver));
        _showStatus($"{category.Name} 편집");
    }

    public void OpenCategory(Category category)
    {
        _showViewModel(new CategoryViewModel(
            category,
            _dependencies.SnippetRepository,
            _dependencies.SettingsRepository,
            _dependencies.SlotGridViewModelFactory,
            ShowHome,
            () => ShowSettings(() => OpenCategoryById(category.Id)),
            EditSnippet,
            _pasteSnippet));
        _showStatus($"{category.Name} 카테고리");
    }

    private void OpenCategoryById(Guid categoryId)
    {
        var category = _dependencies.CategoryRepository.GetById(categoryId);

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
            _dependencies.DialogAdapter,
            () => OpenCategoryById(category.Id),
            _ => OpenCategoryById(category.Id),
            () => OpenCategoryById(category.Id),
            _showStatus,
            _dependencies.ImageFileRepository,
            _dependencies.Logger,
            _dependencies.SnippetImageResolver,
            _dependencies.StoredImagePathResolver));
        _showStatus(snippet is null
            ? $"슬롯 {slotKey.GetDisplayText()}에 새 실행 항목 만들기"
            : $"{snippet.Title} 편집");
    }

    private SaveCategoryUseCase CreateSaveCategoryUseCase()
    {
        return new SaveCategoryUseCase(
            _dependencies.CategoryRepository,
            _dependencies.SettingsRepository,
            _autoBackupCoordinator);
    }

    private LoadCategoryEditorStateUseCase CreateLoadCategoryEditorStateUseCase()
    {
        return new LoadCategoryEditorStateUseCase(
            _dependencies.CategoryRepository,
            _dependencies.SettingsRepository);
    }

    private DeleteCategoryUseCase CreateDeleteCategoryUseCase()
    {
        return new DeleteCategoryUseCase(
            _dependencies.CategoryRepository,
            _dependencies.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private TransferCategoryUseCase CreateTransferCategoryUseCase()
    {
        return new TransferCategoryUseCase(
            _dependencies.CategoryRepository,
            _dependencies.SettingsRepository,
            CreateSaveCategoryUseCase(),
            _dependencies.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private SaveSnippetUseCase CreateSaveSnippetUseCase()
    {
        return new SaveSnippetUseCase(
            _dependencies.SnippetRepository,
            _dependencies.SettingsRepository,
            _autoBackupCoordinator);
    }

    private LoadSnippetEditorStateUseCase CreateLoadSnippetEditorStateUseCase()
    {
        return new LoadSnippetEditorStateUseCase(
            _dependencies.SnippetRepository,
            _dependencies.SettingsRepository);
    }

    private DeleteSnippetUseCase CreateDeleteSnippetUseCase()
    {
        return new DeleteSnippetUseCase(
            _dependencies.SnippetRepository,
            _dependencies.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private TransferSnippetUseCase CreateTransferSnippetUseCase()
    {
        return new TransferSnippetUseCase(
            _dependencies.SnippetRepository,
            _dependencies.SettingsRepository,
            CreateSaveSnippetUseCase(),
            _dependencies.ImageFileRepository,
            _autoBackupCoordinator);
    }

    private void ShowSettings(Action returnTo)
    {
        _enterEditMode();
        _showViewModel(new SettingsViewModel(
            _dependencies.SettingsRepository,
            returnTo,
            returnTo,
            _showStatus,
            _dependencies.Logger,
            _dependencies.BackupGateway,
            _autoBackupCoordinator,
            _dependencies.DialogAdapter,
            _dependencies.SpotifyConnectionUseCase,
            _dependencies.ClipboardTextWriter));
        _showStatus("설정");
    }
}
