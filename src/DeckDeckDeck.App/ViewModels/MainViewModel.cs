using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Windows.Input;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private IAutoBackupCoordinator? _autoBackupCoordinator;
    private MainViewModelNavigator _navigator = null!;
    private Func<ExecutableAction, Task> _executeActionAsync = null!;
    private LoadDirectHotkeyRegistrationsUseCase _loadDirectHotkeyRegistrationsUseCase = null!;
    private ResolveExecutableHotkeyActionUseCase _resolveExecutableHotkeyActionUseCase = null!;
    private ResolveCategoryHotkeyUseCase _resolveCategoryHotkeyUseCase = null!;
    private ILoadSettingsUseCase _loadSettingsUseCase = null!;
    private SaveWindowPlacementUseCase _saveWindowPlacementUseCase = null!;
    private IBluetoothAudioStatusGateway _bluetoothAudioStatusGateway = null!;
    private IAppLogger? _loggingService;
    private object? _currentViewModel;
    private string _statusMessage = "준비됨.";
    private string _topBarStatusMessage = BluetoothAudioStatusRules.LoadingText;
    private string _topBarStatusToolTip = BluetoothAudioStatusRules.LoadingToolTip;
    private CancellationTokenSource? _bluetoothStatusCancellation;
    private long _bluetoothStatusRequestVersion;
    private int _bluetoothStatusRefreshPosted;
    private bool _disposed;
    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;

    internal MainViewModel(
        MainViewModelDependencies dependencies,
        MainViewModelCallbacks? callbacks = null)
    {
        Initialize(dependencies, callbacks ?? MainViewModelCallbacks.Empty);
    }

    public string WindowTitle => "DeckDeckDeck";

    public object? CurrentViewModel
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

    /// <summary>
    /// 내부/테스트용 상태 문자열. 상단바에는 더 이상 표시하지 않는다.
    /// (네비·단축키 위치 문구는 주석 처리됨. 실행 결과 등은 여기로만 남김)
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// 상단바 우측: 기본 재생 장치가 블루투스 오디오일 때 기기명·배터리.
    /// </summary>
    public string TopBarStatusMessage
    {
        get => _topBarStatusMessage;
        private set => SetProperty(ref _topBarStatusMessage, value);
    }

    public string TopBarStatusToolTip
    {
        get => _topBarStatusToolTip;
        private set => SetProperty(ref _topBarStatusToolTip, value);
    }

    public string TopBarTitle => CurrentViewModel switch
    {
        CategoryViewModel categoryViewModel => categoryViewModel.Title,
        HotkeyListViewModel => "핫키",
        HotkeyEditViewModel hotkeyEditViewModel => hotkeyEditViewModel.Title,
        SettingsViewModel => "설정",
        CategoryEditViewModel categoryEditViewModel => $"카테고리 편집 / 슬롯 {categoryEditViewModel.KeyText}",
        SnippetEditViewModel snippetEditViewModel => $"실행 항목 편집 / 슬롯 {snippetEditViewModel.KeyText}",
        _ => string.Empty
    };

    public ICommand? TopBarBackCommand => CurrentViewModel switch
    {
        CategoryViewModel categoryViewModel => categoryViewModel.BackCommand,
        HotkeyListViewModel hotkeyListViewModel => hotkeyListViewModel.BackCommand,
        HotkeyEditViewModel hotkeyEditViewModel => hotkeyEditViewModel.CancelCommand,
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

    public bool IsCapturingHotkeyInput =>
        CurrentViewModel is HotkeyEditViewModel { IsCapturingHotkey: true };

    public event EventHandler? DirectHotkeysChanged;

    public event EventHandler? DirectHotkeyCaptureStateChanged;

    public AppSettings LoadSettings()
    {
        return _loadSettingsUseCase.Execute();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bluetoothAudioStatusGateway.StatusInvalidated -= OnBluetoothStatusInvalidated;
        var cancellation = Interlocked.Exchange(ref _bluetoothStatusCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        _bluetoothAudioStatusGateway.Dispose();
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

    public void InitializeHome()
    {
        if (CurrentViewModel is null)
        {
            ShowHome();
        }
    }

    public IReadOnlyList<string> GetVisibleThumbnailPaths()
    {
        return CurrentViewModel switch
        {
            HomeViewModel homeViewModel => GetThumbnailPaths(homeViewModel.NumpadGrid),
            CategoryViewModel categoryViewModel => GetThumbnailPaths(categoryViewModel.NumpadGrid),
            _ => []
        };
    }

    public NumpadGridViewModel? GetVisibleNumpadGrid()
    {
        return CurrentViewModel switch
        {
            HomeViewModel homeViewModel => homeViewModel.NumpadGrid,
            CategoryViewModel categoryViewModel => categoryViewModel.NumpadGrid,
            _ => null
        };
    }

    public void OpenHomeFromHotkey()
    {
        if (CurrentViewModel is not HomeViewModel)
        {
            ShowHome();
        }

        // [removed from top bar] 네비/단축키 위치 문구 — TopBarTitle과 역할이 겹쳐 상단 표시 제거.
        // StatusMessage = "전역 단축키로 홈을 열었습니다.";
        _ = RefreshBluetoothAudioStatusAsync();
    }

    public void OpenCategoryFromHotkey(SlotKey slotKey)
    {
        var resolution = _resolveCategoryHotkeyUseCase.Execute(slotKey);
        switch (resolution.Kind)
        {
            case CategoryHotkeyResolutionKind.OpenExisting:
                OpenResolvedCategoryFromHotkey(resolution.Category!);
                break;
            case CategoryHotkeyResolutionKind.CreateNew:
                _navigator.CreateCategory(resolution.SlotKey);
                break;
            case CategoryHotkeyResolutionKind.Blocked:
            case CategoryHotkeyResolutionKind.Unsupported:
                // 차단 사유는 내부 StatusMessage에만 남김(상단바 비표시).
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

    public IReadOnlyList<DirectHotkeyRegistration> LoadActiveDirectHotkeys()
    {
        return _loadDirectHotkeyRegistrationsUseCase.Execute();
    }

    public async Task ExecuteDirectHotkeyAsync(Guid hotkeyActionId)
    {
        var action = _resolveExecutableHotkeyActionUseCase.Execute(hotkeyActionId);
        if (action is null)
        {
            return;
        }

        await _executeActionAsync(action);
    }

    /// <summary>
    /// 창이 다시 보일 때 등 외부에서 블루투스 상태를 즉시 갱신할 때 사용.
    /// </summary>
    public Task RefreshBluetoothAudioStatusAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        return RefreshBluetoothAudioStatusCoreAsync();
    }

    private async Task RefreshBluetoothAudioStatusCoreAsync()
    {
        var requestVersion = Interlocked.Increment(ref _bluetoothStatusRequestVersion);
        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(
            ref _bluetoothStatusCancellation,
            cancellation);
        previousCancellation?.Cancel();
        previousCancellation?.Dispose();

        try
        {
            var snapshot = await _bluetoothAudioStatusGateway.GetCurrentAsync(cancellation.Token);
            if (_disposed
                || cancellation.IsCancellationRequested
                || requestVersion != Interlocked.Read(ref _bluetoothStatusRequestVersion))
            {
                return;
            }

            ApplyBluetoothStatus(snapshot);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer refresh owns the top-bar state.
        }
        catch (Exception ex)
        {
            _loggingService?.Log("블루투스 오디오 상태 조회 실패", ex);
            // Preserve the last truthful connection/name instead of reporting a false disconnect.
        }
        finally
        {
            Interlocked.CompareExchange(ref _bluetoothStatusCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private void ApplyBluetoothStatus(BluetoothAudioStatusSnapshot snapshot)
    {
        if (!snapshot.IsBluetoothAudioConnected)
        {
            TopBarStatusMessage = BluetoothAudioStatusRules.DisconnectedText;
            TopBarStatusToolTip = BluetoothAudioStatusRules.DisconnectedToolTip;
            return;
        }

        TopBarStatusMessage = BluetoothAudioStatusRules.FormatDisplayText(
            snapshot.DeviceName,
            snapshot.BatteryPercent);
        TopBarStatusToolTip = BluetoothAudioStatusRules.FormatToolTip(
            snapshot.DeviceName,
            snapshot.BatteryPercent);
    }

    private void OnBluetoothStatusInvalidated(object? sender, EventArgs e)
    {
        if (_disposed || Interlocked.Exchange(ref _bluetoothStatusRefreshPosted, 1) == 1)
        {
            return;
        }

        void RefreshOnOwnerContext()
        {
            Interlocked.Exchange(ref _bluetoothStatusRefreshPosted, 0);
            _ = RefreshBluetoothAudioStatusAsync();
        }

        if (_synchronizationContext is null
            || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            RefreshOnOwnerContext();
            return;
        }

        _synchronizationContext.Post(_ => RefreshOnOwnerContext(), null);
    }

    internal void NotifyDirectHotkeysChanged()
    {
        DirectHotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void NotifyDirectHotkeyCaptureStateChanged()
    {
        DirectHotkeyCaptureStateChanged?.Invoke(this, EventArgs.Empty);
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
        // 상단바에는 더 이상 반영하지 않음. 실행 결과·저장 등 내부/테스트용으로만 유지.
        StatusMessage = message;
    }

    private void OpenResolvedCategoryFromHotkey(Category category)
    {
        if (CurrentViewModel is CategoryViewModel categoryViewModel
            && categoryViewModel.CategoryId == category.Id)
        {
            return;
        }

        _navigator.OpenCategory(category);
    }

    private static IReadOnlyList<string> GetThumbnailPaths(NumpadGridViewModel grid)
    {
        return grid.Slots
            .Select(slot => slot.ThumbnailPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => path!)
            .ToList();
    }

    private void NotifyTopBarPropertiesChanged()
    {
        OnPropertyChanged(nameof(TopBarTitle));
        OnPropertyChanged(nameof(TopBarBackCommand));
        OnPropertyChanged(nameof(TopBarSettingsCommand));
        OnPropertyChanged(nameof(TopBarStatusMessage));
        OnPropertyChanged(nameof(TopBarStatusToolTip));
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
        _loadDirectHotkeyRegistrationsUseCase = dependencies.LoadDirectHotkeyRegistrationsUseCase;
        _resolveExecutableHotkeyActionUseCase = dependencies.ResolveExecutableHotkeyActionUseCase;
        _loggingService = dependencies.Logger;
        _autoBackupCoordinator = dependencies.AutoBackupCoordinator;
        _bluetoothAudioStatusGateway = dependencies.BluetoothAudioStatusGateway;

        var viewFactory = new MainViewModelViewFactory(
            dependencies.NavigatorDependencies,
            snippet => dependencies.ExecuteActionAsync(ExecutableAction.FromSnippet(snippet)),
            ShowStatus);
        _executeActionAsync = dependencies.ExecuteActionAsync;

        _navigator = new MainViewModelNavigator(
            dependencies.NavigatorDependencies,
            viewFactory,
            viewModel => CurrentViewModel = viewModel,
            ShowStatus,
            callbacks.EnterEditMode,
            NotifyDirectHotkeysChanged,
            NotifyDirectHotkeyCaptureStateChanged);

        _bluetoothAudioStatusGateway.StatusInvalidated += OnBluetoothStatusInvalidated;
        try
        {
            _bluetoothAudioStatusGateway.StartMonitoring();
        }
        catch (Exception ex)
        {
            _loggingService?.Log("블루투스 오디오 변경 감시 시작 실패", ex);
        }

        _ = RefreshBluetoothAudioStatusAsync();
    }
}
