using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using System.Windows.Input;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private IAutoBackupCoordinator? _autoBackupCoordinator;
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
        Action? completePasteSelection = null,
        Func<Action>? createPasteSelectionCompletion = null)
        : this(
            AppServices.CreateDefault(),
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            createPasteSelectionCompletion)
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
        Func<Action>? createPasteSelectionCompletion = null,
        LoggingService? loggingService = null,
        ThumbnailService? thumbnailService = null,
        IFileLaunchService? fileLaunchService = null,
        IUrlLaunchService? urlLaunchService = null,
        IMediaActionService? mediaActionService = null,
        SnippetImageService? snippetImageService = null,
        BackupService? backupService = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        var transferService = new CategoryTransferService(
            categoryService,
            settingsService,
            thumbnailService,
            loggingService);
        var snippetTransferService = new SnippetTransferService(
            snippetService,
            settingsService,
            thumbnailService,
            loggingService);

        var services = new AppServices(
            categoryService,
            transferService,
            backupService,
            dialogService,
            settingsService,
            slotService,
            snippetService,
            snippetTransferService,
            snippetImageService,
            clipboardPasteService ?? new ClipboardPasteService(),
            fileLaunchService ?? new FileLaunchService(),
            urlLaunchService ?? new UrlLaunchService(),
            mediaActionService ?? new MediaActionService(),
            loggingService,
            thumbnailService);

        Initialize(
            services,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            createPasteSelectionCompletion,
            autoBackupCoordinator);
    }

    private MainViewModel(
        AppServices services,
        Func<IntPtr>? getPasteTargetWindowHandle,
        Action? hideWindowAfterPaste,
        Action? enterEditMode,
        Action? completePasteSelection,
        Func<Action>? createPasteSelectionCompletion)
    {
        Initialize(
            services,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            createPasteSelectionCompletion,
            autoBackupCoordinator: null);
    }

    public string WindowTitle => "DeckDeckDeck";

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (!SetProperty(ref _currentViewModel, value))
            {
                return;
            }

            NotifyTopBarPropertiesChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (!SetProperty(ref _statusMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TopBarStatusMessage));
        }
    }

    public string TopBarStatusMessage => StatusMessage == "홈"
        ? "준비됨"
        : StatusMessage;

    public string TopBarTitle => CurrentViewModel switch
    {
        CategoryViewModel categoryViewModel => categoryViewModel.Title,
        SettingsViewModel => "설정",
        CategoryEditViewModel categoryEditViewModel => $"카테고리 편집 / 슬롯 {categoryEditViewModel.KeyText}",
        SnippetEditViewModel snippetEditViewModel => $"실행 항목 편집 / 슬롯 {snippetEditViewModel.KeyText}",
        _ => string.Empty
    };

    public ICommand? TopBarBackCommand => CurrentViewModel switch
    {
        CategoryViewModel categoryViewModel => categoryViewModel.BackCommand,
        SettingsViewModel settingsViewModel => settingsViewModel.BackCommand,
        CategoryEditViewModel categoryEditViewModel => categoryEditViewModel.CancelCommand,
        SnippetEditViewModel snippetEditViewModel => snippetEditViewModel.CancelCommand,
        _ => null
    };

    public ICommand? TopBarSettingsCommand => CurrentViewModel switch
    {
        HomeViewModel homeViewModel => homeViewModel.SettingsCommand,
        CategoryViewModel categoryViewModel => categoryViewModel.SettingsCommand,
        _ => null
    };

    public bool ShowTopBarBackButton => TopBarBackCommand is not null;

    public bool ShowTopBarSettingsButton => TopBarSettingsCommand is not null;

    public bool ShowTopBarTitle => !string.IsNullOrWhiteSpace(TopBarTitle);

    public AppSettings LoadSettings()
    {
        return _settingsService.Load();
    }

    public void Dispose()
    {
        (_autoBackupCoordinator as IDisposable)?.Dispose();
    }

    public void SaveWindowPlacement(double left, double top, string screenDeviceName)
    {
        try
        {
            _settingsService.SaveWindowPlacement(left, top, screenDeviceName);
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

    private void NotifyTopBarPropertiesChanged()
    {
        OnPropertyChanged(nameof(TopBarTitle));
        OnPropertyChanged(nameof(TopBarBackCommand));
        OnPropertyChanged(nameof(TopBarSettingsCommand));
        OnPropertyChanged(nameof(TopBarStatusMessage));
        OnPropertyChanged(nameof(ShowTopBarBackButton));
        OnPropertyChanged(nameof(ShowTopBarSettingsButton));
        OnPropertyChanged(nameof(ShowTopBarTitle));
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
        Action? completePasteSelection,
        Func<Action>? createPasteSelectionCompletion,
        IAutoBackupCoordinator? autoBackupCoordinator)
    {
        _categoryService = services.CategoryService;
        _settingsService = services.SettingsService;
        _loggingService = services.LoggingService;
        _autoBackupCoordinator = autoBackupCoordinator
            ?? (services.BackupService is null
                ? null
                : new AutoBackupCoordinator(
                    services.BackupService,
                    services.SettingsService,
                    ShowStatus,
                    services.LoggingService));

        var pasteFlowService = new PasteFlowService(
            services.ClipboardPasteService,
            services.FileLaunchService,
            services.UrlLaunchService,
            services.MediaActionService,
            services.SettingsService,
            getPasteTargetWindowHandle ?? (() => IntPtr.Zero),
            hideWindowAfterPaste ?? (() => { }),
            createPasteSelectionCompletion ?? (() => completePasteSelection ?? (() => { })),
            ShowStatus,
            services.LoggingService);

        _navigator = new MainViewModelNavigator(
            services,
            viewModel => CurrentViewModel = viewModel,
            ShowStatus,
            enterEditMode ?? (() => { }),
            pasteFlowService.PasteSnippetAsync,
            _autoBackupCoordinator);
        _settingsService.EnsureDefaults();

        ShowHome();
    }
}
