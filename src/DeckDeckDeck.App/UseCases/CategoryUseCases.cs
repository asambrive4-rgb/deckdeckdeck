using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class SaveCategoryUseCase
{
    private const string SlotSettingSaveFailedMessage = "슬롯 설정을 저장하지 못했습니다.";

    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsStore? _settingsStore;

    public SaveCategoryUseCase(
        ICategoryRepository categoryRepository,
        ISettingsStore? settingsStore = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _categoryRepository = categoryRepository;
        _settingsStore = settingsStore;
        _autoBackupRequester = autoBackupRequester;
    }

    public SaveCategoryResult Execute(SaveCategoryRequest request)
    {
        return Execute(request, requestAutoBackup: true);
    }

    internal SaveCategoryResult Execute(
        SaveCategoryRequest request,
        bool requestAutoBackup)
    {
        if (CategoryRules.ValidateName(request.Name) is { } validationError)
        {
            return TrySaveSlotOnly(request, requestAutoBackup)
                ?? SaveCategoryResult.Failure(validationError);
        }

        var slotSaveResult = SaveSlotEnabled(request);
        if (!slotSaveResult.Succeeded)
        {
            return SaveCategoryResult.Failure(slotSaveResult.ErrorMessage!);
        }

        var category = request.CategoryId.HasValue
            ? _categoryRepository.Update(
                request.CategoryId.Value,
                request.Name,
                request.Description,
                request.ImagePath,
                request.ThumbnailPath)
            : _categoryRepository.Create(
                request.SlotKey,
                request.Name,
                request.Description,
                request.ImagePath,
                request.ThumbnailPath);

        if (requestAutoBackup)
        {
            _autoBackupRequester?.RequestAutoBackup();
        }

        return SaveCategoryResult.Success(category);
    }

    private SaveCategoryResult? TrySaveSlotOnly(
        SaveCategoryRequest request,
        bool requestAutoBackup)
    {
        if (request.CategoryId.HasValue || request.IsSlotEnabled == request.OriginalIsSlotEnabled)
        {
            return null;
        }

        var slotSaveResult = SaveSlotEnabled(request);
        if (!slotSaveResult.Succeeded)
        {
            return SaveCategoryResult.Failure(slotSaveResult.ErrorMessage!);
        }

        if (requestAutoBackup)
        {
            _autoBackupRequester?.RequestAutoBackup();
        }

        return SaveCategoryResult.SlotOnly();
    }

    private SlotSettingSaveResult SaveSlotEnabled(SaveCategoryRequest request)
    {
        if (_settingsStore is null || request.IsSlotEnabled == request.OriginalIsSlotEnabled)
        {
            return SlotSettingSaveResult.Success();
        }

        try
        {
            _settingsStore.SetCategorySlotEnabled(request.SlotKey, request.IsSlotEnabled);
            return SlotSettingSaveResult.Success();
        }
        catch
        {
            return SlotSettingSaveResult.Failure(SlotSettingSaveFailedMessage);
        }
    }
}

public sealed class DeleteCategoryUseCase
{
    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IImageFileManager? _imageFileManager;

    public DeleteCategoryUseCase(
        ICategoryRepository categoryRepository,
        IImageFileManager? imageFileManager = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _categoryRepository = categoryRepository;
        _imageFileManager = imageFileManager;
        _autoBackupRequester = autoBackupRequester;
    }

    public void Execute(Guid categoryId)
    {
        var deletedImageFiles = _categoryRepository.Delete(categoryId);
        DeleteImageFiles(deletedImageFiles);
        _autoBackupRequester?.RequestAutoBackup();
    }

    private void DeleteImageFiles(IEnumerable<ImageFileReference> imageFiles)
    {
        if (_imageFileManager is null)
        {
            return;
        }

        foreach (var imageFileSet in imageFiles)
        {
            _imageFileManager.DeleteImageFiles(imageFileSet);
        }
    }
}

public sealed class TransferCategoryUseCase
{
    private const string ImageStorageMissingMessage = "이미지 저장소가 준비되지 않았습니다.";

    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IImageFileManager? _imageFileManager;
    private readonly SaveCategoryUseCase _saveCategoryUseCase;
    private readonly ISettingsStore _settingsStore;

    public TransferCategoryUseCase(
        ICategoryRepository categoryRepository,
        ISettingsStore settingsStore,
        SaveCategoryUseCase saveCategoryUseCase,
        IImageFileManager? imageFileManager = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _categoryRepository = categoryRepository;
        _settingsStore = settingsStore;
        _saveCategoryUseCase = saveCategoryUseCase;
        _imageFileManager = imageFileManager;
        _autoBackupRequester = autoBackupRequester;
    }

    public TransferCategoryResult Execute(TransferCategoryRequest request)
    {
        if (!request.CategoryId.HasValue)
        {
            return TransferCategoryResult.Failure(
                "저장된 카테고리만 복사하거나 이동할 수 있습니다.");
        }

        var targetCategory = _categoryRepository.GetBySlotKey(request.TargetSlotKey);
        if (targetCategory is not null
            && targetCategory.Id != request.CategoryId.Value
            && !request.OverwriteConfirmed)
        {
            return TransferCategoryResult.RequiresConfirmation(targetCategory.Name);
        }

        var saveResult = _saveCategoryUseCase.Execute(request.SaveRequest, requestAutoBackup: false);
        if (!saveResult.Succeeded)
        {
            return TransferCategoryResult.Failure(saveResult.ErrorMessage!);
        }

        var transferredCategory = request.Operation == CategoryTransferOperation.Copy
            ? CopyCategory(request)
            : MoveCategory(request);

        _autoBackupRequester?.RequestAutoBackup();
        return TransferCategoryResult.Success(transferredCategory);
    }

    private Category CopyCategory(TransferCategoryRequest request)
    {
        var createdImageFiles = new List<ImageFileReference>();
        CategoryTransferRepositoryResult result;

        try
        {
            result = _categoryRepository.CopyToSlot(
                request.CategoryId!.Value,
                request.TargetSlotKey,
                imageFiles => CopyImageFiles(imageFiles, createdImageFiles));
        }
        catch
        {
            DeleteImageFiles(createdImageFiles);
            throw;
        }

        try
        {
            _settingsStore.SetCategorySlotEnabled(request.TargetSlotKey, request.SourceSlotEnabled);
            return result.Category;
        }
        finally
        {
            DeleteImageFiles(result.OverwrittenImageFiles);
        }
    }

    private Category MoveCategory(TransferCategoryRequest request)
    {
        var result = _categoryRepository.MoveToSlot(request.CategoryId!.Value, request.TargetSlotKey);

        try
        {
            _settingsStore.SetCategorySlotEnabled(request.TargetSlotKey, request.SourceSlotEnabled);
            _settingsStore.SetCategorySlotEnabled(request.SourceSlotKey, true);
            return result.Category;
        }
        finally
        {
            DeleteImageFiles(result.OverwrittenImageFiles);
        }
    }

    private ImageFileReference CopyImageFiles(
        ImageFileReference imageFiles,
        List<ImageFileReference> createdImageFiles)
    {
        if (string.IsNullOrWhiteSpace(imageFiles.ImagePath))
        {
            return new ImageFileReference(null, null);
        }

        if (_imageFileManager is null)
        {
            throw new InvalidOperationException(ImageStorageMissingMessage);
        }

        var storedImage = _imageFileManager.StoreImage(imageFiles.ImagePath);
        var copiedImageFiles = new ImageFileReference(storedImage.ImagePath, storedImage.ThumbnailPath);
        createdImageFiles.Add(copiedImageFiles);

        return copiedImageFiles;
    }

    private void DeleteImageFiles(IEnumerable<ImageFileReference> imageFiles)
    {
        if (_imageFileManager is null)
        {
            return;
        }

        foreach (var imageFileSet in imageFiles)
        {
            _imageFileManager.DeleteImageFiles(imageFileSet);
        }
    }
}

public sealed record SaveCategoryRequest(
    SlotKey SlotKey,
    Guid? CategoryId,
    string Name,
    string? Description,
    string? ImagePath,
    string? ThumbnailPath,
    bool IsSlotEnabled,
    bool OriginalIsSlotEnabled);

public sealed record SaveCategoryResult(
    bool Succeeded,
    Category? Category = null,
    string? ErrorMessage = null,
    bool SavedSlotOnly = false)
{
    public static SaveCategoryResult Success(Category category)
    {
        return new SaveCategoryResult(true, category);
    }

    public static SaveCategoryResult SlotOnly()
    {
        return new SaveCategoryResult(true, SavedSlotOnly: true);
    }

    public static SaveCategoryResult Failure(string errorMessage)
    {
        return new SaveCategoryResult(false, ErrorMessage: errorMessage);
    }
}

public enum CategoryTransferOperation
{
    Copy,
    Move
}

public sealed record TransferCategoryRequest(
    Guid? CategoryId,
    SlotKey SourceSlotKey,
    SlotKey TargetSlotKey,
    bool SourceSlotEnabled,
    CategoryTransferOperation Operation,
    SaveCategoryRequest SaveRequest,
    bool OverwriteConfirmed);

public sealed record TransferCategoryResult(
    bool Succeeded,
    Category? Category = null,
    string? ErrorMessage = null,
    bool NeedsOverwriteConfirmation = false,
    string? ExistingTargetName = null)
{
    public static TransferCategoryResult Success(Category category)
    {
        return new TransferCategoryResult(true, category);
    }

    public static TransferCategoryResult Failure(string errorMessage)
    {
        return new TransferCategoryResult(false, ErrorMessage: errorMessage);
    }

    public static TransferCategoryResult RequiresConfirmation(string existingTargetName)
    {
        return new TransferCategoryResult(
            false,
            NeedsOverwriteConfirmation: true,
            ExistingTargetName: existingTargetName);
    }
}

internal sealed record SlotSettingSaveResult(bool Succeeded, string? ErrorMessage = null)
{
    public static SlotSettingSaveResult Success()
    {
        return new SlotSettingSaveResult(true);
    }

    public static SlotSettingSaveResult Failure(string errorMessage)
    {
        return new SlotSettingSaveResult(false, errorMessage);
    }
}
