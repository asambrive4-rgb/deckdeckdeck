using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class MainViewModelViewFactory
{
    private readonly MainViewModelNavigatorDependencies _dependencies;
    private readonly Func<Snippet, Task> _executeSnippet;
    private readonly Action<string> _showStatus;

    public MainViewModelViewFactory(
        MainViewModelNavigatorDependencies dependencies,
        Func<Snippet, Task> executeSnippet,
        Action<string> showStatus)
    {
        _dependencies = dependencies;
        _executeSnippet = executeSnippet;
        _showStatus = showStatus;
    }

    public HomeViewModel CreateHome(
        Action<Category> openCategory,
        Action<Category> editCategory,
        Action<SlotKey> createCategory,
        Action showSettings)
    {
        return new HomeViewModel(
            _dependencies.LoadHomeGridUseCase.Execute(),
            _dependencies.SlotGridViewModelFactory,
            openCategory,
            editCategory,
            createCategory,
            showSettings);
    }

    public CategoryViewModel CreateCategory(
        Category category,
        Action showHome,
        Action showSettings,
        Action<Category, SlotKey, Snippet?> editSnippet)
    {
        return new CategoryViewModel(
            category,
            _dependencies.LoadCategoryGridUseCase.Execute(category.Id),
            _dependencies.SlotGridViewModelFactory,
            showHome,
            showSettings,
            editSnippet,
            _executeSnippet);
    }

    public CategoryEditViewModel CreateCategoryEditor(
        SlotKey slotKey,
        Category? category,
        Action cancel,
        Action<Category> afterSave,
        Action afterDelete)
    {
        return new CategoryEditViewModel(
            slotKey,
            category,
            _dependencies.LoadCategoryEditorStateUseCase.Execute(
                new LoadCategoryEditorStateRequest(slotKey, category?.Id)),
            _dependencies.SaveCategoryUseCase,
            _dependencies.DeleteCategoryUseCase,
            _dependencies.TransferCategoryUseCase,
            _dependencies.DialogAdapter,
            cancel,
            afterSave,
            afterDelete,
            _showStatus,
            _dependencies.ImageFileRepository,
            _dependencies.Logger,
            _dependencies.StoredImagePathResolver);
    }

    public SnippetEditViewModel CreateSnippetEditor(
        Category category,
        SlotKey slotKey,
        Snippet? snippet,
        Action cancel,
        Action<Snippet> afterSave,
        Action afterDelete)
    {
        return new SnippetEditViewModel(
            category,
            slotKey,
            snippet,
            _dependencies.LoadSnippetEditorStateUseCase.Execute(
                new LoadSnippetEditorStateRequest(category.Id, slotKey, snippet?.Id)),
            _dependencies.SaveSnippetUseCase,
            _dependencies.DeleteSnippetUseCase,
            _dependencies.TransferSnippetUseCase,
            _dependencies.DialogAdapter,
            cancel,
            afterSave,
            afterDelete,
            _showStatus,
            _dependencies.ImageFileRepository,
            _dependencies.Logger,
            _dependencies.SnippetImageResolver,
            _dependencies.StoredImagePathResolver);
    }

    public SettingsViewModel CreateSettings(
        Action cancel,
        Action afterSave)
    {
        return new SettingsViewModel(
            _dependencies.LoadSettingsUseCase,
            _dependencies.SaveSettingsUseCase,
            _dependencies.CreateManualBackupUseCase,
            _dependencies.RestoreBackupUseCase,
            _dependencies.SpotifyConnectionUseCase,
            _dependencies.ClipboardTextWriter,
            _dependencies.DialogAdapter,
            cancel,
            afterSave,
            _showStatus,
            _dependencies.Logger);
    }
}
