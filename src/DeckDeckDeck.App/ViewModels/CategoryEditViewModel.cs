using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

public sealed class CategoryEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Category> _afterSave;
    private readonly Action _cancel;
    private readonly DeleteCategoryUseCase _deleteCategoryUseCase;
    private readonly IDialogAdapter _dialogService;
    private readonly CategoryEditDraft _draft;
    private readonly IAppLogger? _loggingService;
    private readonly Guid? _categoryId;
    private bool _originalIsSlotEnabled;
    private readonly SaveCategoryUseCase _saveCategoryUseCase;
    private readonly Action<string> _showStatus;
    private readonly TransferCategoryUseCase _transferCategoryUseCase;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isSlotEnabled;
    private string _name = string.Empty;
    private CategoryTransferTargetSlot? _selectedTransferTarget;

    public CategoryEditViewModel(
        SlotKey slotKey,
        Category? category,
        CategoryEditorState editorState,
        SaveCategoryUseCase saveCategoryUseCase,
        DeleteCategoryUseCase deleteCategoryUseCase,
        TransferCategoryUseCase transferCategoryUseCase,
        IDialogAdapter dialogService,
        Action cancel,
        Action<Category> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        IImageFileRepository? thumbnailService = null,
        IAppLogger? loggingService = null,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _categoryId = category?.Id;
        _saveCategoryUseCase = saveCategoryUseCase;
        _deleteCategoryUseCase = deleteCategoryUseCase;
        _transferCategoryUseCase = transferCategoryUseCase;
        _dialogService = dialogService;
        _loggingService = loggingService;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;
        _draft = CategoryEditDraft.FromCategory(
            category,
            thumbnailService,
            storedImagePathResolver);

        _name = category?.Name ?? string.Empty;
        _description = category?.Description ?? string.Empty;
        TransferTargetSlots = BuildTransferTargetSlots(editorState.TransferTargets);
        _selectedTransferTarget = TransferTargetSlots.FirstOrDefault();
        _originalIsSlotEnabled = editorState.IsSlotEnabled;
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

    public string? ThumbnailPath => _draft.PreviewThumbnailPath;

    public bool HasImage => _draft.HasImage;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CopyCategoryCommand { get; }

    public ICommand MoveCategoryCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    public void DropImageFiles(IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count != 1)
        {
            ErrorMessage = "이미지는 하나만 드롭해 주세요.";
            return;
        }

        ReplaceImageFromPath(sourcePaths[0]);
    }

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
        var result = _saveCategoryUseCase.Execute(BuildSaveRequest());
        if (!result.Succeeded)
        {
            ErrorMessage = "카테고리 이름을 입력해 주세요.";
            return null;
        }

        if (result.SavedSlotOnly)
        {
            _originalIsSlotEnabled = IsSlotEnabled;
            _draft.DeleteCurrentUnsavedImage();
            _showStatus($"슬롯 {KeyText} 설정을 저장했습니다.");
            // afterDelete navigates back with cache invalidation (slot enablement changed).
            _afterDelete();
            return null;
        }

        var category = result.Category!;

        _draft.MarkSaved();
        _originalIsSlotEnabled = IsSlotEnabled;
        ErrorMessage = string.Empty;

        return category;
    }

    private SaveCategoryRequest BuildSaveRequest()
    {
        return new SaveCategoryRequest(
            SlotKey,
            _categoryId,
            Name,
            Description,
            _draft.ImagePath,
            _draft.ThumbnailPath,
            IsSlotEnabled,
            _originalIsSlotEnabled);
    }

    private void Cancel()
    {
        _draft.DeleteCurrentUnsavedImage();
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

        _draft.DeleteCurrentUnsavedImage();
        _deleteCategoryUseCase.Execute(_categoryId.Value);

        _showStatus("카테고리를 삭제했습니다.");
        _afterDelete();
    }

    private void CopyCategory()
    {
        TransferCategory(
            "복사",
            CategoryTransferOperation.Copy,
            targetSlotKey => $"슬롯 {targetSlotKey.GetDisplayText()}에 카테고리를 복사했습니다.");
    }

    private void MoveCategory()
    {
        TransferCategory(
            "이동",
            CategoryTransferOperation.Move,
            targetSlotKey => $"슬롯 {targetSlotKey.GetDisplayText()}로 카테고리를 이동했습니다.");
    }

    private void TransferCategory(
        string actionText,
        CategoryTransferOperation operation,
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
        try
        {
            var result = ExecuteTransferCategory(operation, targetSlotKey, overwriteConfirmed: false);
            if (result.NeedsOverwriteConfirmation)
            {
                var confirmed = _dialogService.Confirm(
                    $"카테고리 {actionText}",
                    $"슬롯 {targetSlotKey.GetDisplayText()}에 이미 '{result.ExistingTargetName}' 카테고리가 있습니다.\n기존 카테고리와 안의 실행 항목을 덮어쓸까요?");
                if (!confirmed)
                {
                    return;
                }

                result = ExecuteTransferCategory(operation, targetSlotKey, overwriteConfirmed: true);
            }

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? string.Empty;
                return;
            }

            var transferredCategory = result.Category!;
            _draft.MarkSaved();
            _originalIsSlotEnabled = IsSlotEnabled;
            _afterSave(transferredCategory);
            _showStatus(getStatusMessage(targetSlotKey));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"카테고리 {actionText}에 실패했습니다.";
            _loggingService?.Log($"Category {actionText} failed for slot {SlotKey}.", ex);
        }
    }

    private TransferCategoryResult ExecuteTransferCategory(
        CategoryTransferOperation operation,
        SlotKey targetSlotKey,
        bool overwriteConfirmed)
    {
        return _transferCategoryUseCase.Execute(new TransferCategoryRequest(
            _categoryId,
            SlotKey,
            targetSlotKey,
            IsSlotEnabled,
            operation,
            BuildSaveRequest(),
            overwriteConfirmed));
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
            _loggingService?.Log($"Image processing failed for category slot {SlotKey}.", ex);
        }
    }

    private void RemoveImage()
    {
        _draft.RemoveImage();
        NotifyImageChanged();
    }

    private void NotifyImageChanged()
    {
        OnPropertyChanged(nameof(ThumbnailPath));
        OnPropertyChanged(nameof(HasImage));
    }

    private static IReadOnlyList<CategoryTransferTargetSlot> BuildTransferTargetSlots(
        IReadOnlyList<TransferTargetState> transferTargets)
    {
        return transferTargets
            .Select(target => new CategoryTransferTargetSlot(
                target.SlotKey,
                FormatTransferTargetLabel(target)))
            .ToList();
    }

    private static string FormatTransferTargetLabel(TransferTargetState target)
    {
        var prefix = $"슬롯 {target.SlotKey.GetDisplayText()}";

        return string.IsNullOrWhiteSpace(target.ExistingTitle)
            ? $"{prefix} - 비어 있음"
            : $"{prefix} - {target.ExistingTitle}";
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

