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
    private readonly EditableImageState _imageState;
    private readonly LoggingService? _loggingService;
    private readonly Guid? _categoryId;
    private bool _originalIsSlotEnabled;
    private readonly SettingsService? _settingsService;
    private readonly Action<string> _showStatus;
    private readonly ThumbnailService? _thumbnailService;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isSlotEnabled;
    private string _name = string.Empty;

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
        _imageState = new EditableImageState(category?.ImagePath, category?.ThumbnailPath, thumbnailService);

        _name = category?.Name ?? string.Empty;
        _description = category?.Description ?? string.Empty;
        _originalIsSlotEnabled = LoadSlotEnabledState();
        _isSlotEnabled = _originalIsSlotEnabled;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
    }

    public string Title => IsExisting ? "카테고리 편집" : "새 카테고리";

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

    public string? ThumbnailPath => _imageState.ThumbnailPath;

    public bool HasImage => _imageState.HasImage;

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

            ErrorMessage = "카테고리 이름을 입력해 주세요.";
            return;
        }

        if (!SaveSlotEnabled())
        {
            return;
        }

        var category = _categoryId.HasValue
            ? _categoryService.Update(_categoryId.Value, Name, Description, _imageState.ImagePath, _imageState.ThumbnailPath)
            : _categoryService.Create(SlotKey, Name, Description, _imageState.ImagePath, _imageState.ThumbnailPath);

        _imageState.DeleteOriginalImageIfReplaced();
        _imageState.MarkCurrentAsOriginal();
        _showStatus($"{category.Name} 저장됨.");
        _afterSave(category);
    }

    private void Cancel()
    {
        _imageState.DeleteCurrentUnsavedImage();
        _cancel();
    }

    private void Delete()
    {
        if (!_categoryId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "카테고리 삭제",
            "이 카테고리와 안에 있는 모든 실행 항목을 삭제할까요?");

        if (!confirmed)
        {
            return;
        }

        _imageState.DeleteCurrentUnsavedImage();
        var deletedImageFiles = _categoryService.Delete(_categoryId.Value);
        if (_thumbnailService is not null)
        {
            foreach (var imageFiles in deletedImageFiles)
            {
                _thumbnailService.DeleteImageFiles(imageFiles);
            }
        }

        _showStatus("카테고리를 삭제했습니다.");
        _afterDelete();
    }

    private void ChooseImage()
    {
        if (_thumbnailService is null)
        {
            ErrorMessage = "이미지 저장소가 준비되지 않았습니다.";
            return;
        }

        var selectedPath = _dialogService.SelectImageFile();
        if (selectedPath is null)
        {
            return;
        }

        try
        {
            _imageState.ReplaceWithStoredImage(selectedPath);
            NotifyImageChanged();
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
        _imageState.RemoveImage();
        NotifyImageChanged();
    }

    private void NotifyImageChanged()
    {
        OnPropertyChanged(nameof(ThumbnailPath));
        OnPropertyChanged(nameof(HasImage));
    }

    private bool LoadSlotEnabledState()
    {
        var settings = _settingsService?.Load();

        return settings is null
            || !settings.EnabledCategorySlotKeys.TryGetValue(SlotKey, out var enabled)
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

        _imageState.DeleteCurrentUnsavedImage();
        _showStatus($"슬롯 {KeyText} 설정을 저장했습니다.");
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
            _settingsService.SetCategorySlotEnabled(SlotKey, IsSlotEnabled);
            _originalIsSlotEnabled = IsSlotEnabled;

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "슬롯 설정을 저장하지 못했습니다.";
            _loggingService?.Log($"Setting save failed for category slot {SlotKey}.", ex);

            return false;
        }
    }
}
