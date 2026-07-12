using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class MainViewModelNavigator
{
    private const int MaxCachedCategories = 3;

    private readonly MainViewModelNavigatorDependencies _dependencies;
    private readonly Action _enterEditMode;
    private readonly Action _notifyDirectHotkeyCaptureStateChanged;
    private readonly Action _notifyDirectHotkeysChanged;
    private readonly MainViewModelViewFactory _viewFactory;
    private readonly Action<string> _showStatus;
    private readonly Action<object> _showViewModel;

    private HomeViewModel? _cachedHome;
    private readonly List<CachedCategoryEntry> _categoryLru = new(MaxCachedCategories);

    public MainViewModelNavigator(
        MainViewModelNavigatorDependencies dependencies,
        MainViewModelViewFactory viewFactory,
        Action<object> showViewModel,
        Action<string> showStatus,
        Action enterEditMode,
        Action notifyDirectHotkeysChanged,
        Action notifyDirectHotkeyCaptureStateChanged)
    {
        _dependencies = dependencies;
        _viewFactory = viewFactory;
        _showViewModel = showViewModel;
        _showStatus = showStatus;
        _enterEditMode = enterEditMode;
        _notifyDirectHotkeysChanged = notifyDirectHotkeysChanged;
        _notifyDirectHotkeyCaptureStateChanged = notifyDirectHotkeyCaptureStateChanged;
    }

    public void ShowHome()
    {
        _showViewModel(GetOrCreateHome());
        _showStatus("홈");
    }

    public void CreateCategory(SlotKey slotKey)
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateCategoryEditor(
            slotKey,
            category: null,
            cancel: ShowHome,
            afterSave: _ =>
            {
                InvalidateHome();
                InvalidateCategoryCache();
                ShowHome();
            },
            afterDelete: () =>
            {
                InvalidateHome();
                InvalidateCategoryCache();
                ShowHome();
            }));
        _showStatus($"슬롯 {slotKey.GetDisplayText()}에 새 카테고리 만들기");
    }

    public void EditCategory(Category category)
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateCategoryEditor(
            category.SlotKey,
            category,
            cancel: ShowHome,
            afterSave: _ =>
            {
                InvalidateHome();
                InvalidateCategoryCache();
                ShowHome();
            },
            afterDelete: () =>
            {
                InvalidateHome();
                InvalidateCategoryCache();
                ShowHome();
            }));
        _showStatus($"{category.Name} 편집");
    }

    public void OpenCategory(Category category)
    {
        _showViewModel(GetOrCreateCategory(category));
        _showStatus($"{category.Name} 카테고리");
    }

    private void OpenCategoryById(Guid categoryId)
    {
        if (TryShowCachedCategory(categoryId))
        {
            return;
        }

        var category = _dependencies.GetCategoryByIdUseCase.Execute(categoryId);

        if (category is null)
        {
            InvalidateCategory(categoryId);
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
            cancel: () => OpenCategoryById(category.Id),
            afterSave: _ =>
            {
                InvalidateCategory(category.Id);
                OpenCategoryById(category.Id);
            },
            afterDelete: () =>
            {
                InvalidateCategory(category.Id);
                OpenCategoryById(category.Id);
            }));
        _showStatus(snippet is null
            ? $"슬롯 {slotKey.GetDisplayText()}에 새 실행 항목 만들기"
            : $"{snippet.Title} 편집");
    }

    private void ShowSettings(Action returnTo)
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateSettings(
            cancel: returnTo,
            afterSave: () =>
            {
                // Settings can change slot enablement and other grid-facing flags.
                InvalidateHome();
                InvalidateCategoryCache();
                returnTo();
            }));
        _showStatus("설정");
    }

    public void ShowHotkeys()
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateHotkeyList(
            CreateHotkey,
            EditHotkey,
            ShowHome,
            ShowHotkeys,
            NotifyHotkeysChanged));
        _showStatus("핫키");
    }

    private void CreateHotkey()
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateHotkeyEditor(
            action: null,
            ShowHotkeys,
            _ =>
            {
                ShowHotkeys();
                NotifyHotkeysChanged();
            },
            () =>
            {
                ShowHotkeys();
                NotifyHotkeysChanged();
            },
            _notifyDirectHotkeyCaptureStateChanged));
        _showStatus("새 핫키 만들기");
    }

    private void EditHotkey(HotkeyAction action)
    {
        _enterEditMode();
        _showViewModel(_viewFactory.CreateHotkeyEditor(
            action,
            ShowHotkeys,
            _ =>
            {
                ShowHotkeys();
                NotifyHotkeysChanged();
            },
            () =>
            {
                ShowHotkeys();
                NotifyHotkeysChanged();
            },
            _notifyDirectHotkeyCaptureStateChanged));
        _showStatus($"{action.Title} 핫키 편집");
    }

    private void NotifyHotkeysChanged()
    {
        _notifyDirectHotkeysChanged();
    }

    private HomeViewModel GetOrCreateHome()
    {
        if (_cachedHome is not null)
        {
            return _cachedHome;
        }

        _cachedHome = _viewFactory.CreateHome(
            OpenCategory,
            EditCategory,
            CreateCategory,
            () => ShowSettings(ShowHome),
            ShowHotkeys,
            ReorderCategory);
        return _cachedHome;
    }

    private CategoryViewModel GetOrCreateCategory(Category category)
    {
        if (TryGetCachedCategory(category.Id, touch: true, out var cached))
        {
            return cached;
        }

        var categoryId = category.Id;
        var created = _viewFactory.CreateCategory(
            category,
            ShowHome,
            () => ShowSettings(() => OpenCategoryById(categoryId)),
            EditSnippet,
            (source, target) => ReorderSnippet(categoryId, source, target));
        AddCategoryToCache(category.Id, created);
        return created;
    }

    private void ReorderCategory(SlotKey sourceSlotKey, SlotKey targetSlotKey)
    {
        try
        {
            var result = _dependencies.MoveCategorySlotUseCase.Execute(
                new MoveCategorySlotRequest(sourceSlotKey, targetSlotKey, OverwriteConfirmed: false));

            if (result.NeedsOverwriteConfirmation)
            {
                var confirmed = _dependencies.DialogAdapter.Confirm(
                    "카테고리 이동",
                    $"슬롯 {targetSlotKey.GetDisplayText()}에 이미 '{result.ExistingTargetName}' 카테고리가 있습니다.\n기존 카테고리와 안의 실행 항목을 덮어쓸까요?");
                if (!confirmed)
                {
                    return;
                }

                result = _dependencies.MoveCategorySlotUseCase.Execute(
                    new MoveCategorySlotRequest(sourceSlotKey, targetSlotKey, OverwriteConfirmed: true));
            }

            if (result.IsNoOp)
            {
                return;
            }

            if (!result.Succeeded)
            {
                _showStatus(result.ErrorMessage ?? "카테고리 이동에 실패했습니다.");
                return;
            }

            InvalidateHome();
            InvalidateCategoryCache();
            ShowHome();
            _showStatus($"슬롯 {targetSlotKey.GetDisplayText()}로 카테고리를 이동했습니다.");
        }
        catch (Exception)
        {
            _showStatus("카테고리 이동에 실패했습니다.");
        }
    }

    private void ReorderSnippet(Guid categoryId, SlotKey sourceSlotKey, SlotKey targetSlotKey)
    {
        try
        {
            var result = _dependencies.MoveSnippetSlotUseCase.Execute(
                new MoveSnippetSlotRequest(
                    categoryId,
                    sourceSlotKey,
                    targetSlotKey,
                    OverwriteConfirmed: false));

            if (result.NeedsOverwriteConfirmation)
            {
                var confirmed = _dependencies.DialogAdapter.Confirm(
                    "실행 항목 이동",
                    $"슬롯 {targetSlotKey.GetDisplayText()}에 이미 '{result.ExistingTargetName}' 실행 항목이 있습니다.\n기존 실행 항목을 덮어쓸까요?");
                if (!confirmed)
                {
                    return;
                }

                result = _dependencies.MoveSnippetSlotUseCase.Execute(
                    new MoveSnippetSlotRequest(
                        categoryId,
                        sourceSlotKey,
                        targetSlotKey,
                        OverwriteConfirmed: true));
            }

            if (result.IsNoOp)
            {
                return;
            }

            if (!result.Succeeded)
            {
                _showStatus(result.ErrorMessage ?? "실행 항목 이동에 실패했습니다.");
                return;
            }

            InvalidateCategory(categoryId);
            OpenCategoryById(categoryId);
            _showStatus($"슬롯 {targetSlotKey.GetDisplayText()}로 실행 항목을 이동했습니다.");
        }
        catch (Exception)
        {
            _showStatus("실행 항목 이동에 실패했습니다.");
        }
    }

    private bool TryShowCachedCategory(Guid categoryId)
    {
        if (!TryGetCachedCategory(categoryId, touch: true, out var cached))
        {
            return false;
        }

        _showViewModel(cached);
        _showStatus($"{cached.Title} 카테고리");
        return true;
    }

    private bool TryGetCachedCategory(Guid categoryId, bool touch, out CategoryViewModel viewModel)
    {
        for (var i = 0; i < _categoryLru.Count; i++)
        {
            var entry = _categoryLru[i];
            if (entry.CategoryId != categoryId)
            {
                continue;
            }

            if (touch && i < _categoryLru.Count - 1)
            {
                _categoryLru.RemoveAt(i);
                _categoryLru.Add(entry);
            }

            viewModel = entry.ViewModel;
            return true;
        }

        viewModel = null!;
        return false;
    }

    private void AddCategoryToCache(Guid categoryId, CategoryViewModel viewModel)
    {
        // Replace any prior entry for the same id (defensive if factory is called twice).
        for (var i = _categoryLru.Count - 1; i >= 0; i--)
        {
            if (_categoryLru[i].CategoryId == categoryId)
            {
                _categoryLru.RemoveAt(i);
            }
        }

        _categoryLru.Add(new CachedCategoryEntry(categoryId, viewModel));
        while (_categoryLru.Count > MaxCachedCategories)
        {
            _categoryLru.RemoveAt(0);
        }
    }

    private void InvalidateHome()
    {
        _cachedHome = null;
    }

    private void InvalidateCategory(Guid categoryId)
    {
        _categoryLru.RemoveAll(entry => entry.CategoryId == categoryId);
    }

    private void InvalidateCategoryCache()
    {
        _categoryLru.Clear();
    }

    private readonly record struct CachedCategoryEntry(Guid CategoryId, CategoryViewModel ViewModel);
}
