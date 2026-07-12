using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

/// <summary>
/// Moves a category between numpad slots from the home grid (drag-and-drop).
/// Occupied targets require explicit overwrite confirmation.
/// </summary>
public sealed class MoveCategorySlotUseCase
{
    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IImageFileRepository? _imageFileManager;
    private readonly ISettingsRepository _settingsStore;

    public MoveCategorySlotUseCase(
        ICategoryRepository categoryRepository,
        ISettingsRepository settingsStore,
        IImageFileRepository? imageFileManager = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _categoryRepository = categoryRepository;
        _settingsStore = settingsStore;
        _imageFileManager = imageFileManager;
        _autoBackupRequester = autoBackupRequester;
    }

    public MoveCategorySlotResult Execute(MoveCategorySlotRequest request)
    {
        if (request.SourceSlotKey == request.TargetSlotKey)
        {
            return MoveCategorySlotResult.NoOp();
        }

        var source = _categoryRepository.GetBySlotKey(request.SourceSlotKey);
        if (source is null)
        {
            return MoveCategorySlotResult.Failure("옮길 카테고리가 없습니다.");
        }

        var settings = _settingsStore.Load();
        if (!SlotRules.IsEnabled(request.SourceSlotKey, settings.EnabledCategorySlotKeys))
        {
            return MoveCategorySlotResult.Failure(
                $"슬롯 {request.SourceSlotKey.GetDisplayText()}은 사용 안 함 상태입니다.");
        }

        if (!SlotRules.IsEnabled(request.TargetSlotKey, settings.EnabledCategorySlotKeys))
        {
            return MoveCategorySlotResult.Failure(
                $"슬롯 {request.TargetSlotKey.GetDisplayText()}은 사용 안 함 상태입니다.");
        }

        var target = _categoryRepository.GetBySlotKey(request.TargetSlotKey);
        if (target is not null
            && target.Id != source.Id
            && !request.OverwriteConfirmed)
        {
            return MoveCategorySlotResult.RequiresConfirmation(target.Name);
        }

        var sourceEnabled = SlotRules.IsEnabled(
            request.SourceSlotKey,
            settings.EnabledCategorySlotKeys);

        var transferResult = _categoryRepository.MoveToSlot(source.Id, request.TargetSlotKey);

        try
        {
            _settingsStore.SetCategorySlotEnabled(request.TargetSlotKey, sourceEnabled);
            _settingsStore.SetCategorySlotEnabled(request.SourceSlotKey, true);
            _autoBackupRequester?.RequestAutoBackup();
            return MoveCategorySlotResult.Success(transferResult.Category);
        }
        finally
        {
            DeleteImageFiles(transferResult.OverwrittenImageFiles);
        }
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

/// <summary>
/// Moves a snippet between numpad slots inside a category (drag-and-drop).
/// Occupied targets require explicit overwrite confirmation.
/// </summary>
public sealed class MoveSnippetSlotUseCase
{
    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly IImageFileRepository? _imageFileManager;
    private readonly ISettingsRepository _settingsStore;
    private readonly ISnippetRepository _snippetRepository;

    public MoveSnippetSlotUseCase(
        ISnippetRepository snippetRepository,
        ISettingsRepository settingsStore,
        IImageFileRepository? imageFileManager = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _snippetRepository = snippetRepository;
        _settingsStore = settingsStore;
        _imageFileManager = imageFileManager;
        _autoBackupRequester = autoBackupRequester;
    }

    public MoveSnippetSlotResult Execute(MoveSnippetSlotRequest request)
    {
        if (request.SourceSlotKey == request.TargetSlotKey)
        {
            return MoveSnippetSlotResult.NoOp();
        }

        var snippets = _snippetRepository.GetByCategoryId(request.CategoryId);
        var source = snippets.FirstOrDefault(snippet => snippet.SlotKey == request.SourceSlotKey);
        if (source is null)
        {
            return MoveSnippetSlotResult.Failure("옮길 실행 항목이 없습니다.");
        }

        var settings = _settingsStore.Load();
        if (!SlotRules.IsEnabled(request.SourceSlotKey, settings.EnabledSnippetSlotKeys))
        {
            return MoveSnippetSlotResult.Failure(
                $"슬롯 {request.SourceSlotKey.GetDisplayText()}은 사용 안 함 상태입니다.");
        }

        if (!SlotRules.IsEnabled(request.TargetSlotKey, settings.EnabledSnippetSlotKeys))
        {
            return MoveSnippetSlotResult.Failure(
                $"슬롯 {request.TargetSlotKey.GetDisplayText()}은 사용 안 함 상태입니다.");
        }

        var target = snippets.FirstOrDefault(snippet => snippet.SlotKey == request.TargetSlotKey);
        if (target is not null
            && target.Id != source.Id
            && !request.OverwriteConfirmed)
        {
            return MoveSnippetSlotResult.RequiresConfirmation(target.Title);
        }

        var sourceEnabled = SlotRules.IsEnabled(
            request.SourceSlotKey,
            settings.EnabledSnippetSlotKeys);

        var transferResult = _snippetRepository.MoveToSlot(source.Id, request.TargetSlotKey);

        try
        {
            _settingsStore.SetSnippetSlotEnabled(request.TargetSlotKey, sourceEnabled);
            _settingsStore.SetSnippetSlotEnabled(request.SourceSlotKey, true);
            _autoBackupRequester?.RequestAutoBackup();
            return MoveSnippetSlotResult.Success(transferResult.Snippet);
        }
        finally
        {
            DeleteImageFiles(transferResult.OverwrittenImageFiles);
        }
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

public sealed record MoveCategorySlotRequest(
    SlotKey SourceSlotKey,
    SlotKey TargetSlotKey,
    bool OverwriteConfirmed);

public sealed record MoveCategorySlotResult(
    bool Succeeded,
    Category? Category = null,
    string? ErrorMessage = null,
    bool NeedsOverwriteConfirmation = false,
    string? ExistingTargetName = null,
    bool IsNoOp = false)
{
    public static MoveCategorySlotResult Success(Category category)
    {
        return new MoveCategorySlotResult(true, category);
    }

    public static MoveCategorySlotResult Failure(string errorMessage)
    {
        return new MoveCategorySlotResult(false, ErrorMessage: errorMessage);
    }

    public static MoveCategorySlotResult RequiresConfirmation(string existingTargetName)
    {
        return new MoveCategorySlotResult(
            false,
            NeedsOverwriteConfirmation: true,
            ExistingTargetName: existingTargetName);
    }

    public static MoveCategorySlotResult NoOp()
    {
        return new MoveCategorySlotResult(true, IsNoOp: true);
    }
}

public sealed record MoveSnippetSlotRequest(
    Guid CategoryId,
    SlotKey SourceSlotKey,
    SlotKey TargetSlotKey,
    bool OverwriteConfirmed);

public sealed record MoveSnippetSlotResult(
    bool Succeeded,
    Snippet? Snippet = null,
    string? ErrorMessage = null,
    bool NeedsOverwriteConfirmation = false,
    string? ExistingTargetName = null,
    bool IsNoOp = false)
{
    public static MoveSnippetSlotResult Success(Snippet snippet)
    {
        return new MoveSnippetSlotResult(true, snippet);
    }

    public static MoveSnippetSlotResult Failure(string errorMessage)
    {
        return new MoveSnippetSlotResult(false, ErrorMessage: errorMessage);
    }

    public static MoveSnippetSlotResult RequiresConfirmation(string existingTargetName)
    {
        return new MoveSnippetSlotResult(
            false,
            NeedsOverwriteConfirmation: true,
            ExistingTargetName: existingTargetName);
    }

    public static MoveSnippetSlotResult NoOp()
    {
        return new MoveSnippetSlotResult(true, IsNoOp: true);
    }
}
