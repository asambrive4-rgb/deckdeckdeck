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
    private readonly LoggingService? _loggingService;
    private readonly SettingsService _settingsService;
    private readonly Action<string> _showStatus;
    private readonly AppSettings _settings;
    private bool _autoHideAfterPaste;
    private bool _bringWindowToFrontOnHotkey;
    private string _errorMessage = string.Empty;
    private bool _restoreClipboardAfterPaste;

    public SettingsViewModel(
        SettingsService settingsService,
        Action cancel,
        Action afterSave,
        Action<string> showStatus,
        LoggingService? loggingService = null)
    {
        _settingsService = settingsService;
        _cancel = cancel;
        _afterSave = afterSave;
        _showStatus = showStatus;
        _loggingService = loggingService;
        _settings = settingsService.Load();

        _bringWindowToFrontOnHotkey = _settings.BringWindowToFrontOnHotkey;
        _autoHideAfterPaste = _settings.AutoHideAfterPaste;
        _restoreClipboardAfterPaste = _settings.RestoreClipboardAfterPaste;

        SaveCommand = new RelayCommand(Save);
        BackCommand = new RelayCommand(_cancel);
    }

    public string Title => "설정";

    public string HomeHotkey => _settings.HomeHotkey;

    public string DirectCategoryHotkeys => _settings.DirectCategoryHotkeys;

    public string HomeHotkeyText => $"홈: {HomeHotkey}";

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

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand SaveCommand { get; }

    public ICommand BackCommand { get; }

    private void Save()
    {
        try
        {
            _settings.BringWindowToFrontOnHotkey = BringWindowToFrontOnHotkey;
            _settings.AutoHideAfterPaste = AutoHideAfterPaste;
            _settings.RestoreClipboardAfterPaste = RestoreClipboardAfterPaste;
            _settingsService.Save(_settings);
            _showStatus("설정을 저장했습니다.");
            _afterSave();
        }
        catch (Exception ex)
        {
            ErrorMessage = "설정을 저장하지 못했습니다.";
            _loggingService?.Log("Settings save failed.", ex);
        }
    }
}
