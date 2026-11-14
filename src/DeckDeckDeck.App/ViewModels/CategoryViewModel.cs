// 역할: 선택된 카테고리에 속한 9개 슬롯의 상태와 Android ADB 연결 요약 배지를 화면에 표시하고 클릭 입력을 처리하는 화면 모델입니다.
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class CategoryViewModel : ObservableObject
{
    private readonly Category _category;
    private readonly Action<Category, SlotKey, Snippet?> _editSnippet;
    private readonly Func<Snippet, Task> _pasteSnippet;
    private readonly ISlotTimerCoordinator? _timerCoordinator;
    private readonly IAndroidRemoteInstallGateway? _androidRemoteInstallGateway;
    private string _androidConnectionSummaryText = "ADB 연결 확인 중...";

    public CategoryViewModel(
        Category category,
        CategoryGridState gridState,
        SlotGridViewModelFactory slotGridViewModelFactory,
        Action showHome,
        Action showSettings,
        Action<Category, SlotKey, Snippet?> editSnippet,
        Func<Snippet, Task> pasteSnippet,
        Action<SlotKey, SlotKey>? reorderSnippet = null,
        ISlotTimerCoordinator? timerCoordinator = null,
        IAndroidRemoteInstallGateway? androidRemoteInstallGateway = null)
    {
        _category = category;
        _editSnippet = editSnippet;
        _pasteSnippet = pasteSnippet;
        _timerCoordinator = timerCoordinator;
        _androidRemoteInstallGateway = androidRemoteInstallGateway;

        Title = category.Name;
        Subtitle = $"슬롯 {category.SlotKey.GetDisplayText()} 카테고리";
        BackCommand = new RelayCommand(showHome);
        SettingsCommand = new RelayCommand(showSettings);
        RefreshAndroidConnectionCommand = new RelayCommand(() => _ = RefreshAndroidConnectionAsync());

        HasAndroidDeviceSlots = gridState.Snippets.Any(static snippet =>
            snippet.ActionType == SnippetActionType.RemoteInstall
            || TerminalCommandParameterRules.IsAdbWirelessConnectCommand(snippet.TerminalCommand));

        NumpadGrid = slotGridViewModelFactory.BuildSnippetGrid(
            gridState.Snippets,
            gridState.Settings,
            SelectSnippetSlot,
            EditSnippetSlot,
            reorderSnippet);

        if (ShowAndroidConnectionSummary)
        {
            _ = RefreshAndroidConnectionAsync();
        }
    }

    public string Title { get; }

    public Guid CategoryId => _category.Id;

    public string Subtitle { get; }

    public NumpadGridViewModel NumpadGrid { get; }

    public ICommand BackCommand { get; }

    public ICommand SettingsCommand { get; }

    public ICommand RefreshAndroidConnectionCommand { get; }

    public bool HasAndroidDeviceSlots { get; }

    public bool ShowAndroidConnectionSummary => HasAndroidDeviceSlots && _androidRemoteInstallGateway is not null;

    public string AndroidConnectionSummaryText
    {
        get => _androidConnectionSummaryText;
        private set => SetProperty(ref _androidConnectionSummaryText, value);
    }

    public bool SelectSlot(SlotKey slotKey)
    {
        NumpadGrid.SelectSlot(slotKey);
        return true;
    }

    public async Task RefreshAndroidConnectionAsync()
    {
        if (_androidRemoteInstallGateway is null)
        {
            return;
        }

        try
        {
            AndroidConnectionSummaryText = "ADB 연결 확인 중...";
            var devices = await _androidRemoteInstallGateway.ScanConnectedDevicesAsync();
            if (devices.Count == 0)
            {
                AndroidConnectionSummaryText = "ADB 연결: 없음 (1·2·3 슬롯으로 기기 연결)";
                return;
            }

            var tabletCount = devices.Count(static d => d.DeviceKind == RemoteInstallDeviceKind.Tablet);
            var phoneCount = devices.Count(static d => d.DeviceKind == RemoteInstallDeviceKind.Phone);
            var watchCount = devices.Count(static d => d.DeviceKind == RemoteInstallDeviceKind.Watch);
            var models = string.Join(", ", devices.Select(static d => d.Model));
            AndroidConnectionSummaryText =
                $"ADB 연결: 태블릿 {tabletCount} · 폰 {phoneCount} · 워치 {watchCount} ({models})";
        }
        catch
        {
            AndroidConnectionSummaryText = "ADB 연결 상태를 확인하지 못했습니다.";
        }
    }

    private void SelectSnippetSlot(SlotKey slotKey, Snippet? snippet)
    {
        if (snippet is null)
        {
            _editSnippet(_category, slotKey, snippet);
            return;
        }

        _ = PasteSnippetSafely(snippet);
    }

    private void EditSnippetSlot(SlotKey slotKey, Snippet? snippet)
    {
        _editSnippet(_category, slotKey, snippet);
    }

    private async Task PasteSnippetSafely(Snippet snippet)
    {
        try
        {
            await _pasteSnippet(snippet);
        }
        catch
        {
            // Paste failures are intentionally silent in this stage.
        }
    }
}
