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
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly Action _cancel;
    private readonly CategoryService _categoryService;
    private readonly CategoryTransferService _categoryTransferService;
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
    private CategoryTransferTargetSlot? _selectedTransferTarget;

    public CategoryEditViewModel(
        SlotKey slotKey,
        Category? category,
        CategoryService categoryService,
        CategoryTransferService categoryTransferService,
        DialogService dialogService,
        Action cancel,
        Action<Category> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        ThumbnailService? thumbnailService = null,
        SettingsService? settingsService = null,
        LoggingService? loggingService = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _categoryId = category?.Id;
        _categoryService = categoryService;
        _categoryTransferService = categoryTransferService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _loggingService = loggingService;
        _autoBackupCoordinator = autoBackupCoordinator;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;
        _thumbnailService = thumbnailService;
        _imageState = new EditableImageState(category?.ImagePath, category?.ThumbnailPath, thumbnailService);

        _name = category?.Name ?? string.Empty;
        _description = category?.Description ?? string.Empty;
        TransferTargetSlots = BuildTransferTargetSlots();
        _selectedTransferTarget = TransferTargetSlots.FirstOrDefault();
        _originalIsSlotEnabled = LoadSlotEnabledState();
        _isSlotEnabled = _originalIsSlotEnabled;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        CopyCategoryCommand = new RelayCommand(CopyCategory);
        MoveCategoryCommand = new RelayCommand(MoveCategory);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
    }

    public string Title => IsExisting ? "카테고리 편집" : "새 카테고리";

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public bool IsExisting => _categoryId.HasValue;

    public bool CanDelete => IsExisting;

    public bool CanTransfer => IsExisting;

    public IReadOnlyList<CategoryTransferTargetSlot> TransferTargetSlots { get; }

    public CategoryTransferTargetSlot? SelectedTransferTarget
    {
        get => _selectedTransferTarget;
        set => SetProperty(ref _selectedTransferTarget, value);
    }

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

    public ICommand CopyCategoryCommand { get; }

    public ICommand MoveCategoryCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    private void Save()
    {
        var category = SaveCategory();
        if (category is null)
        {
            return;
        }

        _showStatus($"{category.Name} 저장됨.");
        _afterSave(category);
    }

    private Category? SaveCategory()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            if (TrySaveSlotOnly())
            {
                return null;
            }

            ErrorMessage = "카테고리 이름을 입력해 주세요.";
            return null;
        }

        if (!SaveSlotEnabled())
        {
            return null;
        }

        var category = _categoryId.HasValue
            ? _categoryService.Update(_categoryId.Value, Name, Description, _imageState.ImagePath, _imageState.ThumbnailPath)
            : _categoryService.Create(SlotKey, Name, Description, _imageState.ImagePath, _imageState.ThumbnailPath);

        _imageState.DeleteOriginalImageIfReplaced();
        _imageState.MarkCurrentAsOriginal();
        _autoBackupCoordinator?.RequestAutoBackup();
        ErrorMessage = string.Empty;

        return category;
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
        _autoBackupCoordinator?.RequestAutoBackup();
        _afterDelete();
    }

    private void CopyCategory()
    {
        TransferCategory(
            "복사",
            targetSlotKey => _categoryTransferService.CopyCategory(
                _categoryId!.Value,
                targetSlotKey,
                IsSlotEnabled),
            targetSlotKey => $"슬롯 {targetSlotKey.GetDisplayText()}에 카테고리를 복사했습니다.");
    }

    private void MoveCategory()
    {
        TransferCategory(
            "이동",
            targetSlotKey => _categoryTransferService.MoveCategory(
                _categoryId!.Value,
                SlotKey,
                targetSlotKey,
                IsSlotEnabled),
            targetSlotKey => $"슬롯 {targetSlotKey.GetDisplayText()}로 카테고리를 이동했습니다.");
    }

    private void TransferCategory(
        string actionText,
        Func<SlotKey, Category> transfer,
        Func<SlotKey, string> getStatusMessage)
    {
        if (!_categoryId.HasValue)
        {
            ErrorMessage = "저장된 카테고리만 복사하거나 이동할 수 있습니다.";
            return;
        }

        if (SelectedTransferTarget is null)
        {
            ErrorMessage = "대상 슬롯을 선택해 주세요.";
            return;
        }

        var targetSlotKey = SelectedTransferTarget.SlotKey;
        if (!ConfirmOverwriteIfNeeded(targetSlotKey, actionText))
        {
            return;
        }

        if (SaveCategory() is null)
        {
            return;
        }

        try
        {
            var transferredCategory = transfer(targetSlotKey);
            _autoBackupCoordinator?.RequestAutoBackup();
            _afterSave(transferredCategory);
            _showStatus(getStatusMessage(targetSlotKey));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"카테고리 {actionText}에 실패했습니다.";
            _loggingService?.Log($"Category {actionText} failed for slot {SlotKey}.", ex);
        }
    }

    private bool ConfirmOverwriteIfNeeded(SlotKey targetSlotKey, string actionText)
    {
        var targetCategory = _categoryService.GetBySlotKey(targetSlotKey);
        if (targetCategory is null || targetCategory.Id == _categoryId)
        {
            return true;
        }

        return _dialogService.Confirm(
            $"카테고리 {actionText}",
            $"슬롯 {targetSlotKey.GetDisplayText()}에 이미 '{targetCategory.Name}' 카테고리가 있습니다.\n기존 카테고리와 안의 실행 항목을 덮어쓸까요?");
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

    private IReadOnlyList<CategoryTransferTargetSlot> BuildTransferTargetSlots()
    {
        var categoriesBySlot = _categoryService
            .GetAll()
            .Where(category => !_categoryId.HasValue || category.Id != _categoryId.Value)
            .ToDictionary(category => category.SlotKey);

        return SlotKeyCatalog.All
            .Where(slotKey => slotKey != SlotKey)
            .Select(slotKey =>
            {
                categoriesBySlot.TryGetValue(slotKey, out var category);

                return new CategoryTransferTargetSlot(
                    slotKey,
                    FormatTransferTargetLabel(slotKey, category));
            })
            .ToList();
    }

    private static string FormatTransferTargetLabel(SlotKey slotKey, Category? category)
    {
        var prefix = $"슬롯 {slotKey.GetDisplayText()}";

        return category is null
            ? $"{prefix} - 비어 있음"
            : $"{prefix} - {category.Name}";
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
        _autoBackupCoordinator?.RequestAutoBackup();
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

public sealed class CategoryTransferTargetSlot
{
    public CategoryTransferTargetSlot(SlotKey slotKey, string label)
    {
        SlotKey = slotKey;
        Label = label;
    }

    public SlotKey SlotKey { get; }

    public string Label { get; }
}
