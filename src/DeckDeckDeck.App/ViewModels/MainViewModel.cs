using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Windows.Input;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private IAutoBackupCoordinator? _autoBackupCoordinator;
    private MainViewModelNavigator _navigator = null!;
    private ResolveCategoryHotkeyUseCase _resolveCategoryHotkeyUseCase = null!;
    private ILoadSettingsUseCase _loadSettingsUseCase = null!;
    private SaveWindowPlacementUseCase _saveWindowPlacementUseCase = null!;
    private IAppLogger? _loggingService;
    private object _currentViewModel = null!;
    private string _statusMessage = "준비됨.";

    internal MainViewModel(
        MainViewModelDependencies dependencies,
        MainViewModelCallbacks? callbacks = null)
    {
        Initialize(dependencies, callbacks ?? MainViewModelCallbacks.Empty);
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
        return _loadSettingsUseCase.Execute();
    }

    public void Dispose()
    {
        (_autoBackupCoordinator as IDisposable)?.Dispose();
    }

    public void SaveWindowPlacement(double left, double top, string screenDeviceName)
    {
        _saveWindowPlacementUseCase.Execute(
            new SaveWindowPlacementRequest(left, top, screenDeviceName));
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

    internal void ReportBackgroundStatus(string message)
    {
        ShowStatus(message);
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
        MainViewModelDependencies dependencies,
        MainViewModelCallbacks callbacks)
    {
        _loadSettingsUseCase = dependencies.LoadSettingsUseCase;
        _saveWindowPlacementUseCase = dependencies.SaveWindowPlacementUseCase;
        _resolveCategoryHotkeyUseCase = dependencies.ResolveCategoryHotkeyUseCase;
        _loggingService = dependencies.Logger;
        _autoBackupCoordinator = dependencies.AutoBackupCoordinator;

        var actionRunner = new SnippetActionRunner(
            dependencies.LoadSettingsUseCase,
            dependencies.PrepareSnippetActionUseCase,
            dependencies.ExecuteSnippetActionUseCase,
            callbacks,
            ShowStatus,
            dependencies.Logger);
        var viewFactory = new MainViewModelViewFactory(
            dependencies.NavigatorDependencies,
            actionRunner.ExecuteAsync,
            ShowStatus);

        _navigator = new MainViewModelNavigator(
            dependencies.NavigatorDependencies,
            viewFactory,
            viewModel => CurrentViewModel = viewModel,
            ShowStatus,
            callbacks.EnterEditMode);

        ShowHome();
    }
}
