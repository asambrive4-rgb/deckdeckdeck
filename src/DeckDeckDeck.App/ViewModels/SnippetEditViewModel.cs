using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SnippetEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Snippet> _afterSave;
    private readonly Action _cancel;
    private readonly DialogService _dialogService;
    private readonly LoggingService? _loggingService;
    private string? _originalImagePath;
    private bool _originalIsSlotEnabled;
    private string? _originalThumbnailPath;
    private readonly SettingsService? _settingsService;
    private readonly Guid? _snippetId;
    private readonly SnippetService _snippetService;
    private readonly Action<string> _showStatus;
    private readonly ThumbnailService? _thumbnailService;
    private string _content = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private string? _imagePath;
    private bool _isSlotEnabled;
    private string _snippetTitle = string.Empty;
    private string? _thumbnailPath;

    public SnippetEditViewModel(
        Category category,
        SlotKey slotKey,
        Snippet? snippet,
        SnippetService snippetService,
        DialogService dialogService,
        Action cancel,
        Action<Snippet> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        ThumbnailService? thumbnailService = null,
        SettingsService? settingsService = null,
        LoggingService? loggingService = null)
    {
        CategoryId = category.Id;
        CategoryName = category.Name;
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _snippetId = snippet?.Id;
        _snippetService = snippetService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _loggingService = loggingService;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;
        _thumbnailService = thumbnailService;
        _originalImagePath = snippet?.ImagePath;
        _originalThumbnailPath = snippet?.ThumbnailPath;

        _snippetTitle = snippet?.Title ?? string.Empty;
        _content = snippet?.Content ?? string.Empty;
        _description = snippet?.Description ?? string.Empty;
        _imagePath = snippet?.ImagePath;
        _thumbnailPath = snippet?.ThumbnailPath;
        _originalIsSlotEnabled = LoadSlotEnabledState();
        _isSlotEnabled = _originalIsSlotEnabled;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
    }

    public string Title => IsExisting ? "Edit Snippet" : "New Snippet";

    public Guid CategoryId { get; }

    public string CategoryName { get; }

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public bool IsExisting => _snippetId.HasValue;

    public bool CanDelete => IsExisting;

    public string SnippetTitle
    {
        get => _snippetTitle;
        set => SetProperty(ref _snippetTitle, value);
    }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
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

    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        private set
        {
            if (SetProperty(ref _thumbnailPath, value))
            {
                OnPropertyChanged(nameof(HasImage));
            }
        }
    }

    public bool HasImage => !string.IsNullOrWhiteSpace(ThumbnailPath);

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(SnippetTitle))
        {
            if (TrySaveSlotOnly())
            {
                return;
            }

            ErrorMessage = "Snippet title is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            if (TrySaveSlotOnly())
            {
                return;
            }

            ErrorMessage = "Snippet content is required.";
            return;
        }

        if (!SaveSlotEnabled())
        {
            return;
        }

        var snippet = _snippetId.HasValue
            ? _snippetService.Update(_snippetId.Value, SnippetTitle, Content, Description, _imagePath, ThumbnailPath)
            : _snippetService.Create(CategoryId, SlotKey, SnippetTitle, Content, Description, _imagePath, ThumbnailPath);

        DeleteOriginalImageIfReplaced();
        _originalImagePath = _imagePath;
        _originalThumbnailPath = ThumbnailPath;
        _showStatus($"{snippet.Title} saved.");
        _afterSave(snippet);
    }

    private void Cancel()
    {
        DeleteCurrentUnsavedImage();
        _cancel();
    }

    private void Delete()
    {
        if (!_snippetId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "Delete snippet",
            "Delete this snippet?");

        if (!confirmed)
        {
            return;
        }

        DeleteCurrentUnsavedImage();
        var deletedImageFiles = _snippetService.Delete(_snippetId.Value);
        _thumbnailService?.DeleteImageFiles(deletedImageFiles);

        _showStatus("Snippet deleted.");
        _afterDelete();
    }

    private void ChooseImage()
    {
        if (_thumbnailService is null)
        {
            ErrorMessage = "Image storage is not ready.";
            return;
        }

        var selectedPath = _dialogService.SelectImageFile();
        if (selectedPath is null)
        {
            return;
        }

        try
        {
            var storedImage = _thumbnailService.StoreImage(selectedPath);
            DeleteCurrentUnsavedImage();
            _imagePath = storedImage.ImagePath;
            ThumbnailPath = storedImage.ThumbnailPath;
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
        DeleteCurrentUnsavedImage();
        _imagePath = null;
        ThumbnailPath = null;
    }

    private void DeleteCurrentUnsavedImage()
    {
        if (_thumbnailService is null || IsCurrentOriginalImage())
        {
            return;
        }

        _thumbnailService.DeleteImageFiles(_imagePath, ThumbnailPath);
    }

    private void DeleteOriginalImageIfReplaced()
    {
        if (_thumbnailService is null || IsCurrentOriginalImage())
        {
            return;
        }

        _thumbnailService.DeleteImageFiles(_originalImagePath, _originalThumbnailPath);
    }

    private bool IsCurrentOriginalImage()
    {
        return SamePath(_imagePath, _originalImagePath)
            && SamePath(ThumbnailPath, _originalThumbnailPath);
    }

    private static bool SamePath(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private bool LoadSlotEnabledState()
    {
        var settings = _settingsService?.Load();

        return settings is null
            || !settings.EnabledSlotKeys.TryGetValue(SlotKey, out var enabled)
            || enabled;
    }

    private bool TrySaveSlotOnly()
    {
        if (IsExisting || IsSlotEnabled == _originalIsSlotEnabled)
        {
            return false;
        }

        if (!SaveSlotEnabled())
        {
            return true;
        }

        DeleteCurrentUnsavedImage();
        _showStatus($"{KeyText} slot updated.");
        _cancel();

        return true;
    }

    private bool SaveSlotEnabled()
    {
        if (_settingsService is null || IsSlotEnabled == _originalIsSlotEnabled)
        {
            return true;
        }

        try
        {
            _settingsService.SetSlotEnabled(SlotKey, IsSlotEnabled);
            _originalIsSlotEnabled = IsSlotEnabled;

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Slot setting could not be saved.";
            _loggingService?.Log($"Setting save failed for snippet slot {SlotKey}.", ex);

            return false;
        }
    }
}
