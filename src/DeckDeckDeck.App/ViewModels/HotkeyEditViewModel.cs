// 역할: 단축키 추가 및 편집 화면에서 키 조합 입력과 실행할 동작(앱, 미디어, 블루투스 등) 선택 상태를 관리하는 화면 모델입니다.
using System.Windows.Input;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class HotkeyEditViewModel : ObservableObject
{
    private readonly Action<HotkeyAction> _afterSave;
    private readonly Action _afterDelete;
    private readonly Action _cancel;
    private readonly Action _notifyHotkeyCaptureStateChanged;
    private readonly DeleteHotkeyActionUseCase _deleteHotkeyActionUseCase;
    private readonly IDialogAdapter _dialogService;
    private readonly ExecutableActionEditDraft _draft;
    private readonly IAppLogger? _loggingService;
    private readonly bool _isSpotifyConnected;
    private readonly SaveHotkeyActionUseCase _saveHotkeyActionUseCase;
    private readonly Guid? _hotkeyActionId;
    private readonly Action<string> _showStatus;
    private const string TurnOffDisplayDefaultDescription = "모니터 화면을 끕니다. 핫스팟과 백그라운드 작업은 유지됩니다.";
    private string _errorMessage = string.Empty;
    private bool _showAdvancedTerminalSettings;
    private BluetoothAudioActionKind _bluetoothControlAction = BluetoothAudioActionKind.Connect;
    private string _bluetoothTarget = string.Empty;
    private bool _isBluetoothPresetSelected;
    private bool _hasAutoFilledTitle;
    private bool _hasAutoFilledDescription;
    private HotkeyGesture? _gesture;
    private bool _isCapturingHotkey;
    private bool _isEnabled = true;
    private bool _isSaving;
    private IReadOnlyList<SnippetMediaCommandOption> _mediaCommandOptions = SnippetMediaCommandOption.SystemCommands;

    public HotkeyEditViewModel(
        HotkeyAction? action,
        HotkeyActionEditorState editorState,
        SaveHotkeyActionUseCase saveHotkeyActionUseCase,
        DeleteHotkeyActionUseCase deleteHotkeyActionUseCase,
        IDialogAdapter dialogService,
        Action cancel,
        Action<HotkeyAction> afterSave,
        Action afterDelete,
        Action notifyHotkeyCaptureStateChanged,
        Action<string> showStatus,
        IImageFileRepository? thumbnailService = null,
        IAppLogger? loggingService = null,
        ISnippetImageResolver? snippetImageService = null,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        _hotkeyActionId = action?.Id;
        _saveHotkeyActionUseCase = saveHotkeyActionUseCase;
        _deleteHotkeyActionUseCase = deleteHotkeyActionUseCase;
        _dialogService = dialogService;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _notifyHotkeyCaptureStateChanged = notifyHotkeyCaptureStateChanged;
        _showStatus = showStatus;
        _loggingService = loggingService;
        _draft = ExecutableActionEditDraft.FromHotkeyAction(
            action,
            thumbnailService,
            snippetImageService,
            storedImagePathResolver);
        _isSpotifyConnected = editorState.SpotifyConnection.IsConnected;

        _gesture = action?.Gesture;
        _isEnabled = action?.IsEnabled ?? true;
        _mediaCommandOptions = SnippetMediaCommandOption.ForProvider(_draft.MediaProvider);

        SaveCommand = new RelayCommand(Save, () => !IsSaving);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        BeginHotkeyCaptureCommand = new RelayCommand(BeginHotkeyCapture);
        ClearHotkeyCommand = new RelayCommand(ClearHotkey);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
        ChooseLaunchFileCommand = new RelayCommand(ChooseLaunchFile);
        ChooseLaunchFolderCommand = new RelayCommand(ChooseLaunchFolder);
        ChooseInstalledAppCommand = new RelayCommand(ChooseInstalledApp);
        ChooseTerminalWorkingDirectoryCommand = new RelayCommand(ChooseTerminalWorkingDirectory);
        ChooseBluetoothDeviceCommand = new RelayCommand(ChooseBluetoothDevice);
        ToggleAdvancedTerminalSettingsCommand = new RelayCommand(ToggleAdvancedTerminalSettings);
        if (TerminalCommandParameterRules.TryParseBluetoothAudioCommand(_draft.TerminalCommand, out var parsedBt, allowEmptyTarget: true))
        {
            _isBluetoothPresetSelected = true;
            _bluetoothControlAction = parsedBt.Action;
            _bluetoothTarget = parsedBt.Target;
        }
    }

    public string Title => IsExisting ? "핫키 편집" : "새 핫키";

    public bool IsExisting => _hotkeyActionId.HasValue;

    public bool CanDelete => IsExisting;

    public IReadOnlyList<SnippetMediaProviderOption> MediaProviderOptions { get; } =
        SnippetMediaProviderOption.All;

    public IReadOnlyList<PasteShortcutModeOption> PasteShortcutModeOptions { get; } =
        PasteShortcutModeOption.All;

    public IReadOnlyList<TerminalShellOption> TerminalShellOptions { get; } =
        TerminalShellOption.All;

    public IReadOnlyList<SnippetMediaCommandOption> MediaCommandOptions
    {
        get => _mediaCommandOptions;
        private set => SetProperty(ref _mediaCommandOptions, value);
    }

    public string HotkeyTitle
    {
        get => _draft.Title;
        set
        {
            if (SetDraftValue(_draft.Title, value, static (draft, newValue) => draft.Title = newValue))
            {
                _hasAutoFilledTitle = false;
            }
        }
    }

    public string HotkeyDisplayText => IsCapturingHotkey
        ? "입력 대기 중..."
        : _gesture?.DisplayText ?? "미지정";

    public bool IsCapturingHotkey
    {
        get => _isCapturingHotkey;
        private set
        {
            if (SetProperty(ref _isCapturingHotkey, value))
            {
                OnPropertyChanged(nameof(HotkeyDisplayText));
                _notifyHotkeyCaptureStateChanged();
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string Content
    {
        get => _draft.Content;
        set => SetDraftValue(_draft.Content, value, static (draft, newValue) => draft.Content = newValue);
    }

    public PasteShortcutMode PasteShortcutMode
    {
        get => _draft.PasteShortcutMode;
        set => SetDraftValue(
            _draft.PasteShortcutMode,
            value,
            static (draft, newValue) => draft.PasteShortcutMode = newValue);
    }

    public SnippetActionType ActionType
    {
        get => _draft.ActionType;
        private set
        {
            if (_draft.ActionType == value)
            {
                return;
            }

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
            if (_draft.LaunchPath == value)
            {
                return;
            }

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
            if (_draft.FileActionMode == value)
            {
                return;
            }

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

    public string FilePathLabel => IsFilePasteMode
        ? "붙여넣을 파일"
        : "실행할 파일, 폴더 또는 바로 가기";

    public string FilePickerButtonText => IsFilePasteMode
        ? "파일 선택"
        : "파일/바로 가기 선택";

    public string LaunchUrl
    {
        get => _draft.LaunchUrl;
        set => SetDraftValue(_draft.LaunchUrl, value, static (draft, newValue) => draft.SetLaunchUrl(newValue));
    }

    public SnippetMediaProvider SelectedMediaProvider
    {
        get => _draft.MediaProvider;
        set
        {
            if (_draft.MediaProvider == value)
            {
                return;
            }

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
            if (_draft.MediaCommand == value)
            {
                return;
            }

            _draft.SetMediaCommand(value);
            OnPropertyChanged();
            NotifyImageChanged();
        }
    }

    public bool IsSpotifyMediaProvider => SelectedMediaProvider == SnippetMediaProvider.Spotify;

    public bool ShowSpotifyMediaConnectionNotice =>
        IsMediaAction
        && IsSpotifyMediaProvider
        && !_isSpotifyConnected;

    public string SpotifyMediaConnectionNotice =>
        "Spotify 연결 전에도 저장할 수 있습니다. 실행하려면 설정에서 Spotify를 연결해 주세요.";

    public string TerminalCommand
    {
        get => _draft.TerminalCommand;
        set
        {
            if (!SetDraftValue(
                    _draft.TerminalCommand,
                    value,
                    static (draft, newValue) => draft.TerminalCommand = newValue))
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
            if (_showAdvancedTerminalSettings == value)
            {
                return;
            }

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
        }
    }

    public IRelayCommand ChooseBluetoothDeviceCommand { get; }
    public IRelayCommand ToggleAdvancedTerminalSettingsCommand { get; }

    public SnippetTerminalShell SelectedTerminalShell
    {
        get => _draft.TerminalShell;
        set => SetDraftValue(
            _draft.TerminalShell,
            value,
            static (draft, newValue) => draft.TerminalShell = newValue);
    }

    public bool RunAsAdministrator
    {
        get => _draft.RunAsAdministrator;
        set => SetDraftValue(
            _draft.RunAsAdministrator,
            value,
            static (draft, newValue) => draft.RunAsAdministrator = newValue);
    }

    public bool OpenTerminalWindow
    {
        get => _draft.OpenTerminalWindow;
        set => SetDraftValue(
            _draft.OpenTerminalWindow,
            value,
            static (draft, newValue) => draft.OpenTerminalWindow = newValue);
    }

    public string TerminalWorkingDirectory
    {
        get => _draft.TerminalWorkingDirectory;
        set => SetDraftValue(
            _draft.TerminalWorkingDirectory,
            value,
            static (draft, newValue) => draft.TerminalWorkingDirectory = newValue);
    }

    public string AdbDeviceIp
    {
        get => _draft.AdbDeviceIp;
        set => SetDraftValue(
            _draft.AdbDeviceIp,
            value,
            static (draft, newValue) => draft.AdbDeviceIp = newValue);
    }

    public string Description
    {
        get => _draft.Description;
        set
        {
            if (SetDraftValue(_draft.Description, value, static (draft, newValue) => draft.Description = newValue))
            {
                _hasAutoFilledDescription = false;
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value) && SaveCommand is RelayCommand relayCommand)
            {
                relayCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ThumbnailPath => _draft.GetPreviewThumbnailPath();

    public bool HasImage => _draft.HasImage;

    public SlotImageMode SlotImageMode => _draft.SlotImageMode;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand BeginHotkeyCaptureCommand { get; }

    public ICommand ClearHotkeyCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    public ICommand ChooseLaunchFileCommand { get; }

    public ICommand ChooseLaunchFolderCommand { get; }

    public ICommand ChooseInstalledAppCommand { get; }

    public ICommand ChooseTerminalWorkingDirectoryCommand { get; }

    public void CaptureHotkey(HotkeyGesture gesture)
    {
        if (!IsCapturingHotkey)
        {
            return;
        }

        if (!gesture.IsComplete)
        {
            ErrorMessage = SaveHotkeyActionUseCase.HotkeyModifierOnlyMessage;
            return;
        }

        _gesture = gesture;
        IsCapturingHotkey = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HotkeyDisplayText));
    }

    public void CancelHotkeyCapture()
    {
        IsCapturingHotkey = false;
        ErrorMessage = string.Empty;
    }

    public void DropImageFiles(IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count != 1)
        {
            ErrorMessage = "이미지는 하나만 드롭해 주세요.";
            return;
        }

        ReplaceImageFromPath(sourcePaths[0]);
    }

    private void BeginHotkeyCapture()
    {
        IsCapturingHotkey = true;
        ErrorMessage = "사용할 키를 눌러 주세요. Esc를 누르면 기존 값을 유지합니다.";
    }

    private void ClearHotkey()
    {
        _gesture = null;
        IsCapturingHotkey = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HotkeyDisplayText));
    }

    private void Save()
    {
        if (IsSaving)
        {
            return;
        }

        IsCapturingHotkey = false;
        IsSaving = true;
        try
        {
            var autoIcon = _draft.PrepareAutoIconForSave();
            NotifyImageChanged();
            var result = _saveHotkeyActionUseCase.Execute(BuildSaveRequest(autoIcon));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? string.Empty;
                return;
            }

            if (ActionType == SnippetActionType.LaunchUrl
                && !string.IsNullOrWhiteSpace(result.NormalizedLaunchUrl))
            {
                ApplyNormalizedLaunchUrl(result.NormalizedLaunchUrl);
            }

            var action = result.HotkeyAction!;
            _draft.MarkSaved();
            ErrorMessage = string.Empty;
            _showStatus($"{action.Title} 핫키를 저장했습니다.");
            _afterSave(action);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private SaveHotkeyActionRequest BuildSaveRequest(AutoIconCacheEntry? autoIcon)
    {
        return new SaveHotkeyActionRequest(
            _hotkeyActionId,
            _draft.ToHotkeyActionSaveData(_gesture, IsEnabled, autoIcon));
    }

    private void Cancel()
    {
        IsCapturingHotkey = false;
        _draft.DeleteCurrentUnsavedImage();
        _cancel();
    }

    private void Delete()
    {
        if (!_hotkeyActionId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "핫키 삭제",
            "이 핫키를 삭제할까요?");
        if (!confirmed)
        {
            return;
        }

        IsCapturingHotkey = false;
        _draft.DeleteCurrentUnsavedImage();
        _deleteHotkeyActionUseCase.Execute(_hotkeyActionId.Value);
        _showStatus("핫키를 삭제했습니다.");
        _afterDelete();
    }

    private void ChooseImage()
    {
        if (!_draft.CanStoreImages)
        {
            ErrorMessage = "이미지 저장소가 준비되지 않았습니다.";
            return;
        }

        var selectedPath = _dialogService.SelectImageFile();
        if (selectedPath is null)
        {
            return;
        }

        ReplaceImageFromPath(selectedPath);
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
            _loggingService?.Log("Image processing failed for hotkey action.", ex);
        }
    }

    private void RemoveImage()
    {
        _draft.RemoveImage();
        NotifyImageChanged();
    }

    private void ChooseLaunchFile()
    {
        var selectedPath = IsFilePasteMode
            ? _dialogService.SelectPasteFile()
            : _dialogService.SelectLaunchFile();
        if (selectedPath is null)
        {
            return;
        }

        LaunchPath = selectedPath;
        ErrorMessage = string.Empty;
    }

    private void ChooseInstalledApp()
    {
        var selected = _dialogService.SelectInstalledApp();
        if (selected is null)
        {
            return;
        }

        LaunchPath = selected.LaunchPath;
        if (string.IsNullOrWhiteSpace(HotkeyTitle) || HotkeyTitle == "새 핫키")
        {
            HotkeyTitle = selected.DisplayName;
        }

        ErrorMessage = string.Empty;
    }

    private void ChooseLaunchFolder()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null)
        {
            return;
        }

        _draft.SetLaunchFolderPath(selectedPath);
        OnPropertyChanged(nameof(LaunchPath));
        ErrorMessage = string.Empty;
        NotifyImageChanged();
    }

    private void ChooseTerminalWorkingDirectory()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null)
        {
            return;
        }

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

    private void SwitchActionMode(Action apply)
    {
        ClearUntouchedAutoFilledPresetCopy();
        apply();
        NotifyActionModeTransition();
    }

    private bool CanApplyAutoTitle() =>
        string.IsNullOrWhiteSpace(HotkeyTitle) || HotkeyTitle == "새 핫키" || _hasAutoFilledTitle;

    private bool CanApplyAutoDescription() =>
        string.IsNullOrWhiteSpace(Description) || _hasAutoFilledDescription;

    private void ApplyAutoTitle(string title)
    {
        _draft.Title = title;
        _hasAutoFilledTitle = true;
        OnPropertyChanged(nameof(HotkeyTitle));
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
            OnPropertyChanged(nameof(HotkeyTitle));
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
        if (selected is null)
        {
            return;
        }

        BluetoothTarget = selected.MacAddress;
        if (CanApplyAutoTitle())
        {
            var suffix = IsBluetoothDisconnectAction ? "off" : "on";
            HotkeyTitle = $"{selected.DisplayName} {suffix}";
        }

        ErrorMessage = string.Empty;
    }

    private void ToggleAdvancedTerminalSettings()
    {
        ShowAdvancedTerminalSettings = !_showAdvancedTerminalSettings;
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
