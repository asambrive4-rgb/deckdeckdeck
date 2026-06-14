using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.UseCases;
using System.Windows.Input;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private IAutoBackupCoordinator? _autoBackupCoordinator;
    private MainViewModelNavigator _navigator = null!;
    private ResolveCategoryHotkeyUseCase _resolveCategoryHotkeyUseCase = null!;
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
        SlotGridViewModelFactory slotGridViewModelFactory,
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
        ISpotifyConnectionService? spotifyConnectionService = null,
        ISpotifyMediaActionService? spotifyMediaActionService = null,
        SnippetImageService? snippetImageService = null,
        BackupService? backupService = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null,
        IStoredImagePathResolver? storedImagePathResolver = null,
        IClipboardService? clipboardService = null)
    {
        var services = AppServices.Create(
            categoryService,
            backupService,
            dialogService,
            settingsService,
            snippetService,
            snippetImageService,
            clipboardPasteService,
            fileLaunchService,
            urlLaunchService,
            mediaActionService,
            spotifyConnectionService,
            spotifyMediaActionService,
            storedImagePathResolver,
            loggingService,
            thumbnailService,
            slotGridViewModelFactory,
            clipboardService);

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
        var resolution = _resolveCategoryHotkeyUseCase.Execute(slotKey);
        switch (resolution.Kind)
        {
            case CategoryHotkeyResolutionKind.OpenExisting:
                _navigator.OpenCategory(resolution.Category!);
                break;
            case CategoryHotkeyResolutionKind.CreateNew:
                _navigator.CreateCategory(resolution.SlotKey);
                break;
            case CategoryHotkeyResolutionKind.Blocked:
            case CategoryHotkeyResolutionKind.Unsupported:
                StatusMessage = resolution.StatusMessage ?? string.Empty;
                break;
        }
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

    private void Initialize(
        AppServices services,
        Func<IntPtr>? getPasteTargetWindowHandle,
        Action? hideWindowAfterPaste,
        Action? enterEditMode,
        Action? completePasteSelection,
        Func<Action>? createPasteSelectionCompletion,
        IAutoBackupCoordinator? autoBackupCoordinator)
    {
        _settingsService = services.SettingsService;
        _resolveCategoryHotkeyUseCase = services.ResolveCategoryHotkeyUseCase;
        _loggingService = services.LoggingService;
        _autoBackupCoordinator = autoBackupCoordinator
            ?? (services.BackupService is null
                ? null
                : new AutoBackupCoordinator(
                    services.BackupService,
                    services.SettingsService,
                    ShowStatus,
                    services.LoggingService));

        _navigator = new MainViewModelNavigator(
            services,
            viewModel => CurrentViewModel = viewModel,
            ShowStatus,
            enterEditMode ?? (() => { }),
            snippet => ExecuteSnippetActionAsync(
                snippet,
                services.ExecuteSnippetActionUseCase,
                getPasteTargetWindowHandle ?? (() => IntPtr.Zero),
                hideWindowAfterPaste ?? (() => { }),
                createPasteSelectionCompletion ?? (() => completePasteSelection ?? (() => { }))),
            _autoBackupCoordinator);
        _settingsService.EnsureDefaults();

        ShowHome();
    }

    private async Task ExecuteSnippetActionAsync(
        Snippet snippet,
        ExecuteSnippetActionUseCase executeSnippetActionUseCase,
        Func<IntPtr> getPasteTargetWindowHandle,
        Action hideWindowAfterPaste,
        Func<Action> createPasteSelectionCompletion)
    {
        var settings = _settingsService.Load();
        var completePasteSelection = createPasteSelectionCompletion();

        try
        {
            if (snippet.ActionType == SnippetActionType.PasteText && settings.AutoHideAfterPaste)
            {
                hideWindowAfterPaste();
            }

            var result = await executeSnippetActionUseCase.ExecuteAsync(
                new ExecuteSnippetActionRequest(snippet, settings, getPasteTargetWindowHandle()));

            if (result.ShouldHideWindow)
            {
                hideWindowAfterPaste();
            }

            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                ShowStatus(result.StatusMessage);
            }

            LogSnippetActionResult(result);
        }
        finally
        {
            completePasteSelection();
        }
    }

    private void LogSnippetActionResult(ExecuteSnippetActionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.LogMessage))
        {
            return;
        }

        if (result.Exception is null)
        {
            _loggingService?.Log(result.LogMessage);
            return;
        }

        _loggingService?.Log(result.LogMessage, result.Exception);
    }
}
