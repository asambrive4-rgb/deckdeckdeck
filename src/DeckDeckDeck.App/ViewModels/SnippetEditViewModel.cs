// 역할: 텍스트 스니펫 및 동작(URL, 파일, 앱, 미디어, 블루투스, Android 원격설치 등) 편집 화면의 입력 필드와 프리셋 설정을 관리하는 화면 모델입니다.
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SnippetEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Snippet> _afterSave;
    private readonly Action _cancel;
    private readonly DeleteSnippetUseCase _deleteSnippetUseCase;
    private readonly IDialogAdapter _dialogService;
    private readonly ExecutableActionEditDraft _draft;
    private readonly IAppLogger? _loggingService;
    private readonly IAndroidRemoteInstallGateway? _androidRemoteInstallGateway;
    private readonly string? _defaultRemoteInstallProjectPath;
    private readonly bool _isSpotifyConnected;
    private bool _originalIsSlotEnabled;
    private readonly SaveSnippetUseCase _saveSnippetUseCase;
    private readonly Guid? _snippetId;
    private readonly Action<string> _showStatus;
    private readonly TransferSnippetUseCase _transferSnippetUseCase;
    private const string TurnOffDisplayDefaultDescription = "모니터 화면을 끕니다. 핫스팟과 백그라운드 작업은 유지됩니다.";
    private string _errorMessage = string.Empty;
    private bool _showAdvancedTerminalSettings;
    private bool _showAdvancedRemoteInstallSettings;
    private string _remoteInstallConnectedDevicesSummary = "연결 기기 새로고침을 눌러 ADB 분류 결과를 확인하세요.";
    private IReadOnlyList<AndroidAppModuleOption> _remoteInstallModuleOptions = [];
    private BluetoothAudioActionKind _bluetoothControlAction = BluetoothAudioActionKind.Connect;
    private string _bluetoothTarget = string.Empty;
    private bool _isBluetoothPresetSelected;
    private bool _hasAutoFilledTitle;
    private bool _hasAutoFilledDescription;
    private bool _isSlotEnabled;
    private IReadOnlyList<SnippetMediaCommandOption> _mediaCommandOptions;
    private SnippetTransferTargetSlot? _selectedTransferTarget;

    public SnippetEditViewModel(
        Category category,
        SlotKey slotKey,
        Snippet? snippet,
        SnippetEditorState editorState,
        SaveSnippetUseCase saveSnippetUseCase,
        DeleteSnippetUseCase deleteSnippetUseCase,
        TransferSnippetUseCase transferSnippetUseCase,
        IDialogAdapter dialogService,
        Action cancel,
        Action<Snippet> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        IImageFileRepository? thumbnailService = null,
        IAppLogger? loggingService = null,
        ISnippetImageResolver? snippetImageService = null,
        IStoredImagePathResolver? storedImagePathResolver = null,
        IAndroidRemoteInstallGateway? androidRemoteInstallGateway = null)
    {
        CategoryId = category.Id;
        CategoryName = category.Name;
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _snippetId = snippet?.Id;
        _saveSnippetUseCase = saveSnippetUseCase;
        _deleteSnippetUseCase = deleteSnippetUseCase;
        _transferSnippetUseCase = transferSnippetUseCase;
        _dialogService = dialogService;
        _loggingService = loggingService;
        _androidRemoteInstallGateway = androidRemoteInstallGateway;
        _defaultRemoteInstallProjectPath = editorState.DefaultRemoteInstallProjectPath;
        CategoryAdbIpHints = editorState.AdbSlotIpHints ?? [];
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;
        _draft = ExecutableActionEditDraft.FromSnippet(
            snippet,
            thumbnailService,
            snippetImageService,
            storedImagePathResolver);
        if (snippet is null)
        {
            _draft.RemoteInstallDeviceKind = RemoteInstallRules.GetDefaultDeviceKindForSlot(slotKey);
        }
        _isSpotifyConnected = editorState.SpotifyConnection.IsConnected;

        _mediaCommandOptions = SnippetMediaCommandOption.ForProvider(_draft.MediaProvider);
        TransferTargetSlots = SlotEditCommon.BuildTransferTargetSlots(editorState.TransferTargets)
            .Select(t => new SnippetTransferTargetSlot(t.SlotKey, t.Label))
            .ToList();
        _selectedTransferTarget = TransferTargetSlots.FirstOrDefault();
        _originalIsSlotEnabled = editorState.IsSlotEnabled;
        _isSlotEnabled = _originalIsSlotEnabled;

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        CopySnippetCommand = new RelayCommand(CopySnippet);
        MoveSnippetCommand = new RelayCommand(MoveSnippet);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
        ChooseLaunchFileCommand = new RelayCommand(ChooseLaunchFile);
        ChooseLaunchFolderCommand = new RelayCommand(ChooseLaunchFolder);
        ChooseInstalledAppCommand = new RelayCommand(ChooseInstalledApp);
        ChooseTerminalWorkingDirectoryCommand = new RelayCommand(ChooseTerminalWorkingDirectory);
        ChooseBluetoothDeviceCommand = new RelayCommand(ChooseBluetoothDevice);
        ToggleAdvancedTerminalSettingsCommand = new RelayCommand(ToggleAdvancedTerminalSettings);
        SelectRemoteInstallTabletCommand = new RelayCommand(() => ToggleRemoteInstallDeviceKind(RemoteInstallDeviceKind.Tablet));
        SelectRemoteInstallPhoneCommand = new RelayCommand(() => ToggleRemoteInstallDeviceKind(RemoteInstallDeviceKind.Phone));
        SelectRemoteInstallWatchCommand = new RelayCommand(() => ToggleRemoteInstallDeviceKind(RemoteInstallDeviceKind.Watch));
        SelectRemoteInstallTabletPhoneComboCommand = new RelayCommand(() => SetRemoteInstallDeviceKind(RemoteInstallDeviceKind.Tablet | RemoteInstallDeviceKind.Phone));
        SelectRemoteInstallAllKindsCommand = new RelayCommand(() => SetRemoteInstallDeviceKind(RemoteInstallDeviceKind.Tablet | RemoteInstallDeviceKind.Phone | RemoteInstallDeviceKind.Watch));
        SelectFixedRemoteInstallProjectModeCommand = new RelayCommand(() => RemoteInstallPromptProject = false);
        SelectPromptRemoteInstallProjectModeCommand = new RelayCommand(() => RemoteInstallPromptProject = true);
        ChooseRemoteInstallProjectFolderCommand = new RelayCommand(ChooseRemoteInstallProjectFolder);
        RescanRemoteInstallModulesCommand = new RelayCommand(() => RefreshRemoteInstallModules(autoRecommendModule: false));
        RefreshRemoteInstallDevicesCommand = new RelayCommand(() => _ = RefreshRemoteInstallDevicesAsync());
        ToggleAdvancedRemoteInstallSettingsCommand = new RelayCommand(ToggleAdvancedRemoteInstallSettings);
        ApplyAdbIpHintCommand = new RelayCommand<string?>(ApplyAdbIpHint);
        ApplyBatch456RemoteInstallSlotsCommand = new RelayCommand(ApplyBatch456RemoteInstallSlots);
        if (TerminalCommandParameterRules.TryParseBluetoothAudioCommand(_draft.TerminalCommand, out var parsedBt, allowEmptyTarget: true))
        {
            _isBluetoothPresetSelected = true;
            _bluetoothControlAction = parsedBt.Action;
            _bluetoothTarget = parsedBt.Target;
        }

        if (IsRemoteInstallMode)
        {
            if (!RemoteInstallPromptProject)
            {
                RefreshRemoteInstallModules(autoRecommendModule: string.IsNullOrWhiteSpace(_draft.RemoteInstallModule));
            }
            _ = RefreshRemoteInstallDevicesAsync();
        }
    }

    public string Title => IsExisting ? "실행 항목 편집" : "새 실행 항목";
    public Guid CategoryId { get; }
    public string CategoryName { get; }
    public SlotKey SlotKey { get; }
    public string KeyText { get; }
    public bool IsExisting => _snippetId.HasValue;
    public bool CanDelete => IsExisting;
    public bool CanTransfer => IsExisting;

    public IReadOnlyList<SnippetTransferTargetSlot> TransferTargetSlots { get; }
    public IReadOnlyList<SnippetMediaProviderOption> MediaProviderOptions => SnippetMediaProviderOption.All;
    public IReadOnlyList<PasteShortcutModeOption> PasteShortcutModeOptions => PasteShortcutModeOption.All;
    public IReadOnlyList<TerminalShellOption> TerminalShellOptions => TerminalShellOption.All;

    public IReadOnlyList<SnippetMediaCommandOption> MediaCommandOptions
    {
        get => _mediaCommandOptions;
        private set => SetProperty(ref _mediaCommandOptions, value);
    }

    public SnippetTransferTargetSlot? SelectedTransferTarget
    {
        get => _selectedTransferTarget;
        set => SetProperty(ref _selectedTransferTarget, value);
    }

    public string SnippetTitle
    {
        get => _draft.Title;
        set
        {
            if (SetDraftValue(_draft.Title, value, static (draft, v) => draft.Title = v))
            {
                _hasAutoFilledTitle = false;
                NotifyDetectedAudioIconChanged();
            }
        }
    }

    public string Content
    {
        get => _draft.Content;
        set => SetDraftValue(_draft.Content, value, static (draft, v) => draft.Content = v);
    }

    public PasteShortcutMode PasteShortcutMode
    {
        get => _draft.PasteShortcutMode;
        set => SetDraftValue(_draft.PasteShortcutMode, value, static (draft, v) => draft.PasteShortcutMode = v);
    }

    public SnippetActionType ActionType
    {
        get => _draft.ActionType;
        private set
        {
            if (_draft.ActionType == value) return;

            _draft.SetActionType(value);
            OnPropertyChanged();
            NotifyActionModeTransition();
        }
    }

    public ActionEditorPanel EditorPanel => _draft.EditorPanel;

    public bool IsPasteAction
    {
        get => EditorPanel == ActionEditorPanel.Paste;
        set { if (value && !IsPasteAction) ActivatePasteTextMode(); }
    }

    public bool IsOpenLaunchAction
    {
        get => EditorPanel == ActionEditorPanel.OpenLaunch;
        set { if (value && !IsOpenLaunchAction) ActivateOpenFileMode(); }
    }

    public bool IsMediaAction
    {
        get => EditorPanel == ActionEditorPanel.Media;
        set { if (value && !IsMediaAction) ActivateMediaMode(); }
    }

    public bool IsSystemUtilityAction
    {
        get => EditorPanel == ActionEditorPanel.SystemUtility;
        set { if (value && !IsSystemUtilityAction) ActivateTurnOffDisplayMode(); }
    }

    public bool IsTerminalAction
    {
        get => EditorPanel == ActionEditorPanel.Terminal;
        set { if (value && !IsTerminalAction) ActivateCustomTerminalMode(); }
    }

    public bool IsAndroidAction
    {
        get => EditorPanel == ActionEditorPanel.Android;
        set { if (value && !IsAndroidAction) ActivateAdbConnectMode(); }
    }

    public string LaunchPath
    {
        get => _draft.LaunchPath;
        set
        {
            if (_draft.LaunchPath == value) return;
            _draft.SetLaunchPath(value);
            OnPropertyChanged();
            NotifyImageChanged();
        }
    }

    public FileActionMode SelectedFileActionMode
    {
        get => _draft.FileActionMode;
        set
        {
            if (_draft.FileActionMode == value) return;
            _draft.SetFileActionMode(value);
            OnPropertyChanged();
            NotifyActionModeTransition();
        }
    }

    public bool IsPasteTextMode
    {
        get => ActionType == SnippetActionType.PasteText;
        set { if (value) ActivatePasteTextMode(); }
    }

    public bool IsFilePasteMode
    {
        get => ActionType == SnippetActionType.LaunchFile && SelectedFileActionMode == FileActionMode.Paste;
        set { if (value) ActivatePasteFileMode(); }
    }

    public bool IsFileLaunchMode
    {
        get => ActionType == SnippetActionType.LaunchFile && SelectedFileActionMode == FileActionMode.Launch;
        set { if (value) ActivateOpenFileMode(); }
    }

    public bool IsLaunchUrlMode
    {
        get => ActionType == SnippetActionType.LaunchUrl;
        set { if (value) ActivateOpenUrlMode(); }
    }

    public bool IsTimerMode
    {
        get => ActionType == SnippetActionType.Timer;
        set { if (value) ActivateTimerMode(); }
    }

    public bool IsRemoteInstallMode
    {
        get => ActionType == SnippetActionType.RemoteInstall;
        set { if (value) ActivateRemoteInstallAction(); }
    }

    public string FilePathLabel => IsFilePasteMode ? "붙여넣을 파일" : "실행할 파일, 폴더 또는 바로 가기";
    public string FilePickerButtonText => IsFilePasteMode ? "파일 선택" : "파일/바로 가기 선택";

    public string LaunchUrl
    {
        get => _draft.LaunchUrl;
        set => SetDraftValue(_draft.LaunchUrl, value, static (draft, v) => draft.SetLaunchUrl(v));
    }

    public SnippetMediaProvider SelectedMediaProvider
    {
        get => _draft.MediaProvider;
        set
        {
            if (_draft.MediaProvider == value) return;
            _draft.SetMediaProvider(value);
            OnPropertyChanged();
            MediaCommandOptions = SnippetMediaCommandOption.ForProvider(value);
            OnPropertyChanged(nameof(SelectedMediaCommand));
            OnPropertyChanged(nameof(IsSpotifyMediaProvider));
            OnPropertyChanged(nameof(ShowSpotifyMediaConnectionNotice));
            NotifyImageChanged();
        }
    }

    public SnippetMediaCommand SelectedMediaCommand
    {
        get => _draft.MediaCommand;
        set
        {
            if (_draft.MediaCommand == value) return;
            _draft.SetMediaCommand(value);
            OnPropertyChanged();
            NotifyImageChanged();
        }
    }

    public bool IsSpotifyMediaProvider => SelectedMediaProvider == SnippetMediaProvider.Spotify;
    public bool ShowSpotifyMediaConnectionNotice => IsMediaAction && IsSpotifyMediaProvider && !_isSpotifyConnected;
    public string SpotifyMediaConnectionNotice => "Spotify 연결 전에도 저장할 수 있습니다. 실행하려면 설정에서 Spotify를 연결해 주세요.";

    public string TerminalCommand
    {
        get => _draft.TerminalCommand;
        set
        {
            if (!SetDraftValue(_draft.TerminalCommand, value, static (draft, v) => draft.TerminalCommand = v))
            {
                return;
            }

            if (TerminalCommandParameterRules.TryParseBluetoothAudioCommand(value, out var btCmd, allowEmptyTarget: true))
            {
                _isBluetoothPresetSelected = true;
                _bluetoothControlAction = btCmd.Action;
                _bluetoothTarget = btCmd.Target;
                OnPropertyChanged(nameof(IsBluetoothConnectAction));
                OnPropertyChanged(nameof(IsBluetoothDisconnectAction));
                OnPropertyChanged(nameof(BluetoothTarget));
            }
            else
            {
                _isBluetoothPresetSelected = false;
            }

            NotifyActionModeTransition();
        }
    }

    public bool ShowAdvancedTerminalSettings
    {
        get => _showAdvancedTerminalSettings || IsCustomTerminalCommandActive;
        set
        {
            if (_showAdvancedTerminalSettings == value) return;
            _showAdvancedTerminalSettings = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowTerminalCommandEditor));
        }
    }

    public bool IsTurnOffDisplayPresetActive
    {
        get => ActionType == SnippetActionType.TerminalCommand
            && TerminalCommandParameterRules.IsTurnOffDisplayCommand(TerminalCommand);
        set { if (value) ActivateTurnOffDisplayMode(); }
    }

    public bool IsAdbPairingPresetActive
    {
        get => ActionType == SnippetActionType.TerminalCommand
            && TerminalCommandParameterRules.IsAdbWirelessConnectCommand(TerminalCommand);
        set { if (value) ActivateAdbConnectMode(); }
    }

    public bool IsBluetoothPresetActive
    {
        get => ActionType == SnippetActionType.TerminalCommand
            && (_isBluetoothPresetSelected
                || TerminalCommandParameterRules.TryParseBluetoothAudioCommand(TerminalCommand, out _, allowEmptyTarget: true));
        set { if (value) ActivateBluetoothMode(); }
    }

    public bool IsCustomTerminalCommandActive =>
        ActionType == SnippetActionType.TerminalCommand
        && !IsTurnOffDisplayPresetActive
        && !IsAdbPairingPresetActive
        && !IsBluetoothPresetActive;

    public bool ShowTerminalCommandEditor =>
        ActionType == SnippetActionType.TerminalCommand && ShowAdvancedTerminalSettings;

    public BluetoothAudioActionKind BluetoothControlAction
    {
        get => _bluetoothControlAction;
        set
        {
            if (_bluetoothControlAction == value) return;
            _bluetoothControlAction = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBluetoothConnectAction));
            OnPropertyChanged(nameof(IsBluetoothDisconnectAction));
            if (IsBluetoothPresetActive) SyncBluetoothTerminalCommand();
            NotifyDetectedAudioIconChanged();
        }
    }

    public bool IsBluetoothConnectAction
    {
        get => BluetoothControlAction == BluetoothAudioActionKind.Connect;
        set { if (value) BluetoothControlAction = BluetoothAudioActionKind.Connect; }
    }

    public bool IsBluetoothDisconnectAction
    {
        get => BluetoothControlAction == BluetoothAudioActionKind.Disconnect;
        set { if (value) BluetoothControlAction = BluetoothAudioActionKind.Disconnect; }
    }

    public string BluetoothTarget
    {
        get => _bluetoothTarget;
        set
        {
            if (_bluetoothTarget == value) return;
            _bluetoothTarget = value;
            OnPropertyChanged();
            if (IsBluetoothPresetActive) SyncBluetoothTerminalCommand();
            NotifyDetectedAudioIconChanged();
        }
    }

    public string DetectedAudioDeviceIconResourcePath =>
        AudioPresetIconResolver.GetAutoIconResourcePath(
            TerminalCommandParameterRules.BuildBluetoothAudioCommand(BluetoothControlAction, BluetoothTarget),
            SnippetTitle) ?? string.Empty;

    public bool HasDetectedAudioDeviceIcon => !string.IsNullOrWhiteSpace(DetectedAudioDeviceIconResourcePath);

    private void NotifyDetectedAudioIconChanged()
    {
        OnPropertyChanged(nameof(DetectedAudioDeviceIconResourcePath));
        OnPropertyChanged(nameof(HasDetectedAudioDeviceIcon));
    }

    public IRelayCommand ChooseBluetoothDeviceCommand { get; }
    public IRelayCommand ToggleAdvancedTerminalSettingsCommand { get; }
    public IRelayCommand SelectRemoteInstallTabletCommand { get; }
    public IRelayCommand SelectRemoteInstallPhoneCommand { get; }
    public IRelayCommand SelectRemoteInstallWatchCommand { get; }
    public IRelayCommand SelectRemoteInstallTabletPhoneComboCommand { get; }
    public IRelayCommand SelectRemoteInstallAllKindsCommand { get; }
    public IRelayCommand SelectFixedRemoteInstallProjectModeCommand { get; }
    public IRelayCommand SelectPromptRemoteInstallProjectModeCommand { get; }
    public IRelayCommand ChooseRemoteInstallProjectFolderCommand { get; }
    public IRelayCommand RescanRemoteInstallModulesCommand { get; }
    public IRelayCommand RefreshRemoteInstallDevicesCommand { get; }
    public IRelayCommand ToggleAdvancedRemoteInstallSettingsCommand { get; }
    public IRelayCommand<string?> ApplyAdbIpHintCommand { get; }
    public IRelayCommand ApplyBatch456RemoteInstallSlotsCommand { get; }

    public IReadOnlyList<AdbSlotIpHint> CategoryAdbIpHints { get; }
    public bool HasCategoryAdbIpHints => CategoryAdbIpHints.Count > 0;

    public IReadOnlyList<AndroidAppModuleOption> RemoteInstallModuleOptions
    {
        get => _remoteInstallModuleOptions;
        private set
        {
            if (SetProperty(ref _remoteInstallModuleOptions, value))
            {
                OnPropertyChanged(nameof(HasRemoteInstallModuleOptions));
            }
        }
    }

    public bool HasRemoteInstallModuleOptions => _remoteInstallModuleOptions.Count > 0;

    public RemoteInstallDeviceKind RemoteInstallDeviceKind
    {
        get => _draft.RemoteInstallDeviceKind;
        set
        {
            if (!SetDraftValue(_draft.RemoteInstallDeviceKind, value, static (draft, v) => draft.RemoteInstallDeviceKind = v))
            {
                return;
            }

            OnPropertyChanged(nameof(IsRemoteInstallTabletSelected));
            OnPropertyChanged(nameof(IsRemoteInstallPhoneSelected));
            OnPropertyChanged(nameof(IsRemoteInstallWatchSelected));
            OnPropertyChanged(nameof(IsRemoteInstallTabletPhoneComboSelected));
            OnPropertyChanged(nameof(IsRemoteInstallAllKindsSelected));
            OnPropertyChanged(nameof(RemoteInstallDeviceKindSelectionSummary));
        }
    }

    public bool IsRemoteInstallTabletSelected => RemoteInstallDeviceKind.HasFlag(RemoteInstallDeviceKind.Tablet);
    public bool IsRemoteInstallPhoneSelected => RemoteInstallDeviceKind.HasFlag(RemoteInstallDeviceKind.Phone);
    public bool IsRemoteInstallWatchSelected => RemoteInstallDeviceKind.HasFlag(RemoteInstallDeviceKind.Watch);
    public bool IsRemoteInstallTabletPhoneComboSelected =>
        RemoteInstallDeviceKind == (RemoteInstallDeviceKind.Tablet | RemoteInstallDeviceKind.Phone);
    public bool IsRemoteInstallAllKindsSelected =>
        RemoteInstallDeviceKind == (RemoteInstallDeviceKind.Tablet | RemoteInstallDeviceKind.Phone | RemoteInstallDeviceKind.Watch);

    public string RemoteInstallDeviceKindSelectionSummary
    {
        get
        {
            var count = RemoteInstallRules.SplitSelectedDeviceKinds(RemoteInstallDeviceKind).Count;
            var label = RemoteInstallRules.GetDeviceKindKoreanLabel(RemoteInstallDeviceKind);
            return count <= 1
                ? $"선택됨: {label} (다른 기기 버튼을 추가로 눌러 동시 설치 가능)"
                : $"선택됨: {label} ({count}종 동시 설치 · 1회 빌드 후 연결된 기기에 병렬 설치)";
        }
    }

    public string RemoteInstallProjectPath
    {
        get => _draft.RemoteInstallProjectPath;
        set
        {
            if (SetDraftValue(_draft.RemoteInstallProjectPath, value, static (draft, v) => draft.RemoteInstallProjectPath = v))
            {
                RefreshRemoteInstallModules(autoRecommendModule: true);
            }
        }
    }

    public string RemoteInstallModule
    {
        get => _draft.RemoteInstallModule;
        set => SetDraftValue(_draft.RemoteInstallModule, value, static (draft, v) => draft.RemoteInstallModule = v);
    }

    public string RemoteInstallTargetDevice
    {
        get => _draft.RemoteInstallTargetDevice;
        set => SetDraftValue(_draft.RemoteInstallTargetDevice, value, static (draft, v) => draft.RemoteInstallTargetDevice = v);
    }

    public bool RemoteInstallLaunchAfterInstall
    {
        get => _draft.RemoteInstallLaunchAfterInstall;
        set => SetDraftValue(_draft.RemoteInstallLaunchAfterInstall, value, static (draft, v) => draft.RemoteInstallLaunchAfterInstall = v);
    }

    public bool RemoteInstallPromptProject
    {
        get => _draft.RemoteInstallPromptProject;
        set
        {
            if (!SetDraftValue(_draft.RemoteInstallPromptProject, value, static (draft, v) => draft.RemoteInstallPromptProject = v))
            {
                return;
            }

            OnPropertyChanged(nameof(IsFixedRemoteInstallProjectMode));
            OnPropertyChanged(nameof(Batch456ButtonText));
            OnPropertyChanged(nameof(Batch456ButtonToolTip));

            if (!value)
            {
                if (string.IsNullOrWhiteSpace(RemoteInstallProjectPath))
                {
                    var candidatePath = ResolveInitialRemoteInstallProjectPath();
                    if (!string.IsNullOrWhiteSpace(candidatePath))
                    {
                        RemoteInstallProjectPath = candidatePath;
                    }
                }
                else
                {
                    RefreshRemoteInstallModules(autoRecommendModule: string.IsNullOrWhiteSpace(RemoteInstallModule));
                }
            }

            if (RemoteInstallRules.IsAutoGeneratedSlotTitle(SnippetTitle))
            {
                SnippetTitle = RemoteInstallRules.RecommendDefaultSlotTitle(
                    RemoteInstallProjectPath,
                    RemoteInstallDeviceKind,
                    value);
            }

            ErrorMessage = string.Empty;
        }
    }

    public bool IsFixedRemoteInstallProjectMode => !RemoteInstallPromptProject;

    public string Batch456ButtonText => RemoteInstallPromptProject
        ? "슬롯 4·5·6 일괄 배치 (실행 시 선택) ⚡"
        : "슬롯 4·5·6 한 번에 배치 ⚡";

    public string Batch456ButtonToolTip => RemoteInstallPromptProject
        ? "실행 시 프로젝트 선택 모드로 이 카테고리의 슬롯 4(태블릿)·5(폰)·6(워치)을 한 번에 저장합니다."
        : "현재 프로젝트 기준으로 이 카테고리의 슬롯 4(태블릿)·5(폰)·6(워치)을 한 번에 저장합니다.";

    public bool ShowAdvancedRemoteInstallSettings
    {
        get => _showAdvancedRemoteInstallSettings || !string.IsNullOrWhiteSpace(RemoteInstallTargetDevice);
        set => SetProperty(ref _showAdvancedRemoteInstallSettings, value);
    }

    public string RemoteInstallConnectedDevicesSummary
    {
        get => _remoteInstallConnectedDevicesSummary;
        private set => SetProperty(ref _remoteInstallConnectedDevicesSummary, value);
    }

    public SnippetTerminalShell SelectedTerminalShell
    {
        get => _draft.TerminalShell;
        set => SetDraftValue(_draft.TerminalShell, value, static (draft, v) => draft.TerminalShell = v);
    }

    public bool RunAsAdministrator
    {
        get => _draft.RunAsAdministrator;
        set => SetDraftValue(_draft.RunAsAdministrator, value, static (draft, v) => draft.RunAsAdministrator = v);
    }

    public bool OpenTerminalWindow
    {
        get => _draft.OpenTerminalWindow;
        set => SetDraftValue(_draft.OpenTerminalWindow, value, static (draft, v) => draft.OpenTerminalWindow = v);
    }

    public string TerminalWorkingDirectory
    {
        get => _draft.TerminalWorkingDirectory;
        set => SetDraftValue(_draft.TerminalWorkingDirectory, value, static (draft, v) => draft.TerminalWorkingDirectory = v);
    }

    public string AdbDeviceIp
    {
        get => _draft.AdbDeviceIp;
        set => SetDraftValue(_draft.AdbDeviceIp, value, static (draft, v) => draft.AdbDeviceIp = v);
    }

    public string Description
    {
        get => _draft.Description;
        set
        {
            if (SetDraftValue(_draft.Description, value, static (draft, v) => draft.Description = v))
            {
                _hasAutoFilledDescription = false;
            }
        }
    }

    public bool IsSlotEnabled
    {
        get => _isSlotEnabled;
        set => SetProperty(ref _isSlotEnabled, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? ThumbnailPath => _draft.GetPreviewThumbnailPath();
    public bool HasImage => _draft.HasImage;
    public SlotImageMode SlotImageMode => _draft.SlotImageMode;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CopySnippetCommand { get; }
    public ICommand MoveSnippetCommand { get; }
    public ICommand ChooseImageCommand { get; }
    public ICommand RemoveImageCommand { get; }
    public ICommand ChooseLaunchFileCommand { get; }
    public ICommand ChooseLaunchFolderCommand { get; }
    public ICommand ChooseInstalledAppCommand { get; }
    public ICommand ChooseTerminalWorkingDirectoryCommand { get; }

    public void DropImageFiles(IReadOnlyList<string> sourcePaths)
    {
        if (SlotEditCommon.TryDropSingleImage(sourcePaths, ReplaceImageFromPath, out var error)) return;

        ErrorMessage = error ?? string.Empty;
    }

    private void Save()
    {
        var autoIcon = _draft.PrepareAutoIconForSave();
        NotifyImageChanged();

        var result = _saveSnippetUseCase.Execute(BuildSaveRequest(autoIcon));
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage ?? string.Empty;
            return;
        }

        if (result.SavedSlotOnly)
        {
            _originalIsSlotEnabled = IsSlotEnabled;
            _draft.DeleteCurrentUnsavedImage();
            _showStatus($"슬롯 {KeyText} 설정을 저장했습니다.");
            _afterDelete();
            return;
        }

        if (ActionType == SnippetActionType.LaunchUrl && !string.IsNullOrWhiteSpace(result.NormalizedLaunchUrl))
        {
            ApplyNormalizedLaunchUrl(result.NormalizedLaunchUrl);
        }

        var snippet = result.Snippet!;
        _draft.MarkSaved();
        _originalIsSlotEnabled = IsSlotEnabled;
        ErrorMessage = string.Empty;
        _showStatus($"{snippet.Title} 저장됨.");
        _afterSave(snippet);
    }

    private SaveSnippetRequest BuildSaveRequest(AutoIconCacheEntry? autoIcon) =>
        new(CategoryId, SlotKey, _snippetId, IsSlotEnabled, _originalIsSlotEnabled, _draft.ToSnippetSaveData(autoIcon));

    private void Cancel()
    {
        _draft.DeleteCurrentUnsavedImage();
        _cancel();
    }

    private void Delete()
    {
        if (!_snippetId.HasValue) return;
        if (!_dialogService.Confirm("실행 항목 삭제", "이 실행 항목을 삭제할까요?")) return;

        _draft.DeleteCurrentUnsavedImage();
        _deleteSnippetUseCase.Execute(_snippetId.Value);
        _showStatus("실행 항목을 삭제했습니다.");
        _afterDelete();
    }

    private void CopySnippet() => TransferSnippet(SnippetTransferOperation.Copy);

    private void MoveSnippet() => TransferSnippet(SnippetTransferOperation.Move);

    private void TransferSnippet(SnippetTransferOperation operation)
    {
        if (!_snippetId.HasValue)
        {
            ErrorMessage = "저장된 실행 항목만 복사하거나 이동할 수 있습니다.";
            return;
        }

        if (SelectedTransferTarget is null)
        {
            ErrorMessage = "대상 슬롯을 선택해 주세요.";
            return;
        }

        var isCopy = operation == SnippetTransferOperation.Copy;
        var (actionText, particle) = SlotEditCommon.GetTransferText(isCopy);
        var targetSlotKey = SelectedTransferTarget.SlotKey;
        var autoIcon = _draft.PrepareAutoIconForSave();
        NotifyImageChanged();

        SlotEditCommon.ExecuteTransferWithConfirmation(
            _dialogService,
            _loggingService,
            SlotKey,
            "실행 항목",
            isCopy,
            overwriteConfirmed => ExecuteTransferSnippet(operation, targetSlotKey, autoIcon, overwriteConfirmed),
            r => (r.NeedsOverwriteConfirmation, r.ExistingTargetTitle, r.Succeeded, r.ErrorMessage),
            existing => $"슬롯 {targetSlotKey.GetDisplayText()}에 이미 '{existing}' 실행 항목이 있습니다.\n기존 실행 항목을 덮어쓸까요?",
            result =>
            {
                if (ActionType == SnippetActionType.LaunchUrl && !string.IsNullOrWhiteSpace(result.NormalizedLaunchUrl))
                {
                    ApplyNormalizedLaunchUrl(result.NormalizedLaunchUrl);
                }

                _draft.MarkSaved();
                _originalIsSlotEnabled = IsSlotEnabled;
                _afterSave(result.Snippet!);
                _showStatus($"슬롯 {targetSlotKey.GetDisplayText()}{particle} 실행 항목을 {actionText}했습니다.");
            },
            msg => ErrorMessage = msg);
    }

    private TransferSnippetResult ExecuteTransferSnippet(
        SnippetTransferOperation operation,
        SlotKey targetSlotKey,
        AutoIconCacheEntry? autoIcon,
        bool overwriteConfirmed) =>
        _transferSnippetUseCase.Execute(new TransferSnippetRequest(
            CategoryId,
            _snippetId,
            SlotKey,
            targetSlotKey,
            IsSlotEnabled,
            operation,
            BuildSaveRequest(autoIcon),
            overwriteConfirmed));

    private void ChooseImage()
    {
        if (SlotEditCommon.TryChooseImage(_draft.CanStoreImages, _dialogService, ReplaceImageFromPath, out var error)) return;
        if (error is not null) ErrorMessage = error;
    }

    private void ReplaceImageFromPath(string sourcePath)
    {
        try
        {
            _draft.ReplaceImageFromPath(sourcePath);
            NotifyImageChanged();
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _loggingService?.Log($"Image processing failed for snippet slot {SlotKey}.", ex);
        }
    }

    private void RemoveImage()
    {
        _draft.RemoveImage();
        NotifyImageChanged();
    }

    private void ChooseLaunchFile()
    {
        var selectedPath = IsFilePasteMode ? _dialogService.SelectPasteFile() : _dialogService.SelectLaunchFile();
        if (selectedPath is null) return;

        LaunchPath = selectedPath;
        ErrorMessage = string.Empty;
    }

    private void ChooseInstalledApp()
    {
        var selected = _dialogService.SelectInstalledApp();
        if (selected is null) return;

        LaunchPath = selected.LaunchPath;
        if (string.IsNullOrWhiteSpace(SnippetTitle) || SnippetTitle == "새 실행 항목")
        {
            SnippetTitle = selected.DisplayName;
        }

        ErrorMessage = string.Empty;
    }

    private void ChooseLaunchFolder()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null) return;

        _draft.SetLaunchFolderPath(selectedPath);
        OnPropertyChanged(nameof(LaunchPath));
        ErrorMessage = string.Empty;
        NotifyImageChanged();
    }

    private void ChooseTerminalWorkingDirectory()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null) return;

        TerminalWorkingDirectory = selectedPath;
        ErrorMessage = string.Empty;
    }

    private void ActivatePasteTextMode() => SwitchActionMode(_draft.ActivatePasteText);

    private void ActivatePasteFileMode() => SwitchActionMode(_draft.ActivatePasteFile);

    private void ActivateOpenFileMode() => SwitchActionMode(_draft.ActivateOpenFile);

    private void ActivateOpenUrlMode() => SwitchActionMode(_draft.ActivateOpenUrl);

    private void ActivateMediaMode() => SwitchActionMode(_draft.ActivateMedia);

    private void ActivateTurnOffDisplayMode()
    {
        SwitchActionMode(() =>
        {
            _isBluetoothPresetSelected = false;
            _showAdvancedTerminalSettings = false;
            _draft.ActivateTurnOffDisplay();
            if (CanApplyAutoTitle())
            {
                ApplyAutoTitle("화면 끄기");
            }

            if (CanApplyAutoDescription())
            {
                ApplyAutoDescription(TurnOffDisplayDefaultDescription);
            }
        });
    }

    private void ActivateBluetoothMode()
    {
        SwitchActionMode(() =>
        {
            _isBluetoothPresetSelected = true;
            _showAdvancedTerminalSettings = false;
            _draft.ActivateBluetoothControl(_bluetoothControlAction, _bluetoothTarget);
        });
    }

    private void ActivateTimerMode()
    {
        SwitchActionMode(() =>
        {
            _isBluetoothPresetSelected = false;
            _draft.ActivateTimer();
            if (CanApplyAutoTitle())
            {
                ApplyAutoTitle("타이머");
            }
        });
    }

    private void ActivateCustomTerminalMode()
    {
        SwitchActionMode(() =>
        {
            _isBluetoothPresetSelected = false;
            _showAdvancedTerminalSettings = true;
            _draft.ActivateCustomTerminalCommand();
        });
    }

    private void ActivateAdbConnectMode()
    {
        SwitchActionMode(() =>
        {
            _isBluetoothPresetSelected = false;
            _showAdvancedTerminalSettings = false;
            _draft.ActivateAdbConnect();
        });
    }

    private void ActivateRemoteInstallAction()
    {
        ClearUntouchedAutoFilledPresetCopy();
        _isBluetoothPresetSelected = false;
        _draft.ActivateRemoteInstall();

        if (!RemoteInstallPromptProject && string.IsNullOrWhiteSpace(RemoteInstallProjectPath))
        {
            var candidatePath = ResolveInitialRemoteInstallProjectPath();
            if (!string.IsNullOrWhiteSpace(candidatePath))
            {
                RemoteInstallProjectPath = candidatePath;
            }
        }
        else if (!RemoteInstallPromptProject)
        {
            RefreshRemoteInstallModules(autoRecommendModule: string.IsNullOrWhiteSpace(RemoteInstallModule));
        }

        if (RemoteInstallRules.IsAutoGeneratedSlotTitle(SnippetTitle))
        {
            SnippetTitle = RemoteInstallRules.RecommendDefaultSlotTitle(
                RemoteInstallProjectPath,
                RemoteInstallDeviceKind,
                RemoteInstallPromptProject);
        }

        NotifyActionModeTransition();
        _ = RefreshRemoteInstallDevicesAsync();
    }

    private void SwitchActionMode(Action apply)
    {
        ClearUntouchedAutoFilledPresetCopy();
        apply();
        NotifyActionModeTransition();
    }

    private bool CanApplyAutoTitle() =>
        string.IsNullOrWhiteSpace(SnippetTitle) || SnippetTitle == "새 실행 항목" || _hasAutoFilledTitle;

    private bool CanApplyAutoDescription() =>
        string.IsNullOrWhiteSpace(Description) || _hasAutoFilledDescription;

    private void ApplyAutoTitle(string title)
    {
        _draft.Title = title;
        _hasAutoFilledTitle = true;
        OnPropertyChanged(nameof(SnippetTitle));
        NotifyDetectedAudioIconChanged();
    }

    private void ApplyAutoDescription(string description)
    {
        _draft.Description = description;
        _hasAutoFilledDescription = true;
        OnPropertyChanged(nameof(Description));
    }

    private void ClearUntouchedAutoFilledPresetCopy()
    {
        if (_hasAutoFilledTitle)
        {
            _draft.Title = string.Empty;
            _hasAutoFilledTitle = false;
            OnPropertyChanged(nameof(SnippetTitle));
            NotifyDetectedAudioIconChanged();
        }

        if (_hasAutoFilledDescription)
        {
            _draft.Description = string.Empty;
            _hasAutoFilledDescription = false;
            OnPropertyChanged(nameof(Description));
        }
    }

    private void SyncBluetoothTerminalCommand()
    {
        _draft.ApplyBluetoothControlDefaults(_bluetoothControlAction, _bluetoothTarget);
        NotifyTerminalPropertiesChanged();
    }

    private void ChooseBluetoothDevice()
    {
        var selected = _dialogService.SelectPairedBluetoothDevice();
        if (selected is null) return;

        BluetoothTarget = selected.MacAddress;
        if (CanApplyAutoTitle())
        {
            var suffix = IsBluetoothDisconnectAction ? "off" : "on";
            SnippetTitle = $"{selected.DisplayName} {suffix}";
        }

        ErrorMessage = string.Empty;
    }

    private void ToggleAdvancedTerminalSettings() =>
        ShowAdvancedTerminalSettings = !_showAdvancedTerminalSettings;

    private void ToggleAdvancedRemoteInstallSettings() =>
        ShowAdvancedRemoteInstallSettings = !_showAdvancedRemoteInstallSettings;

    private void ToggleRemoteInstallDeviceKind(RemoteInstallDeviceKind kind)
    {
        var updated = RemoteInstallDeviceKind.HasFlag(kind)
            ? RemoteInstallDeviceKind & ~kind
            : RemoteInstallDeviceKind | kind;

        // Keep at least one device kind selected so the user cannot accidentally clear all targets.
        if (updated == RemoteInstallDeviceKind.None)
        {
            return;
        }

        SetRemoteInstallDeviceKind(updated);
    }

    private void SetRemoteInstallDeviceKind(RemoteInstallDeviceKind kind)
    {
        RemoteInstallDeviceKind = kind;

        if (_remoteInstallModuleOptions.Count > 0)
        {
            var moduleNames = _remoteInstallModuleOptions.Select(static o => o.ModuleName).ToList();
            var recommended = RemoteInstallRules.RecommendModuleForDeviceKind(moduleNames, kind);
            if (!string.IsNullOrWhiteSpace(recommended))
            {
                RemoteInstallModule = recommended;
            }
        }

        if (RemoteInstallRules.IsAutoGeneratedSlotTitle(SnippetTitle))
        {
            SnippetTitle = RemoteInstallRules.RecommendDefaultSlotTitle(
                RemoteInstallProjectPath,
                kind,
                RemoteInstallPromptProject);
        }

        ErrorMessage = string.Empty;
    }

    private void ChooseRemoteInstallProjectFolder()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null) return;

        RemoteInstallProjectPath = selectedPath;
        if (RemoteInstallRules.IsAutoGeneratedSlotTitle(SnippetTitle))
        {
            SnippetTitle = RemoteInstallRules.RecommendDefaultSlotTitle(selectedPath, RemoteInstallDeviceKind);
        }

        ErrorMessage = string.Empty;
    }

    private void RefreshRemoteInstallModules(bool autoRecommendModule)
    {
        if (_androidRemoteInstallGateway is null || string.IsNullOrWhiteSpace(RemoteInstallProjectPath))
        {
            RemoteInstallModuleOptions = [];
            return;
        }

        var modules = _androidRemoteInstallGateway.ScanProjectModules(RemoteInstallProjectPath);
        RemoteInstallModuleOptions = modules
            .Select(static m => new AndroidAppModuleOption(
                m.ModuleName,
                string.IsNullOrWhiteSpace(m.ApplicationId)
                    ? m.ModuleName
                    : $"{m.ModuleName} ({m.ApplicationId})"))
            .ToList();

        if (modules.Count == 0)
        {
            return;
        }

        var moduleNames = modules.Select(static m => m.ModuleName).ToList();
        var currentNormalized = RemoteInstallRules.NormalizeModuleName(RemoteInstallModule);
        if (autoRecommendModule
            || string.IsNullOrWhiteSpace(currentNormalized)
            || !moduleNames.Contains(currentNormalized, StringComparer.OrdinalIgnoreCase))
        {
            var recommended = RemoteInstallRules.RecommendModuleForDeviceKind(moduleNames, RemoteInstallDeviceKind);
            if (!string.IsNullOrWhiteSpace(recommended))
            {
                RemoteInstallModule = recommended;
            }
        }
    }

    private async Task RefreshRemoteInstallDevicesAsync()
    {
        if (_androidRemoteInstallGateway is null)
        {
            RemoteInstallConnectedDevicesSummary = "ADB 실행기가 준비되지 않았습니다.";
            return;
        }

        try
        {
            RemoteInstallConnectedDevicesSummary = "ADB 기기 스캔 및 4단계 분류 중...";
            var devices = await _androidRemoteInstallGateway.ScanConnectedDevicesAsync();
            if (devices.Count == 0)
            {
                RemoteInstallConnectedDevicesSummary = "현재 연결된 ADB 기기가 없습니다. (1·2·3 슬롯으로 무선 ADB 연결 후 확인)";
                return;
            }

            RemoteInstallConnectedDevicesSummary =
                "감지됨: " + string.Join(" / ", devices.Select(static d => $"{d.DisplaySummary} ({d.ClassificationSource})"));
        }
        catch (Exception ex)
        {
            RemoteInstallConnectedDevicesSummary = $"ADB 기기 스캔 실패: {ex.Message}";
        }
    }

    private void ApplyAdbIpHint(string? ipAddress)
    {
        RemoteInstallTargetDevice = ipAddress ?? string.Empty;
        ErrorMessage = string.Empty;
    }

    private void ApplyBatch456RemoteInstallSlots()
    {
        List<string> moduleNames = [];
        if (!RemoteInstallPromptProject)
        {
            if (string.IsNullOrWhiteSpace(RemoteInstallProjectPath))
            {
                ErrorMessage = RemoteInstallRules.ProjectPathRequiredMessage;
                return;
            }

            var modules = _androidRemoteInstallGateway?.ScanProjectModules(RemoteInstallProjectPath) ?? [];
            moduleNames = modules.Select(static m => m.ModuleName).ToList();
            if (moduleNames.Count == 0 && !string.IsNullOrWhiteSpace(RemoteInstallModule))
            {
                var fallbackMod = RemoteInstallRules.NormalizeModuleName(RemoteInstallModule);
                if (!string.IsNullOrWhiteSpace(fallbackMod))
                {
                    moduleNames.Add(fallbackMod);
                }
            }

            if (moduleNames.Count == 0)
            {
                ErrorMessage = "선택한 폴더에서 Android 앱 모듈(settings.gradle)을 찾지 못했습니다.";
                return;
            }
        }

        var batchTargets = new (SlotKey Slot, RemoteInstallDeviceKind Kind)[]
        {
            (SlotKey.Numpad4, RemoteInstallDeviceKind.Tablet),
            (SlotKey.Numpad5, RemoteInstallDeviceKind.Phone),
            (SlotKey.Numpad6, RemoteInstallDeviceKind.Watch)
        };

        Snippet? lastSaved = null;
        foreach (var (slot, kind) in batchTargets)
        {
            var moduleForKind = RemoteInstallPromptProject
                ? null
                : RemoteInstallRules.RecommendModuleForDeviceKind(moduleNames, kind) ?? moduleNames[0];
            var title = RemoteInstallRules.RecommendDefaultSlotTitle(
                RemoteInstallProjectPath,
                kind,
                RemoteInstallPromptProject);
            var description = RemoteInstallPromptProject
                ? $"{RemoteInstallRules.GetDeviceKindKoreanLabel(kind)} 원격설치 (실행 시 프로젝트 선택)"
                : $"{RemoteInstallRules.GetDeviceKindKoreanLabel(kind)} ({moduleForKind}) 원격설치";
            var saveData = new SnippetSaveData(
                Title: title,
                Content: string.Empty,
                Description: description,
                ImagePath: null,
                ThumbnailPath: null,
                ActionType: SnippetActionType.RemoteInstall,
                RemoteInstallProjectPath: RemoteInstallPromptProject ? null : RemoteInstallProjectPath.Trim(),
                RemoteInstallModule: moduleForKind,
                RemoteInstallDeviceKind: kind,
                RemoteInstallTargetDevice: null,
                RemoteInstallLaunchAfterInstall: RemoteInstallLaunchAfterInstall,
                RemoteInstallPromptProject: RemoteInstallPromptProject);

            var existingId = slot == SlotKey ? _snippetId : null;
            var result = _saveSnippetUseCase.Execute(new SaveSnippetRequest(
                CategoryId,
                slot,
                existingId,
                IsSlotEnabled: true,
                OriginalIsSlotEnabled: true,
                saveData));

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "슬롯 4·5·6 일괄 배치에 실패했습니다.";
                return;
            }

            if (result.Snippet is not null)
            {
                lastSaved = result.Snippet;
            }
        }

        if (lastSaved is not null)
        {
            _draft.MarkSaved();
            ErrorMessage = string.Empty;
            _showStatus(RemoteInstallPromptProject
                ? "슬롯 4(태블릿) · 5(폰) · 6(워치) 실행 시 선택형 원격설치 구성을 저장했습니다."
                : "슬롯 4(태블릿) · 5(폰) · 6(워치) 원격설치 구성을 저장했습니다.");
            _afterSave(lastSaved);
        }
    }

    private string? ResolveInitialRemoteInstallProjectPath()
    {
        if (!string.IsNullOrWhiteSpace(_defaultRemoteInstallProjectPath)
            && Directory.Exists(_defaultRemoteInstallProjectPath))
        {
            return _defaultRemoteInstallProjectPath;
        }

        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                var watchYouDoinPath = Path.Combine(userProfile, "AndroidStudioProjects", "WatchYouDoin");
                if (Directory.Exists(watchYouDoinPath))
                {
                    return watchYouDoinPath;
                }
            }
        }
        catch
        {
            // Ignore directory discovery errors
        }

        return null;
    }

    private void NotifyActionModeTransition()
    {
        OnPropertyChanged(nameof(ActionType));
        OnPropertyChanged(nameof(SelectedFileActionMode));
        NotifyActionTypePresentationChanged();
        NotifyFileActionModeChanged();
        NotifyTerminalPropertiesChanged();
        OnPropertyChanged(nameof(ShowSpotifyMediaConnectionNotice));
        NotifyImageChanged();
        NotifyDetectedAudioIconChanged();
    }

    private void NotifyTerminalPropertiesChanged()
    {
        OnPropertyChanged(nameof(TerminalCommand));
        OnPropertyChanged(nameof(SelectedTerminalShell));
        OnPropertyChanged(nameof(OpenTerminalWindow));
        OnPropertyChanged(nameof(RunAsAdministrator));
        OnPropertyChanged(nameof(IsTurnOffDisplayPresetActive));
        OnPropertyChanged(nameof(IsAdbPairingPresetActive));
        OnPropertyChanged(nameof(IsBluetoothPresetActive));
        OnPropertyChanged(nameof(IsBluetoothConnectAction));
        OnPropertyChanged(nameof(IsBluetoothDisconnectAction));
        OnPropertyChanged(nameof(BluetoothTarget));
        OnPropertyChanged(nameof(IsCustomTerminalCommandActive));
        OnPropertyChanged(nameof(ShowAdvancedTerminalSettings));
        OnPropertyChanged(nameof(ShowTerminalCommandEditor));
        ErrorMessage = string.Empty;
    }

    private void NotifyImageChanged()
    {
        OnPropertyChanged(nameof(ThumbnailPath));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(SlotImageMode));
    }

    private void NotifyActionTypePresentationChanged()
    {
        OnPropertyChanged(nameof(EditorPanel));
        OnPropertyChanged(nameof(IsPasteAction));
        OnPropertyChanged(nameof(IsOpenLaunchAction));
        OnPropertyChanged(nameof(IsMediaAction));
        OnPropertyChanged(nameof(IsSystemUtilityAction));
        OnPropertyChanged(nameof(IsTerminalAction));
        OnPropertyChanged(nameof(IsAndroidAction));
        OnPropertyChanged(nameof(IsPasteTextMode));
        OnPropertyChanged(nameof(IsLaunchUrlMode));
        OnPropertyChanged(nameof(IsTimerMode));
        OnPropertyChanged(nameof(IsRemoteInstallMode));
    }

    private void NotifyFileActionModeChanged()
    {
        OnPropertyChanged(nameof(IsFileLaunchMode));
        OnPropertyChanged(nameof(IsFilePasteMode));
        OnPropertyChanged(nameof(FilePathLabel));
        OnPropertyChanged(nameof(FilePickerButtonText));
    }

    private bool SetDraftValue<T>(
        T currentValue,
        T newValue,
        Action<ExecutableActionEditDraft, T> apply,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return false;
        }

        apply(_draft, newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    private void ApplyNormalizedLaunchUrl(string? normalizedLaunchUrl)
    {
        var previousLaunchUrl = LaunchUrl;
        _draft.ApplyNormalizedLaunchUrl(normalizedLaunchUrl);
        if (previousLaunchUrl != LaunchUrl)
        {
            OnPropertyChanged(nameof(LaunchUrl));
        }
    }
}

public sealed record SnippetTransferTargetSlot(SlotKey SlotKey, string Label);

public sealed record AndroidAppModuleOption(string ModuleName, string DisplayLabel);

public sealed record SnippetMediaCommandOption(SnippetMediaCommand Command, string Label)
{
    public static IReadOnlyList<SnippetMediaCommandOption> SystemCommands { get; } =
    [
        new(SnippetMediaCommand.PlayPause, "재생/일시정지"),
        new(SnippetMediaCommand.PreviousTrack, "이전 곡"),
        new(SnippetMediaCommand.NextTrack, "다음 곡"),
        new(SnippetMediaCommand.Stop, "정지"),
        new(SnippetMediaCommand.Mute, "음소거"),
        new(SnippetMediaCommand.VolumeUp, "볼륨 증가"),
        new(SnippetMediaCommand.VolumeDown, "볼륨 감소")
    ];

    public static IReadOnlyList<SnippetMediaCommandOption> SpotifyCommands { get; } =
    [
        new(SnippetMediaCommand.PlayPause, "재생/일시정지"),
        new(SnippetMediaCommand.PreviousTrack, "이전 곡"),
        new(SnippetMediaCommand.NextTrack, "다음 곡"),
        new(SnippetMediaCommand.ToggleShuffle, "셔플 켜기/끄기"),
        new(SnippetMediaCommand.CycleRepeat, "반복 모드 변경"),
        new(SnippetMediaCommand.OpenSpotifyAndResume, "Spotify 앱 열고 재생 시도")
    ];

    public static IReadOnlyList<SnippetMediaCommandOption> All { get; } =
        SystemCommands.Concat(SpotifyCommands).DistinctBy(option => option.Command).ToList();

    public static IReadOnlyList<SnippetMediaCommandOption> ForProvider(SnippetMediaProvider provider) =>
        provider == SnippetMediaProvider.Spotify ? SpotifyCommands : SystemCommands;

    public static SnippetMediaCommand GetValidCommandForProvider(
        SnippetMediaProvider provider,
        SnippetMediaCommand command) =>
        MediaCommandRules.GetValidCommandForProvider(provider, command);
}

public sealed record PasteShortcutModeOption(PasteShortcutMode Mode, string Label)
{
    public static IReadOnlyList<PasteShortcutModeOption> All { get; } =
    [
        new(PasteShortcutMode.CtrlV, "기본 붙여넣기 (Ctrl+V)"),
        new(PasteShortcutMode.CtrlShiftV, "터미널 붙여넣기 (Ctrl+Shift+V)")
    ];
}

public sealed record TerminalShellOption(SnippetTerminalShell Shell, string Label)
{
    public static IReadOnlyList<TerminalShellOption> All { get; } =
    [
        new(SnippetTerminalShell.Cmd, "cmd"),
        new(SnippetTerminalShell.PowerShell, "PowerShell")
    ];
}

public sealed record SnippetMediaProviderOption(SnippetMediaProvider Provider, string Label)
{
    public static IReadOnlyList<SnippetMediaProviderOption> All { get; } =
    [
        new(SnippetMediaProvider.System, "Windows 기본 미디어 제어"),
        new(SnippetMediaProvider.Spotify, "Spotify")
    ];
}
