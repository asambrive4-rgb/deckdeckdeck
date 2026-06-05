using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Action _cancel;
    private readonly Action _afterSave;
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly BackupService? _backupService;
    private readonly DialogService _dialogService;
    private readonly LoggingService? _loggingService;
    private readonly SettingsService _settingsService;
    private readonly Action<string> _showStatus;
    private readonly AppSettings _settings;
    private bool _autoHideAfterPaste;
    private bool _autoBackupEnabled;
    private string _backupFolderPath = string.Empty;
    private bool _bringWindowToFrontOnHotkey;
    private string _errorMessage = string.Empty;
    private bool _restoreClipboardAfterPaste;

    public SettingsViewModel(
        SettingsService settingsService,
        Action cancel,
        Action afterSave,
        Action<string> showStatus,
        LoggingService? loggingService = null,
        BackupService? backupService = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null,
        DialogService? dialogService = null)
    {
        _settingsService = settingsService;
        _cancel = cancel;
        _afterSave = afterSave;
        _showStatus = showStatus;
        _loggingService = loggingService;
        _backupService = backupService;
        _autoBackupCoordinator = autoBackupCoordinator;
        _dialogService = dialogService ?? new DialogService();
        _settings = settingsService.Load();

        _bringWindowToFrontOnHotkey = _settings.BringWindowToFrontOnHotkey;
        _autoHideAfterPaste = _settings.AutoHideAfterPaste;
        _restoreClipboardAfterPaste = _settings.RestoreClipboardAfterPaste;
        _autoBackupEnabled = _settings.AutoBackupEnabled;
        _backupFolderPath = _settings.BackupFolderPath;

        SaveCommand = new RelayCommand(Save);
        BackCommand = new RelayCommand(_cancel);
        ChooseBackupFolderCommand = new RelayCommand(ChooseBackupFolder);
        CreateManualBackupCommand = new RelayCommand(CreateManualBackup);
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

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand SaveCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand ChooseBackupFolderCommand { get; }

    public ICommand CreateManualBackupCommand { get; }

    private void Save()
    {
        try
        {
            var validationError = ValidateBackupSettings(requireFolder: AutoBackupEnabled);
            if (validationError is not null)
            {
                ErrorMessage = validationError;
                return;
            }

            _settings.BringWindowToFrontOnHotkey = BringWindowToFrontOnHotkey;
            _settings.AutoHideAfterPaste = AutoHideAfterPaste;
            _settings.RestoreClipboardAfterPaste = RestoreClipboardAfterPaste;
            _settings.AutoBackupEnabled = AutoBackupEnabled;
            _settings.BackupFolderPath = BackupFolderPath.Trim();
            _settings.LastBackupCreatedAt = _settingsService.Load().LastBackupCreatedAt;
            _settingsService.Save(_settings);
            _autoBackupCoordinator?.RequestAutoBackup();
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
        var validationError = ValidateBackupSettings(requireFolder: true);
        if (validationError is not null)
        {
            ErrorMessage = validationError;
            _showStatus(validationError);
            return;
        }

        if (_backupService is null)
        {
            ErrorMessage = "백업 서비스가 준비되지 않았습니다.";
            _showStatus(ErrorMessage);
            return;
        }

        var result = _backupService.CreateManualBackup(BackupFolderPath);
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
