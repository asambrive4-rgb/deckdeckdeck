using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class MainViewModelNavigator
{
    private readonly MainViewModelNavigatorDependencies _dependencies;
    private readonly Action _enterEditMode;
    private readonly MainViewModelViewFactory _viewFactory;
    private readonly Action<string> _showStatus;
    private readonly Action<object> _showViewModel;

    public MainViewModelNavigator(
        MainViewModelNavigatorDependencies dependencies,
        MainViewModelViewFactory viewFactory,
        Action<object> showViewModel,
        Action<string> showStatus,
        Action enterEditMode)
    {
        _dependencies = dependencies;
        _viewFactory = viewFactory;
        _showViewModel = showViewModel;
        _showStatus = showStatus;
        _enterEditMode = enterEditMode;
    }

    public void ShowHome()
    {
        _showViewModel(_viewFactory.CreateHome(
            OpenCategory,
            EditCategory,
            CreateCategory,
            () => ShowSettings(ShowHome)));
        _showStatus("홈");
    }

    public void CreateCategory(SlotKey slotKey)
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateCategoryEditor(
            slotKey,
            category: null,
            ShowHome,
            _ => ShowHome(),
            ShowHome));
        _showStatus($"슬롯 {slotKey.GetDisplayText()}에 새 카테고리 만들기");
    }

    public void EditCategory(Category category)
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateCategoryEditor(
            category.SlotKey,
            category,
            ShowHome,
            _ => ShowHome(),
            ShowHome));
        _showStatus($"{category.Name} 편집");
    }

    public void OpenCategory(Category category)
    {
        _showViewModel(_viewFactory.CreateCategory(
            category,
            ShowHome,
            () => ShowSettings(() => OpenCategoryById(category.Id)),
            EditSnippet));
        _showStatus($"{category.Name} 카테고리");
    }

    private void OpenCategoryById(Guid categoryId)
    {
        var category = _dependencies.GetCategoryByIdUseCase.Execute(categoryId);

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
        _showViewModel(_viewFactory.CreateSnippetEditor(
            category,
            slotKey,
            snippet,
            () => OpenCategoryById(category.Id),
            _ => OpenCategoryById(category.Id),
            () => OpenCategoryById(category.Id)));
        _showStatus(snippet is null
            ? $"슬롯 {slotKey.GetDisplayText()}에 새 실행 항목 만들기"
            : $"{snippet.Title} 편집");
    }

    private void ShowSettings(Action returnTo)
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateSettings(
            returnTo,
            returnTo));
        _showStatus("설정");
    }
}
