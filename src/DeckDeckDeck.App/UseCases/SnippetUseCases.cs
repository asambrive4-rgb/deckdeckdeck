using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class SaveSnippetUseCase
{
    private const string SlotSettingSaveFailedMessage = "슬롯 설정을 저장하지 못했습니다.";

    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly ISettingsStore? _settingsStore;
    private readonly ISnippetRepository _snippetRepository;

    public SaveSnippetUseCase(
        ISnippetRepository snippetRepository,
        ISettingsStore? settingsStore = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _snippetRepository = snippetRepository;
        _settingsStore = settingsStore;
        _autoBackupRequester = autoBackupRequester;
    }

    public SaveSnippetResult Execute(SaveSnippetRequest request)
    {
        return Execute(request, requestAutoBackup: true);
    }

    internal SaveSnippetResult Execute(
        SaveSnippetRequest request,
        bool requestAutoBackup)
    {
        var validation = SnippetRules.ValidateForSave(
            request.Title,
            request.Content,
            request.ActionType,
            request.LaunchPath,
            request.LaunchUrl,
            request.SelectedMediaProvider,
            request.SelectedMediaCommand);

        if (!validation.Succeeded)
        {
            if (ShouldTrySaveSlotOnly(request, validation.ErrorMessage)
                && TrySaveSlotOnly(request, requestAutoBackup) is { } slotOnlyResult)
            {
                return slotOnlyResult;
            }

            return SaveSnippetResult.Failure(validation.ErrorMessage!);
        }

        var slotSaveResult = SaveSlotEnabled(request);
        if (!slotSaveResult.Succeeded)
        {
            return SaveSnippetResult.Failure(slotSaveResult.ErrorMessage!);
        }

        var snippet = request.SnippetId.HasValue
            ? _snippetRepository.Update(
                request.SnippetId.Value,
                request.Title,
                request.Content,
                request.Description,
                request.ImagePath,
                request.ThumbnailPath,
                request.ActionType,
                request.LaunchPath,
                request.SlotImageMode,
                request.AutoIcon,
                validation.NormalizedLaunchUrl,
                validation.MediaProvider,
                validation.MediaCommand)
            : _snippetRepository.Create(
                request.CategoryId,
                request.SlotKey,
                request.Title,
                request.Content,
                request.Description,
                request.ImagePath,
                request.ThumbnailPath,
                request.ActionType,
                request.LaunchPath,
                request.SlotImageMode,
                request.AutoIcon,
                validation.NormalizedLaunchUrl,
                validation.MediaProvider,
                validation.MediaCommand);

        if (requestAutoBackup)
        {
            _autoBackupRequester?.RequestAutoBackup();
        }

        return SaveSnippetResult.Success(snippet, validation.NormalizedLaunchUrl);
    }

    private static bool ShouldTrySaveSlotOnly(
        SaveSnippetRequest request,
        string? validationError)
    {
        return !request.SnippetId.HasValue
            && request.IsSlotEnabled != request.OriginalIsSlotEnabled
            && validationError is SnippetRules.TitleRequiredMessage
                or SnippetRules.PasteContentRequiredMessage;
    }

    private SaveSnippetResult? TrySaveSlotOnly(
        SaveSnippetRequest request,
        bool requestAutoBackup)
    {
        if (request.SnippetId.HasValue || request.IsSlotEnabled == request.OriginalIsSlotEnabled)
        {
            return null;
        }

        var slotSaveResult = SaveSlotEnabled(request);
        if (!slotSaveResult.Succeeded)
        {
            return SaveSnippetResult.Failure(slotSaveResult.ErrorMessage!);
        }

        if (requestAutoBackup)
        {
            _autoBackupRequester?.RequestAutoBackup();
        }

        return SaveSnippetResult.SlotOnly();
    }

    private SlotSettingSaveResult SaveSlotEnabled(SaveSnippetRequest request)
    {
        if (_settingsStore is null || request.IsSlotEnabled == request.OriginalIsSlotEnabled)
        {
            return SlotSettingSaveResult.Success();
        }

        try
        {
            _settingsStore.SetSnippetSlotEnabled(request.SlotKey, request.IsSlotEnabled);
            return SlotSettingSaveResult.Success();
        }
        catch
        {
            return SlotSettingSaveResult.Failure(SlotSettingSaveFailedMessage);
        }
    }
}

public sealed class DeleteSnippetUseCase
{
    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly IImageFileManager? _imageFileManager;
    private readonly ISnippetRepository _snippetRepository;

    public DeleteSnippetUseCase(
        ISnippetRepository snippetRepository,
        IImageFileManager? imageFileManager = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _snippetRepository = snippetRepository;
        _imageFileManager = imageFileManager;
        _autoBackupRequester = autoBackupRequester;
    }

    public void Execute(Guid snippetId)
    {
        var deletedImageFiles = _snippetRepository.Delete(snippetId);
        _imageFileManager?.DeleteImageFiles(deletedImageFiles);
        _autoBackupRequester?.RequestAutoBackup();
    }
}

public sealed class TransferSnippetUseCase
{
    private const string ImageStorageMissingMessage = "이미지 저장소가 준비되지 않았습니다.";

    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly IImageFileManager? _imageFileManager;
    private readonly SaveSnippetUseCase _saveSnippetUseCase;
    private readonly ISettingsStore _settingsStore;
    private readonly ISnippetRepository _snippetRepository;

    public TransferSnippetUseCase(
        ISnippetRepository snippetRepository,
        ISettingsStore settingsStore,
        SaveSnippetUseCase saveSnippetUseCase,
        IImageFileManager? imageFileManager = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _snippetRepository = snippetRepository;
        _settingsStore = settingsStore;
        _saveSnippetUseCase = saveSnippetUseCase;
        _imageFileManager = imageFileManager;
        _autoBackupRequester = autoBackupRequester;
    }

    public TransferSnippetResult Execute(TransferSnippetRequest request)
    {
        if (!request.SnippetId.HasValue)
        {
            return TransferSnippetResult.Failure(
                "저장된 실행 항목만 복사하거나 이동할 수 있습니다.");
        }

        var targetSnippet = _snippetRepository
            .GetByCategoryId(request.CategoryId)
            .FirstOrDefault(snippet => snippet.SlotKey == request.TargetSlotKey);
        if (targetSnippet is not null
            && targetSnippet.Id != request.SnippetId.Value
            && !request.OverwriteConfirmed)
        {
            return TransferSnippetResult.RequiresConfirmation(targetSnippet.Title);
        }

        var saveResult = _saveSnippetUseCase.Execute(request.SaveRequest, requestAutoBackup: false);
        if (!saveResult.Succeeded)
        {
            return TransferSnippetResult.Failure(saveResult.ErrorMessage!);
        }

        var transferredSnippet = request.Operation == SnippetTransferOperation.Copy
            ? CopySnippet(request)
            : MoveSnippet(request);

        _autoBackupRequester?.RequestAutoBackup();
        return TransferSnippetResult.Success(transferredSnippet, saveResult.NormalizedLaunchUrl);
    }

    private Snippet CopySnippet(TransferSnippetRequest request)
    {
        var createdImageFiles = new List<ImageFileReference>();
        SnippetTransferRepositoryResult result;

        try
        {
            result = _snippetRepository.CopyToSlot(
                request.SnippetId!.Value,
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
            _settingsStore.SetSnippetSlotEnabled(request.TargetSlotKey, request.SourceSlotEnabled);
            return result.Snippet;
        }
        finally
        {
            DeleteImageFiles(result.OverwrittenImageFiles);
        }
    }

    private Snippet MoveSnippet(TransferSnippetRequest request)
    {
        var result = _snippetRepository.MoveToSlot(request.SnippetId!.Value, request.TargetSlotKey);

        try
        {
            _settingsStore.SetSnippetSlotEnabled(request.TargetSlotKey, request.SourceSlotEnabled);
            _settingsStore.SetSnippetSlotEnabled(request.SourceSlotKey, true);
            return result.Snippet;
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

public sealed record SaveSnippetRequest(
    Guid CategoryId,
    SlotKey SlotKey,
    Guid? SnippetId,
    string Title,
    string Content,
    string? Description,
    string? ImagePath,
    string? ThumbnailPath,
    SnippetActionType ActionType,
    string LaunchPath,
    SlotImageMode SlotImageMode,
    AutoIconCacheEntry? AutoIcon,
    string? LaunchUrl,
    SnippetMediaProvider SelectedMediaProvider,
    SnippetMediaCommand SelectedMediaCommand,
    bool IsSlotEnabled,
    bool OriginalIsSlotEnabled);

public sealed record SaveSnippetResult(
    bool Succeeded,
    Snippet? Snippet = null,
    string? ErrorMessage = null,
    bool SavedSlotOnly = false,
    string? NormalizedLaunchUrl = null)
{
    public static SaveSnippetResult Success(Snippet snippet, string? normalizedLaunchUrl)
    {
        return new SaveSnippetResult(true, snippet, NormalizedLaunchUrl: normalizedLaunchUrl);
    }

    public static SaveSnippetResult SlotOnly()
    {
        return new SaveSnippetResult(true, SavedSlotOnly: true);
    }

    public static SaveSnippetResult Failure(string errorMessage)
    {
        return new SaveSnippetResult(false, ErrorMessage: errorMessage);
    }
}

public enum SnippetTransferOperation
{
    Copy,
    Move
}

public sealed record TransferSnippetRequest(
    Guid CategoryId,
    Guid? SnippetId,
    SlotKey SourceSlotKey,
    SlotKey TargetSlotKey,
    bool SourceSlotEnabled,
    SnippetTransferOperation Operation,
    SaveSnippetRequest SaveRequest,
    bool OverwriteConfirmed);

public sealed record TransferSnippetResult(
    bool Succeeded,
    Snippet? Snippet = null,
    string? ErrorMessage = null,
    bool NeedsOverwriteConfirmation = false,
    string? ExistingTargetTitle = null,
    string? NormalizedLaunchUrl = null)
{
    public static TransferSnippetResult Success(Snippet snippet, string? normalizedLaunchUrl)
    {
        return new TransferSnippetResult(true, snippet, NormalizedLaunchUrl: normalizedLaunchUrl);
    }

    public static TransferSnippetResult Failure(string errorMessage)
    {
        return new TransferSnippetResult(false, ErrorMessage: errorMessage);
    }

    public static TransferSnippetResult RequiresConfirmation(string existingTargetTitle)
    {
        return new TransferSnippetResult(
            false,
            NeedsOverwriteConfirmation: true,
            ExistingTargetTitle: existingTargetTitle);
    }
}
