// 역할: 홈 화면과 카테고리 화면에 표시될 슬롯 타일 뷰모델들을 생성하고 타이머 상태를 약한 이벤트로 바인딩합니다.
using System.Runtime.CompilerServices;
using System.Windows;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SlotGridViewModelFactory
{
    private static readonly ConditionalWeakTable<SlotViewModel, SlotTimerObserver> TimerObservers = new();

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
        SlotViewModel[] slots = [.. SlotKeyCatalog.All.Select(slotKey =>
        {
            var category = categoriesBySlot.GetValueOrDefault(slotKey);
            return BindTimerState(new SlotViewModel(
                slotKey,
                category?.Name,
                ResolveDisplayPath(category?.ThumbnailPath),
                SlotRules.IsEnabled(slotKey, settings.EnabledCategorySlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, category),
                selectedSlotKey => onEdit(selectedSlotKey, category)), isCategoryView: false);
        })];

        return new NumpadGridViewModel(
            slots,
            onHotkeySelected is not null ? HotkeyTileViewModel.Enabled(onHotkeySelected) : HotkeyTileViewModel.Disabled(),
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
        SlotViewModel[] slots = [.. SlotKeyCatalog.All.Select(slotKey =>
        {
            var snippet = snippetsBySlot.GetValueOrDefault(slotKey);
            return BindTimerState(new SlotViewModel(
                slotKey,
                snippet?.Title,
                ResolveSnippetDisplayPath(snippet),
                SlotRules.IsEnabled(slotKey, settings.EnabledSnippetSlotKeys),
                selectedSlotKey => onSelected(selectedSlotKey, snippet),
                selectedSlotKey => onEdit(selectedSlotKey, snippet))
            {
                IsTimerSlot = snippet?.ActionType == SnippetActionType.Timer
            }, isCategoryView: true);
        })];

        return new NumpadGridViewModel(slots, HotkeyTileViewModel.Disabled(), onReorder);
    }

    private SlotViewModel BindTimerState(SlotViewModel vm, bool isCategoryView)
    {
        if (_timerUseCases is null)
        {
            return vm;
        }

        vm.IsCategoryView = isCategoryView;
        var initialState = _timerUseCases.GetTimerState(vm.SlotKey);
        vm.IsTimerRunning = initialState.IsRunning;
        vm.FormattedTimerRemaining = initialState.FormattedRemainingTime;

        var observer = new SlotTimerObserver(vm);
        TimerObservers.AddOrUpdate(vm, observer);
        WeakEventManager<ISlotTimerUseCases, SlotTimerStateChangedEventArgs>.AddHandler(
            _timerUseCases,
            nameof(ISlotTimerUseCases.TimerStateChanged),
            observer.OnTimerStateChanged);

        return vm;
    }

    private sealed class SlotTimerObserver
    {
        private readonly WeakReference<SlotViewModel> _targetRef;
        private readonly SlotKey _slotKey;

        public SlotTimerObserver(SlotViewModel target)
        {
            _targetRef = new WeakReference<SlotViewModel>(target);
            _slotKey = target.SlotKey;
        }

        public void OnTimerStateChanged(object? sender, SlotTimerStateChangedEventArgs args)
        {
            if (args.SlotKey == _slotKey && _targetRef.TryGetTarget(out var vm))
            {
                UpdateSlotVmOnDispatcher(vm, args.State);
            }
        }
    }

    private static void UpdateSlotVmOnDispatcher(SlotViewModel slotVm, SlotTimerState state)
    {
        void Apply()
        {
            slotVm.IsTimerRunning = state.IsRunning;
            slotVm.FormattedTimerRemaining = state.FormattedRemainingTime;
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(Apply);
            return;
        }

        Apply();
    }

    private string? ResolveDisplayPath(string? path) =>
        string.IsNullOrWhiteSpace(path) || _storedImagePathResolver is null
            ? path
            : _storedImagePathResolver.ResolveDisplayPath(path);

    private string? ResolveSnippetDisplayPath(Snippet? snippet) =>
        _snippetImageResolver is not null
            ? _snippetImageResolver.GetDisplayImagePath(snippet)
            : snippet switch
            {
                { SlotImageMode: SlotImageMode.Custom } => ResolveDisplayPath(snippet.ThumbnailPath),
                { SlotImageMode: SlotImageMode.Auto, ActionType: SnippetActionType.LaunchFile }
                    when !string.IsNullOrWhiteSpace(snippet.AutoIconPath)
                    => ResolveDisplayPath(snippet.AutoIconPath),
                { SlotImageMode: SlotImageMode.Auto, ActionType: SnippetActionType.MediaAction }
                    => MediaIconResourcePaths.GetIconResourcePath(snippet.MediaCommand),
                _ => null
            };
}
