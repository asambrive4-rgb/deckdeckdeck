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

        _autoHideAfterPaste = _settings.AutoHideAfterPaste;
        _restoreClipboardAfterPaste = _settings.RestoreClipboardAfterPaste;

        SaveCommand = new RelayCommand(Save);
        BackCommand = new RelayCommand(_cancel);
    }

    public string Title => "Settings";

    public string HomeHotkey => _settings.HomeHotkey;

    public string DirectCategoryHotkeys => _settings.DirectCategoryHotkeys;

    public string HomeHotkeyText => $"Home: {HomeHotkey}";

    public string DirectCategoryHotkeysText => $"Direct category: {DirectCategoryHotkeys}";

    public string AdminPermissionNotice =>
        "Administrator apps, protected input fields, security software, and some games may block paste input unless DeckDeckDeck runs with matching permissions.";

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
            _settings.AutoHideAfterPaste = AutoHideAfterPaste;
            _settings.RestoreClipboardAfterPaste = RestoreClipboardAfterPaste;
            _settingsService.Save(_settings);
            _showStatus("Settings saved.");
            _afterSave();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Settings could not be saved.";
            _loggingService?.Log("Settings save failed.", ex);
        }
    }
}
