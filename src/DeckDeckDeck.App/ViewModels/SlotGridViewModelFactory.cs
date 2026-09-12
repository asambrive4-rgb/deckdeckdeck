// 역할: 홈 화면과 카테고리 화면에 표시될 9개 슬롯 타일 뷰모델들을 생성합니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SlotGridViewModelFactory
{
    private readonly IStoredImagePathResolver? _storedImagePathResolver;
    private readonly ISnippetImageResolver? _snippetImageResolver;
    private readonly ISlotTimerUseCases? _timerUseCases;

    public SlotGridViewModelFactory(
        IStoredImagePathResolver? storedImagePathResolver = null,
        ISnippetImageResolver? snippetImageResolver = null,
        ISlotTimerUseCases? timerUseCases = null)
    {
        _storedImagePathResolver = storedImagePathResolver;
        _snippetImageResolver = snippetImageResolver;
        _timerUseCases = timerUseCases;
    }

    public NumpadGridViewModel BuildCategoryGrid(
        IEnumerable<Category> categories,
        AppSettings settings,
        Action<SlotKey, Category?> onSelected,
        Action<SlotKey, Category?> onEdit,
        Action? onHotkeySelected = null,
        Action<SlotKey, SlotKey>? onReorder = null)
    {
        var categoriesBySlot = categories.ToDictionary(category => category.SlotKey);

        return new NumpadGridViewModel(
            SlotKeyCatalog.All.Select(slotKey =>
        {
            categoriesBySlot.TryGetValue(slotKey, out var category);
            var vm = new SlotViewModel(
                slotKey,
                category?.Name,
                ResolveDisplayPath(category?.ThumbnailPath),
                SlotRules.IsEnabled(slotKey, settings.EnabledCategorySlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, category),
                selectedSlotKey => onEdit(selectedSlotKey, category));

            BindTimerState(vm, isCategoryView: false);
            return vm;
        }),
            onHotkeySelected is null
                ? HotkeyTileViewModel.Disabled()
                : HotkeyTileViewModel.Enabled(onHotkeySelected),
            onReorder);
    }

    public NumpadGridViewModel BuildSnippetGrid(
        IEnumerable<Snippet> snippets,
        AppSettings settings,
        Action<SlotKey, Snippet?> onSelected,
        Action<SlotKey, Snippet?> onEdit,
        Action<SlotKey, SlotKey>? onReorder = null)
    {
        var snippetsBySlot = snippets.ToDictionary(snippet => snippet.SlotKey);

        return new NumpadGridViewModel(
            SlotKeyCatalog.All.Select(slotKey =>
        {
            snippetsBySlot.TryGetValue(slotKey, out var snippet);
            var thumbnailPath = ResolveSnippetDisplayPath(snippet);
            var vm = new SlotViewModel(
                slotKey,
                snippet?.Title,
                thumbnailPath,
                SlotRules.IsEnabled(slotKey, settings.EnabledSnippetSlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, snippet),
                selectedSlotKey => onEdit(selectedSlotKey, snippet));

            if (snippet?.ActionType == SnippetActionType.Timer)
            {
                vm.IsTimerSlot = true;
            }

            BindTimerState(vm, isCategoryView: true);
            return vm;
        }),
            HotkeyTileViewModel.Disabled(),
            onReorder);
    }

    private void BindTimerState(SlotViewModel vm, bool isCategoryView)
    {
        if (_timerUseCases is null) return;

        vm.IsCategoryView = isCategoryView;
        var initialState = _timerUseCases.GetTimerState(vm.SlotKey);
        vm.IsTimerRunning = initialState.IsRunning;
        vm.FormattedTimerRemaining = initialState.FormattedRemainingTime;

        _timerUseCases.TimerStateChanged += (sender, args) =>
        {
            if (args.SlotKey == vm.SlotKey)
            {
                UpdateSlotVmOnDispatcher(vm, args.State);
            }
        };
    }

    private static void UpdateSlotVmOnDispatcher(SlotViewModel slotVm, SlotTimerState state)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() =>
            {
                slotVm.IsTimerRunning = state.IsRunning;
                slotVm.FormattedTimerRemaining = state.FormattedRemainingTime;
            });
        }
        else
        {
            slotVm.IsTimerRunning = state.IsRunning;
            slotVm.FormattedTimerRemaining = state.FormattedRemainingTime;
        }
    }

    private string? ResolveDisplayPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || _storedImagePathResolver is null)
        {
            return path;
        }

        return _storedImagePathResolver.ResolveDisplayPath(path);
    }

    private string? ResolveSnippetDisplayPath(Snippet? snippet)
    {
        if (_snippetImageResolver is not null)
        {
            return _snippetImageResolver.GetDisplayImagePath(snippet);
        }

        if (snippet is null)
        {
            return null;
        }

        return snippet.SlotImageMode switch
        {
            SlotImageMode.Custom => ResolveDisplayPath(snippet.ThumbnailPath),
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.LaunchFile
                && !string.IsNullOrWhiteSpace(snippet.AutoIconPath)
                => ResolveDisplayPath(snippet.AutoIconPath),
            SlotImageMode.Auto when snippet.ActionType == SnippetActionType.MediaAction
                => MediaIconResourcePaths.GetIconResourcePath(snippet.MediaCommand),
            _ => null
        };
    }
}
