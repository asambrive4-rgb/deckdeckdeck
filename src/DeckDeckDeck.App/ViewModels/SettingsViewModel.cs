using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Action _cancel;
    private readonly Action _afterSave;
    private readonly ICreateManualBackupUseCase _createManualBackupUseCase;
    private readonly IDialogAdapter _dialogService;
    private readonly ILoadSettingsUseCase _loadSettingsUseCase;
    private readonly IAppLogger? _loggingService;
    private readonly IRestoreBackupUseCase _restoreBackupUseCase;
    private readonly Action<string> _showStatus;
    private readonly IClipboardTextWriter _clipboardService;
    private readonly IStartupRegistrationUseCase _startupRegistrationUseCase;
    private readonly ISpotifyConnectionUseCase _spotifyConnectionUseCase;
    private readonly ISaveAppPreferencesUseCase _saveAppPreferencesUseCase;
    private readonly AppSettings _settings;
    private bool _autoHideAfterPaste;
    private bool _autoBackupEnabled;
    private string _backupFolderPath = string.Empty;
    private bool _bringWindowToFrontOnHotkey;
    private string _errorMessage = string.Empty;
    private bool _isSpotifyConnected;
    private bool _isSpotifyConnectionBusy;
    private bool _launchAtStartup;
    private bool _restoreClipboardAfterPaste;
    private bool _runAsAdministratorAtStartup;
    private bool _showSpotifyConnectionFields;
    private string _spotifyClientIdInput = string.Empty;
    private string _spotifyConnectionStatusText = string.Empty;

    public SettingsViewModel(
        ILoadSettingsUseCase loadSettingsUseCase,
        ISaveAppPreferencesUseCase saveAppPreferencesUseCase,
        ICreateManualBackupUseCase createManualBackupUseCase,
        IRestoreBackupUseCase restoreBackupUseCase,
        IStartupRegistrationUseCase startupRegistrationUseCase,
        ISpotifyConnectionUseCase spotifyConnectionUseCase,
        IClipboardTextWriter clipboardService,
        IDialogAdapter dialogService,
        Action cancel,
        Action afterSave,
        Action<string> showStatus,
        IAppLogger? loggingService = null)
    {
        _loadSettingsUseCase = loadSettingsUseCase;
        _cancel = cancel;
        _afterSave = afterSave;
        _showStatus = showStatus;
        _loggingService = loggingService;
        _saveAppPreferencesUseCase = saveAppPreferencesUseCase;
        _createManualBackupUseCase = createManualBackupUseCase;
        _restoreBackupUseCase = restoreBackupUseCase;
        _startupRegistrationUseCase = startupRegistrationUseCase;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _spotifyConnectionUseCase = spotifyConnectionUseCase;
        _settings = loadSettingsUseCase.Execute();

        _bringWindowToFrontOnHotkey = _settings.BringWindowToFrontOnHotkey;
        _autoHideAfterPaste = _settings.AutoHideAfterPaste;
        _restoreClipboardAfterPaste = _settings.RestoreClipboardAfterPaste;
        _autoBackupEnabled = _settings.AutoBackupEnabled;
        _backupFolderPath = _settings.BackupFolderPath;
        var startupState = _startupRegistrationUseCase.GetState();
        _launchAtStartup = startupState.IsEnabled;
        _runAsAdministratorAtStartup = startupState.IsEnabled && startupState.RunAsAdministrator;
        RefreshSpotifyConnectionState();

        SaveCommand = new RelayCommand(Save);
        BackCommand = new RelayCommand(_cancel);
        ChooseBackupFolderCommand = new RelayCommand(ChooseBackupFolder);
        CreateManualBackupCommand = new RelayCommand(CreateManualBackup);
        RestoreBackupCommand = new RelayCommand(RestoreBackup);
        ShowSpotifyConnectionFieldsCommand = new RelayCommand(RevealSpotifyConnectionFields);
        OpenSpotifyDeveloperDashboardCommand = new RelayCommand(OpenSpotifyDeveloperDashboard);
        CopySpotifyAppNameExampleCommand = new RelayCommand(CopySpotifyAppNameExample);
        CopySpotifyAppDescriptionExampleCommand = new RelayCommand(CopySpotifyAppDescriptionExample);
        CopySpotifyRedirectUriCommand = new RelayCommand(CopySpotifyRedirectUri);
        StartSpotifyConnectionCommand = new AsyncRelayCommand(StartSpotifyConnectionAsync);
        DisconnectSpotifyCommand = new RelayCommand(DisconnectSpotify);
    }

    public string Title => "설정";

    public string HomeHotkey => _settings.HomeHotkey;

    public string DirectCategoryHotkeys => _settings.DirectCategoryHotkeys;

    public string HomeHotkeyText => $"홈: {HomeHotkey}";

    public string MinimizeHotkeyText => "최소화: Ctrl + Numpad0 길게 누르기";

    public string DirectCategoryHotkeysText => $"카테고리 바로 열기: {DirectCategoryHotkeys}";

    public string AdminPermissionNotice =>
        "관리자 권한 앱, 보호된 입력창, 보안 프로그램, 일부 게임에서는 DeckDeckDeck도 같은 권한으로 실행해야 붙여넣기가 동작할 수 있습니다.";

    public string StartupPermissionNotice =>
        "관리자 권한으로 시작하면 저장할 때 Windows 확인창이 표시될 수 있습니다. 변경 내용은 다음 Windows 로그인부터 적용됩니다.";

    public bool BringWindowToFrontOnHotkey
    {
        get => _bringWindowToFrontOnHotkey;
        set => SetProperty(ref _bringWindowToFrontOnHotkey, value);
    }

    public bool AutoHideAfterPaste
    {
        get => _autoHideAfterPaste;
        set => SetProperty(ref _autoHideAfterPaste, value);
    }

    public bool RestoreClipboardAfterPaste
    {
        get => _restoreClipboardAfterPaste;
        set => SetProperty(ref _restoreClipboardAfterPaste, value);
    }

    public bool AutoBackupEnabled
    {
        get => _autoBackupEnabled;
        set => SetProperty(ref _autoBackupEnabled, value);
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set
        {
            if (!SetProperty(ref _launchAtStartup, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanRunAsAdministratorAtStartup));
            if (!value)
            {
                RunAsAdministratorAtStartup = false;
            }
        }
    }

    public bool RunAsAdministratorAtStartup
    {
        get => _runAsAdministratorAtStartup;
        set => SetProperty(ref _runAsAdministratorAtStartup, value);
    }

    public bool CanRunAsAdministratorAtStartup => LaunchAtStartup;

    public string BackupFolderPath
    {
        get => _backupFolderPath;
        set
        {
            if (!SetProperty(ref _backupFolderPath, value))
            {
                return;
            }

            OnPropertyChanged(nameof(BackupFolderDisplay));
        }
    }

    public string BackupFolderDisplay => string.IsNullOrWhiteSpace(BackupFolderPath)
        ? "백업 폴더가 선택되지 않았습니다."
        : BackupFolderPath;

    public string SpotifyConnectionStatusText
    {
        get => _spotifyConnectionStatusText;
        private set => SetProperty(ref _spotifyConnectionStatusText, value);
    }

    public bool IsSpotifyConnected
    {
        get => _isSpotifyConnected;
        private set
        {
            if (!SetProperty(ref _isSpotifyConnected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSpotifyDisconnected));
            OnPropertyChanged(nameof(ShowSpotifyConnectButton));
            OnPropertyChanged(nameof(ShowSpotifyConnectedActions));
        }
    }

    public bool IsSpotifyDisconnected => !IsSpotifyConnected;

    public bool ShowSpotifyConnectionFields
    {
        get => _showSpotifyConnectionFields;
        private set
        {
            if (!SetProperty(ref _showSpotifyConnectionFields, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowSpotifyConnectButton));
        }
    }

    public bool ShowSpotifyConnectButton => IsSpotifyDisconnected && !ShowSpotifyConnectionFields;

    public bool ShowSpotifyConnectedActions => IsSpotifyConnected;

    public bool IsSpotifyConnectionBusy
    {
        get => _isSpotifyConnectionBusy;
        private set => SetProperty(ref _isSpotifyConnectionBusy, value);
    }

    public string SpotifyClientIdInput
    {
        get => _spotifyClientIdInput;
        set => SetProperty(ref _spotifyClientIdInput, value);
    }

    public string SpotifyRedirectUri => _spotifyConnectionUseCase.RedirectUri;

    public string SpotifyClientIdHelp =>
        "Client Secret은 입력하지 않습니다. Spotify Developer Dashboard에서 Client ID만 복사해 주세요.";

    public string SpotifySetupIntro =>
        "Spotify 비밀번호나 Client Secret은 입력하지 않습니다. Dashboard에서 앱을 만들고 Client ID만 복사해 연결합니다.";

    public string SpotifyAppNameExample => "DeckDeckDeck Local";

    public string SpotifyAppDescriptionExample => "Local Spotify control app for personal playback";

    public string SpotifyRedirectUriHelp =>
        "Redirect URI는 Spotify 로그인이 끝난 뒤 DeckDeckDeck으로 돌아오기 위한 주소입니다. 아래 값을 그대로 등록해 주세요.";

    public string SpotifyTroubleshootingText =>
        "연결이 안 되면 Redirect URI가 한 글자도 틀리지 않았는지, Client ID를 제대로 복사했는지, Spotify 앱에서 음악을 한 번 재생했는지 확인해 주세요. 재생 제어는 Spotify Premium 계정이 필요할 수 있습니다.";

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand SaveCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand ChooseBackupFolderCommand { get; }

    public ICommand CreateManualBackupCommand { get; }

    public ICommand RestoreBackupCommand { get; }

    public ICommand ShowSpotifyConnectionFieldsCommand { get; }

    public ICommand OpenSpotifyDeveloperDashboardCommand { get; }

    public ICommand CopySpotifyAppNameExampleCommand { get; }

    public ICommand CopySpotifyAppDescriptionExampleCommand { get; }

    public ICommand CopySpotifyRedirectUriCommand { get; }

    public ICommand StartSpotifyConnectionCommand { get; }

    public ICommand DisconnectSpotifyCommand { get; }

    private void Save()
    {
        try
        {
            var result = _saveAppPreferencesUseCase.Execute(new SaveAppPreferencesRequest(
                new SaveSettingsRequest(
                    BringWindowToFrontOnHotkey,
                    AutoHideAfterPaste,
                    RestoreClipboardAfterPaste,
                    AutoBackupEnabled,
                    BackupFolderPath),
                new StartupRegistrationSettings(
                    LaunchAtStartup,
                    RunAsAdministratorAtStartup)));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? string.Empty;
                if (result.FailureKind == SaveAppPreferencesFailureKind.StartupRegistration)
                {
                    _showStatus(ErrorMessage);
                }

                return;
            }

            _showStatus("설정을 저장했습니다.");
            _afterSave();
        }
        catch (Exception ex)
        {
            ErrorMessage = "설정을 저장하지 못했습니다.";
            _loggingService?.Log("Settings save failed.", ex);
        }
    }

    private void ChooseBackupFolder()
    {
        var selectedPath = _dialogService.SelectBackupFolder();
        if (selectedPath is null)
        {
            return;
        }

        BackupFolderPath = selectedPath;
        ErrorMessage = string.Empty;
    }

    private void CreateManualBackup()
    {
        var result = _createManualBackupUseCase.Execute(BackupFolderPath);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage ?? "백업을 만들지 못했습니다.";
            _showStatus(ErrorMessage);
            return;
        }

        _settings.LastBackupCreatedAt = _loadSettingsUseCase.Execute().LastBackupCreatedAt;
        ErrorMessage = string.Empty;
        _showStatus($"백업을 만들었습니다: {Path.GetFileName(result.BackupPath)}");
    }

    private void RestoreBackup()
    {
        var selectedPath = _dialogService.SelectBackupZipFile();
        if (selectedPath is null)
        {
            return;
        }

        if (!_dialogService.Confirm(
            "백업 ZIP 복원",
            "현재 설정과 키 슬롯이 백업 내용으로 바뀌며, 복원 전 안전 백업을 만듭니다. 계속할까요?"))
        {
            return;
        }

        var result = _restoreBackupUseCase.Execute(selectedPath);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage ?? "백업 ZIP을 복원하지 못했습니다.";
            _showStatus(ErrorMessage);
            return;
        }

        var safetyBackupName = Path.GetFileName(result.SafetyBackupPath);
        var statusMessage = $"백업 ZIP 복원이 완료되었습니다. 앱을 다시 시작해 주세요. 안전 백업: {safetyBackupName}";
        ErrorMessage = string.Empty;
        _showStatus(statusMessage);
        _dialogService.ShowInformation(
            "백업 ZIP 복원 완료",
            "백업 ZIP 복원이 완료되었습니다.\n앱을 닫았다가 다시 열면 복원된 설정과 키 슬롯이 표시됩니다.");
    }

    private void RevealSpotifyConnectionFields()
    {
        SpotifyClientIdInput = _spotifyConnectionUseCase.GetSavedClientId();
        ShowSpotifyConnectionFields = true;
        ErrorMessage = string.Empty;
    }

    private void OpenSpotifyDeveloperDashboard()
    {
        try
        {
            if (!_spotifyConnectionUseCase.OpenDashboard())
            {
                ErrorMessage = "Spotify Developer Dashboard를 열지 못했습니다.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Spotify Developer Dashboard를 열지 못했습니다.";
            _loggingService?.Log("Spotify Developer Dashboard launch failed.", ex);
        }
    }

    private void CopySpotifyAppNameExample()
    {
        CopySpotifySetupValue(
            SpotifyAppNameExample,
            "Spotify App name 예시를 복사했습니다.",
            "Spotify App name 예시를 복사하지 못했습니다. 예시를 직접 선택해서 복사해 주세요.",
            "Spotify app name example copy failed.");
    }

    private void CopySpotifyAppDescriptionExample()
    {
        CopySpotifySetupValue(
            SpotifyAppDescriptionExample,
            "Spotify App description 예시를 복사했습니다.",
            "Spotify App description 예시를 복사하지 못했습니다. 예시를 직접 선택해서 복사해 주세요.",
            "Spotify app description example copy failed.");
    }

    private void CopySpotifyRedirectUri()
    {
        CopySpotifySetupValue(
            SpotifyRedirectUri,
            "Spotify Redirect URI를 복사했습니다.",
            "Redirect URI를 복사하지 못했습니다. 주소를 직접 선택해서 복사해 주세요.",
            "Spotify redirect URI copy failed.");
    }

    private void CopySpotifySetupValue(
        string value,
        string successMessage,
        string failureMessage,
        string logMessage)
    {
        try
        {
            _clipboardService.SetText(value);
            ErrorMessage = string.Empty;
            _showStatus(successMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = failureMessage;
            _loggingService?.Log(logMessage, ex);
        }
    }

    private async Task StartSpotifyConnectionAsync()
    {
        if (IsSpotifyConnectionBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SpotifyClientIdInput))
        {
            ErrorMessage = "Spotify Client ID를 입력해 주세요.";
            return;
        }

        try
        {
            IsSpotifyConnectionBusy = true;
            ErrorMessage = string.Empty;
            var result = await _spotifyConnectionUseCase.ConnectAsync(SpotifyClientIdInput);
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "Spotify 연결에 실패했습니다.";
                _showStatus(ErrorMessage);
                return;
            }

            RefreshSpotifyConnectionState();
            ShowSpotifyConnectionFields = false;
            _showStatus("Spotify 연결됨.");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Spotify 연결에 실패했습니다.";
            _loggingService?.Log("Spotify connection failed.", ex);
        }
        finally
        {
            IsSpotifyConnectionBusy = false;
        }
    }

    private void DisconnectSpotify()
    {
        try
        {
            _spotifyConnectionUseCase.Disconnect();
            SpotifyClientIdInput = string.Empty;
            ShowSpotifyConnectionFields = false;
            RefreshSpotifyConnectionState();
            ErrorMessage = string.Empty;
            _showStatus("Spotify 연결을 해제했습니다.");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Spotify 연결 해제에 실패했습니다.";
            _loggingService?.Log("Spotify disconnect failed.", ex);
        }
    }

    private void RefreshSpotifyConnectionState()
    {
        var state = _spotifyConnectionUseCase.GetState();
        IsSpotifyConnected = state.IsConnected;
        SpotifyConnectionStatusText = state.IsConnected
            ? $"Spotify 연결되어 있음{FormatSpotifyDisplayName(state)}"
            : "Spotify 연결되어 있지 않음";
    }

    private static string FormatSpotifyDisplayName(SpotifyConnectionState state)
    {
        return string.IsNullOrWhiteSpace(state.DisplayName)
            ? string.Empty
            : $" ({state.DisplayName})";
    }

}

