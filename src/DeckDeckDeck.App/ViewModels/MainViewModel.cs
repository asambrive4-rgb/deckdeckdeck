using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private CategoryService _categoryService = null!;
    private MainViewModelNavigator _navigator = null!;
    private SettingsService _settingsService = null!;
    private LoggingService? _loggingService;
    private object _currentViewModel = null!;
    private string _statusMessage = "준비됨.";

    public MainViewModel(
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null)
        : this(AppServices.CreateDefault(), getPasteTargetWindowHandle, hideWindowAfterPaste, enterEditMode, completePasteSelection)
    {
    }

    public MainViewModel(
        CategoryService categoryService,
        DialogService dialogService,
        SettingsService settingsService,
        SlotService slotService,
        SnippetService snippetService,
        IClipboardPasteService? clipboardPasteService = null,
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null,
        LoggingService? loggingService = null,
        ThumbnailService? thumbnailService = null,
        IFileLaunchService? fileLaunchService = null,
        SnippetImageService? snippetImageService = null)
    {
        var transferService = new CategoryTransferService(
            categoryService,
            settingsService,
            thumbnailService,
            loggingService);

        var services = new AppServices(
            categoryService,
            transferService,
            dialogService,
            settingsService,
            slotService,
            snippetService,
            snippetImageService,
            clipboardPasteService ?? new ClipboardPasteService(),
            fileLaunchService ?? new FileLaunchService(),
            loggingService,
            thumbnailService);

        Initialize(
            services,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection);
    }

    private MainViewModel(
        AppServices services,
        Func<IntPtr>? getPasteTargetWindowHandle,
        Action? hideWindowAfterPaste,
        Action? enterEditMode,
        Action? completePasteSelection)
    {
        Initialize(
            services,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection);
    }

    public string WindowTitle => "DeckDeckDeck";

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public AppSettings LoadSettings()
    {
        return _settingsService.Load();
    }

    public void SaveWindowPlacement(double left, double top, string screenDeviceName)
    {
        try
        {
            var settings = _settingsService.Load();
            settings.LastWindowLeft = left;
            settings.LastWindowTop = top;
            settings.LastWindowScreenDeviceName = screenDeviceName;
            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            _loggingService?.Log("Window placement save failed.", ex);
        }
    }

    public void ShowHome()
    {
        _navigator.ShowHome();
    }

    public void OpenHomeFromHotkey()
    {
        ShowHome();
        StatusMessage = "전역 단축키로 홈을 열었습니다.";
    }

    public void OpenCategoryFromHotkey(SlotKey slotKey)
    {
        if (!IsDirectCategorySlot(slotKey))
        {
            StatusMessage = "카테고리 바로 열기 단축키는 Ctrl+Numpad 1~9와 기호를 지원합니다.";
            return;
        }

        var settings = _settingsService.Load();
        if (settings.EnabledCategorySlotKeys.TryGetValue(slotKey, out var isEnabled) && !isEnabled)
        {
            StatusMessage = $"슬롯 {slotKey.GetDisplayText()}은 사용 안 함 상태입니다.";
            return;
        }

        var category = _categoryService.GetBySlotKey(slotKey);
        if (category is null)
        {
            _navigator.CreateCategory(slotKey);
            return;
        }

        _navigator.OpenCategory(category);
    }

    public void ReportHotkeyRegistrationFailure(IReadOnlyList<string> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        StatusMessage = failures.Count == 1
            ? failures[0]
            : $"전역 단축키 {failures.Count}개를 등록하지 못했습니다.";
        _loggingService?.Log(StatusMessage);
    }

    public bool SelectSlot(SlotKey slotKey)
    {
        return CurrentViewModel switch
        {
            HomeViewModel homeViewModel => homeViewModel.SelectSlot(slotKey),
            CategoryViewModel categoryViewModel => categoryViewModel.SelectSlot(slotKey),
            _ => false
        };
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
    }

    private static bool IsDirectCategorySlot(SlotKey slotKey)
    {
        return slotKey != SlotKey.Numpad0;
    }

    private void Initialize(
        AppServices services,
        Func<IntPtr>? getPasteTargetWindowHandle,
        Action? hideWindowAfterPaste,
        Action? enterEditMode,
        Action? completePasteSelection)
    {
        _categoryService = services.CategoryService;
        _settingsService = services.SettingsService;
        _loggingService = services.LoggingService;

        var pasteFlowService = new PasteFlowService(
            services.ClipboardPasteService,
            services.FileLaunchService,
            services.SettingsService,
            getPasteTargetWindowHandle ?? (() => IntPtr.Zero),
            hideWindowAfterPaste ?? (() => { }),
            completePasteSelection ?? (() => { }),
            ShowStatus,
            services.LoggingService);

        _navigator = new MainViewModelNavigator(
            services,
            viewModel => CurrentViewModel = viewModel,
            ShowStatus,
            enterEditMode ?? (() => { }),
            pasteFlowService.PasteSnippetAsync);
        _settingsService.EnsureDefaults();

        ShowHome();
    }
}
