using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class CategoryEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Category> _afterSave;
    private readonly Action _cancel;
    private readonly CategoryService _categoryService;
    private readonly DialogService _dialogService;
    private readonly LoggingService? _loggingService;
    private readonly Guid? _categoryId;
    private string? _originalImagePath;
    private bool _originalIsSlotEnabled;
    private string? _originalThumbnailPath;
    private readonly SettingsService? _settingsService;
    private readonly Action<string> _showStatus;
    private readonly ThumbnailService? _thumbnailService;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private string? _imagePath;
    private bool _isSlotEnabled;
    private string _name = string.Empty;
    private string? _thumbnailPath;

    public CategoryEditViewModel(
        SlotKey slotKey,
        Category? category,
        CategoryService categoryService,
        DialogService dialogService,
        Action cancel,
        Action<Category> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        ThumbnailService? thumbnailService = null,
        SettingsService? settingsService = null,
        LoggingService? loggingService = null)
    {
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _categoryId = category?.Id;
        _categoryService = categoryService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _loggingService = loggingService;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;
        _thumbnailService = thumbnailService;
        _originalImagePath = category?.ImagePath;
        _originalThumbnailPath = category?.ThumbnailPath;

        _name = category?.Name ?? string.Empty;
        _description = category?.Description ?? string.Empty;
        _imagePath = category?.ImagePath;
        _thumbnailPath = category?.ThumbnailPath;
        _originalIsSlotEnabled = LoadSlotEnabledState();
        _isSlotEnabled = _originalIsSlotEnabled;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
    }

    public string Title => IsExisting ? "Edit Category" : "New Category";

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public bool IsExisting => _categoryId.HasValue;

    public bool CanDelete => IsExisting;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
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
        if (string.IsNullOrWhiteSpace(Name))
        {
            if (TrySaveSlotOnly())
            {
                return;
            }

            ErrorMessage = "Category name is required.";
            return;
        }

        if (!SaveSlotEnabled())
        {
            return;
        }

        var category = _categoryId.HasValue
            ? _categoryService.Update(_categoryId.Value, Name, Description, _imagePath, ThumbnailPath)
            : _categoryService.Create(SlotKey, Name, Description, _imagePath, ThumbnailPath);

        DeleteOriginalImageIfReplaced();
        _originalImagePath = _imagePath;
        _originalThumbnailPath = ThumbnailPath;
        _showStatus($"{category.Name} saved.");
        _afterSave(category);
    }

    private void Cancel()
    {
        DeleteCurrentUnsavedImage();
        _cancel();
    }

    private void Delete()
    {
        if (!_categoryId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "Delete category",
            "Delete this category and all snippets inside it?");

        if (!confirmed)
        {
            return;
        }

        DeleteCurrentUnsavedImage();
        var deletedImageFiles = _categoryService.Delete(_categoryId.Value);
        if (_thumbnailService is not null)
        {
            foreach (var imageFiles in deletedImageFiles)
            {
                _thumbnailService.DeleteImageFiles(imageFiles);
            }
        }

        _showStatus("Category deleted.");
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
            _loggingService?.Log($"Image processing failed for category slot {SlotKey}.", ex);
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
            _loggingService?.Log($"Setting save failed for category slot {SlotKey}.", ex);

            return false;
        }
    }
}
