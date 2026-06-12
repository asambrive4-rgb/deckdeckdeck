using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Action _cancel;
    private readonly Action _afterSave;
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly BackupService? _backupService;
    private readonly CreateManualBackupUseCase _createManualBackupUseCase;
    private readonly DialogService _dialogService;
    private readonly LoggingService? _loggingService;
    private readonly SettingsService _settingsService;
    private readonly Action<string> _showStatus;
    private readonly IClipboardService _clipboardService;
    private readonly ISpotifyConnectionService _spotifyConnectionService;
    private readonly IUrlLaunchService _urlLaunchService;
    private readonly SaveSettingsUseCase _saveSettingsUseCase;
    private readonly AppSettings _settings;
    private bool _autoHideAfterPaste;
    private bool _autoBackupEnabled;
    private string _backupFolderPath = string.Empty;
    private bool _bringWindowToFrontOnHotkey;
    private string _errorMessage = string.Empty;
    private bool _isSpotifyConnected;
    private bool _isSpotifyConnectionBusy;
    private bool _restoreClipboardAfterPaste;
    private bool _showSpotifyConnectionFields;
    private string _spotifyClientIdInput = string.Empty;
    private string _spotifyConnectionStatusText = string.Empty;

    public SettingsViewModel(
        SettingsService settingsService,
        Action cancel,
        Action afterSave,
        Action<string> showStatus,
        LoggingService? loggingService = null,
        BackupService? backupService = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null,
        DialogService? dialogService = null,
        ISpotifyConnectionService? spotifyConnectionService = null,
        IUrlLaunchService? urlLaunchService = null,
        IClipboardService? clipboardService = null)
    {
        _settingsService = settingsService;
        _cancel = cancel;
        _afterSave = afterSave;
        _showStatus = showStatus;
        _loggingService = loggingService;
        _backupService = backupService;
        _autoBackupCoordinator = autoBackupCoordinator;
        _saveSettingsUseCase = new SaveSettingsUseCase(settingsService, backupService, autoBackupCoordinator);
        _createManualBackupUseCase = new CreateManualBackupUseCase(backupService);
        _dialogService = dialogService ?? new DialogService();
        _clipboardService = clipboardService ?? new WpfClipboardService();
        _urlLaunchService = urlLaunchService ?? new UrlLaunchService();
        _spotifyConnectionService = spotifyConnectionService
            ?? new SpotifyConnectionService(settingsService, _urlLaunchService);
        _settings = settingsService.Load();

        _bringWindowToFrontOnHotkey = _settings.BringWindowToFrontOnHotkey;
        _autoHideAfterPaste = _settings.AutoHideAfterPaste;
        _restoreClipboardAfterPaste = _settings.RestoreClipboardAfterPaste;
        _autoBackupEnabled = _settings.AutoBackupEnabled;
        _backupFolderPath = _settings.BackupFolderPath;
        RefreshSpotifyConnectionState();

        SaveCommand = new RelayCommand(Save);
        BackCommand = new RelayCommand(_cancel);
        ChooseBackupFolderCommand = new RelayCommand(ChooseBackupFolder);
        CreateManualBackupCommand = new RelayCommand(CreateManualBackup);
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

    public string SpotifyRedirectUri => _spotifyConnectionService.RedirectUri;

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
            var result = _saveSettingsUseCase.Execute(new SaveSettingsRequest(
                BringWindowToFrontOnHotkey,
                AutoHideAfterPaste,
                RestoreClipboardAfterPaste,
                AutoBackupEnabled,
                BackupFolderPath));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? string.Empty;
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
        if (_backupService is null)
        {
            ErrorMessage = "백업 서비스가 준비되지 않았습니다.";
            _showStatus(ErrorMessage);
            return;
        }

        var result = _createManualBackupUseCase.Execute(BackupFolderPath);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage ?? "백업을 만들지 못했습니다.";
            _showStatus(ErrorMessage);
            return;
        }

        _settings.LastBackupCreatedAt = _settingsService.Load().LastBackupCreatedAt;
        ErrorMessage = string.Empty;
        _showStatus($"백업을 만들었습니다: {Path.GetFileName(result.BackupPath)}");
    }

    private void RevealSpotifyConnectionFields()
    {
        SpotifyClientIdInput = _settingsService.Load().SpotifyClientId;
        ShowSpotifyConnectionFields = true;
        ErrorMessage = string.Empty;
    }

    private void OpenSpotifyDeveloperDashboard()
    {
        try
        {
            if (!_urlLaunchService.TryLaunch(_spotifyConnectionService.DashboardUrl))
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
            var result = await _spotifyConnectionService.ConnectAsync(SpotifyClientIdInput);
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
            _spotifyConnectionService.Disconnect();
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
        var latestSettings = _settingsService.Load();
        var connected = IsSpotifySettingsConnected(latestSettings);
        IsSpotifyConnected = connected;
        SpotifyConnectionStatusText = connected
            ? $"Spotify 연결되어 있음{FormatSpotifyDisplayName(latestSettings)}"
            : "Spotify 연결되어 있지 않음";
    }

    private static bool IsSpotifySettingsConnected(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.SpotifyClientId)
            && !string.IsNullOrWhiteSpace(settings.SpotifyAccessToken)
            && !string.IsNullOrWhiteSpace(settings.SpotifyRefreshToken);
    }

    private static string FormatSpotifyDisplayName(AppSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.SpotifyConnectedUserDisplayName)
            ? string.Empty
            : $" ({settings.SpotifyConnectedUserDisplayName})";
    }

    private string? ValidateBackupSettings(bool requireFolder)
    {
        if (requireFolder && string.IsNullOrWhiteSpace(BackupFolderPath))
        {
            return "백업 폴더를 선택해 주세요.";
        }

        if (string.IsNullOrWhiteSpace(BackupFolderPath))
        {
            return null;
        }

        return _backupService?.ValidateBackupFolder(BackupFolderPath);
    }
}
