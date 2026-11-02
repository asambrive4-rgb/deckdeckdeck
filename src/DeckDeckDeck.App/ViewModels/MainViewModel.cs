// 역할: 메인 화면 전체 상태와 화면 전환, 슬롯 실행 및 다이렉트 핫키 조율, 블루투스 기기 선택을 통합 관리하는 화면 모델입니다.
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

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
    private IBluetoothAudioStatusUseCase _bluetoothAudioStatusUseCase = null!;
    private IAppLogger? _loggingService;
    private object? _currentViewModel;
    private string _statusMessage = "준비됨.";
    private string _topBarStatusMessage = BluetoothAudioStatusRules.LoadingText;
    private string _topBarStatusToolTip = BluetoothAudioStatusRules.LoadingToolTip;
    private string _topBarDeviceName = BluetoothAudioStatusRules.LoadingText;
    private int? _topBarBatteryPercent;
    private string _topBarBatteryText = string.Empty;
    private bool _hasTopBarBattery;
    private bool _isTopBarBatteryLow;
    private BluetoothDeviceCategory _topBarDeviceCategory = BluetoothDeviceCategory.Unknown;
    private bool _isTopBarDeviceConnected;
    private bool _isBluetoothMenuOpen;
    private string? _selectedBluetoothDeviceName;
    private CancellationTokenSource? _bluetoothStatusCancellation;
    private long _bluetoothStatusRequestVersion;
    private int _bluetoothStatusRefreshPosted;
    private bool _disposed;
    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;

    public ObservableCollection<BluetoothDeviceItemViewModel> BluetoothDevices { get; } = new();
    public ICommand ToggleBluetoothMenuCommand { get; private set; } = null!;
    public ICommand SelectBluetoothDeviceCommand { get; private set; } = null!;

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
            if (!SetProperty(ref _currentViewModel, value)) return;
            NotifyTopBarPropertiesChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

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

    public string TopBarDeviceName
    {
        get => _topBarDeviceName;
        private set => SetProperty(ref _topBarDeviceName, value);
    }

    public int? TopBarBatteryPercent
    {
        get => _topBarBatteryPercent;
        private set => SetProperty(ref _topBarBatteryPercent, value);
    }

    public string TopBarBatteryText
    {
        get => _topBarBatteryText;
        private set => SetProperty(ref _topBarBatteryText, value);
    }

    public bool HasTopBarBattery
    {
        get => _hasTopBarBattery;
        private set => SetProperty(ref _hasTopBarBattery, value);
    }

    public bool IsTopBarBatteryLow
    {
        get => _isTopBarBatteryLow;
        private set => SetProperty(ref _isTopBarBatteryLow, value);
    }

    public BluetoothDeviceCategory TopBarDeviceCategory
    {
        get => _topBarDeviceCategory;
        private set => SetProperty(ref _topBarDeviceCategory, value);
    }

    public bool IsTopBarDeviceConnected
    {
        get => _isTopBarDeviceConnected;
        private set => SetProperty(ref _isTopBarDeviceConnected, value);
    }

    public bool IsBluetoothMenuOpen
    {
        get => _isBluetoothMenuOpen;
        set
        {
            if (SetProperty(ref _isBluetoothMenuOpen, value) && value)
            {
                _ = RefreshBluetoothDeviceListAsync();
            }
        }
    }

    public string? SelectedBluetoothDeviceName
    {
        get => _selectedBluetoothDeviceName;
        private set
        {
            if (SetProperty(ref _selectedBluetoothDeviceName, value))
            {
                OnPropertyChanged(nameof(IsAutoDeviceSelected));
                OnPropertyChanged(nameof(ShowTopBarDisconnectedTag));
            }
        }
    }

    public bool IsAutoDeviceSelected => string.IsNullOrWhiteSpace(SelectedBluetoothDeviceName);

    public bool ShowTopBarDisconnectedTag =>
        !IsTopBarDeviceConnected && !string.IsNullOrWhiteSpace(SelectedBluetoothDeviceName);

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
    public bool IsCapturingHotkeyInput => CurrentViewModel is HotkeyEditViewModel { IsCapturingHotkey: true };

    public event EventHandler? DirectHotkeysChanged;
    public event EventHandler? DirectHotkeyCaptureStateChanged;

    public AppSettings LoadSettings() => _loadSettingsUseCase.Execute();

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _bluetoothAudioStatusUseCase.StatusInvalidated -= OnBluetoothStatusInvalidated;
        var cancellation = Interlocked.Exchange(ref _bluetoothStatusCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        _bluetoothAudioStatusUseCase.Dispose();
        (_autoBackupCoordinator as IDisposable)?.Dispose();
    }

    public void SaveWindowPlacement(double left, double top, string screenDeviceName)
    {
        if (!WindowPlacementRules.CanSavePlacement(left, top)) return;
        _saveWindowPlacementUseCase.Execute(new SaveWindowPlacementRequest(left, top, screenDeviceName));
    }

    public void ShowHome() => _navigator.ShowHome();

    public void InitializeHome()
    {
        if (CurrentViewModel is not null) return;
        ShowHome();
    }

    public IReadOnlyList<string> GetVisibleThumbnailPaths() => CurrentViewModel switch
    {
        HomeViewModel homeViewModel => GetThumbnailPaths(homeViewModel.NumpadGrid),
        CategoryViewModel categoryViewModel => GetThumbnailPaths(categoryViewModel.NumpadGrid),
        _ => []
    };

    public NumpadGridViewModel? GetVisibleNumpadGrid() => CurrentViewModel switch
    {
        HomeViewModel homeViewModel => homeViewModel.NumpadGrid,
        CategoryViewModel categoryViewModel => categoryViewModel.NumpadGrid,
        _ => null
    };

    public void OpenHomeFromHotkey()
    {
        if (CurrentViewModel is not HomeViewModel)
        {
            ShowHome();
        }

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
            default:
                StatusMessage = resolution.StatusMessage ?? string.Empty;
                break;
        }
    }

    public void ReportHotkeyRegistrationFailure(IReadOnlyList<string> failures)
    {
        if (failures.Count == 0) return;

        StatusMessage = failures.Count switch
        {
            1 => failures[0],
            _ => $"전역 단축키 {failures.Count}개를 등록하지 못했습니다."
        };
        _loggingService?.Log(StatusMessage);
    }

    public IReadOnlyList<DirectHotkeyRegistration> LoadActiveDirectHotkeys() =>
        _loadDirectHotkeyRegistrationsUseCase.Execute();

    public async Task ExecuteDirectHotkeyAsync(Guid hotkeyActionId)
    {
        var action = _resolveExecutableHotkeyActionUseCase.Execute(hotkeyActionId);
        if (action is null) return;

        await _executeActionAsync(action);
    }

    public Task RefreshBluetoothAudioStatusAsync()
    {
        if (_disposed) return Task.CompletedTask;
        return RefreshBluetoothAudioStatusCoreAsync();
    }

    public void ToggleBluetoothMenu() => IsBluetoothMenuOpen = !IsBluetoothMenuOpen;

    public void SelectBluetoothDevice(string? deviceName)
    {
        var targetName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim();
        SelectedBluetoothDeviceName = targetName;
        _bluetoothAudioStatusUseCase.SetPreferredDeviceName(targetName);
        IsBluetoothMenuOpen = false;
        UpdateBluetoothDeviceListSelection();
        _ = RefreshBluetoothAudioStatusAsync();
    }

    public async Task RefreshBluetoothDeviceListAsync()
    {
        try
        {
            var items = await _bluetoothAudioStatusUseCase.GetPairedDevicesAsync();
            if (_disposed) return;

            void UpdateList()
            {
                BluetoothDevices.Clear();
                foreach (var item in items)
                {
                    BluetoothDevices.Add(new BluetoothDeviceItemViewModel(item, IsDeviceSelected(item.DeviceName)));
                }
            }

            if (_synchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
            {
                UpdateList();
            }
            else
            {
                _synchronizationContext.Post(_ => UpdateList(), null);
            }
        }
        catch (Exception ex)
        {
            _loggingService?.Log("페어링된 블루투스 기기 목록 조회 실패", ex);
        }
    }

    private void UpdateBluetoothDeviceListSelection()
    {
        void UpdateSelection()
        {
            for (var i = 0; i < BluetoothDevices.Count; i++)
            {
                var vm = BluetoothDevices[i];
                var isSelected = IsDeviceSelected(vm.DeviceName);
                if (vm.IsSelected != isSelected)
                {
                    BluetoothDevices[i] = new BluetoothDeviceItemViewModel(vm.DeviceItem, isSelected);
                }
            }
        }

        if (_synchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            UpdateSelection();
        }
        else
        {
            _synchronizationContext.Post(_ => UpdateSelection(), null);
        }
    }

    private bool IsDeviceSelected(string? deviceName) =>
        !string.IsNullOrWhiteSpace(SelectedBluetoothDeviceName)
        && (string.Equals(deviceName, SelectedBluetoothDeviceName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                BluetoothAudioStatusRules.NormalizeDeviceName(deviceName),
                BluetoothAudioStatusRules.NormalizeDeviceName(SelectedBluetoothDeviceName),
                StringComparison.OrdinalIgnoreCase));

    private async Task RefreshBluetoothAudioStatusCoreAsync()
    {
        var requestVersion = Interlocked.Increment(ref _bluetoothStatusRequestVersion);
        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref _bluetoothStatusCancellation, cancellation);
        previousCancellation?.Cancel();
        previousCancellation?.Dispose();

        try
        {
            var presentation = await _bluetoothAudioStatusUseCase.GetPresentationAsync(
                SelectedBluetoothDeviceName,
                cancellation.Token);

            if (_disposed
                || cancellation.IsCancellationRequested
                || requestVersion != Interlocked.Read(ref _bluetoothStatusRequestVersion))
            {
                return;
            }

            ApplyBluetoothPresentation(presentation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Newer refresh owns the state.
        }
        catch (Exception ex)
        {
            _loggingService?.Log("블루투스 오디오 상태 조회 실패", ex);
        }
        finally
        {
            Interlocked.CompareExchange(ref _bluetoothStatusCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private void ApplyBluetoothPresentation(BluetoothTopBarPresentation presentation)
    {
        IsTopBarDeviceConnected = presentation.IsConnected;
        TopBarDeviceName = presentation.DeviceName;
        TopBarStatusMessage = presentation.StatusMessage;
        TopBarStatusToolTip = presentation.StatusToolTip;
        TopBarBatteryPercent = presentation.BatteryPercent;
        TopBarBatteryText = presentation.BatteryText;
        HasTopBarBattery = presentation.HasBattery;
        IsTopBarBatteryLow = presentation.IsBatteryLow;
        TopBarDeviceCategory = presentation.DeviceCategory;
        OnPropertyChanged(nameof(ShowTopBarDisconnectedTag));
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

    internal void NotifyDirectHotkeysChanged() =>
        DirectHotkeysChanged?.Invoke(this, EventArgs.Empty);

    internal void NotifyDirectHotkeyCaptureStateChanged() =>
        DirectHotkeyCaptureStateChanged?.Invoke(this, EventArgs.Empty);

    internal void ReportBackgroundStatus(string message) => StatusMessage = message;

    public bool SelectSlot(SlotKey slotKey) => CurrentViewModel switch
    {
        HomeViewModel homeViewModel => homeViewModel.SelectSlot(slotKey),
        CategoryViewModel categoryViewModel => categoryViewModel.SelectSlot(slotKey),
        _ => false
    };

    private void OpenResolvedCategoryFromHotkey(Category category)
    {
        if (CurrentViewModel is CategoryViewModel categoryViewModel
            && categoryViewModel.CategoryId == category.Id)
        {
            return;
        }

        _navigator.OpenCategory(category);
    }

    private static IReadOnlyList<string> GetThumbnailPaths(NumpadGridViewModel grid) =>
        grid.Slots
            .Select(slot => slot.ThumbnailPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => path!)
            .ToList();

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
        _bluetoothAudioStatusUseCase = dependencies.BluetoothAudioStatusUseCase;

        _selectedBluetoothDeviceName = _bluetoothAudioStatusUseCase.GetPreferredDeviceName();

        ToggleBluetoothMenuCommand = new RelayCommand(ToggleBluetoothMenu);
        SelectBluetoothDeviceCommand = new RelayCommand<string?>(SelectBluetoothDevice);

        var viewFactory = new MainViewModelViewFactory(
            dependencies.NavigatorDependencies,
            snippet => dependencies.ExecuteActionAsync(ExecutableAction.FromSnippet(snippet)),
            msg => StatusMessage = msg);
        _executeActionAsync = dependencies.ExecuteActionAsync;

        _navigator = new MainViewModelNavigator(
            dependencies.NavigatorDependencies,
            viewFactory,
            viewModel => CurrentViewModel = viewModel,
            msg => StatusMessage = msg,
            callbacks.EnterEditMode,
            NotifyDirectHotkeysChanged,
            NotifyDirectHotkeyCaptureStateChanged);

        _bluetoothAudioStatusUseCase.StatusInvalidated += OnBluetoothStatusInvalidated;
        _bluetoothAudioStatusUseCase.StartMonitoring();
        _ = RefreshBluetoothAudioStatusAsync();
    }
}
