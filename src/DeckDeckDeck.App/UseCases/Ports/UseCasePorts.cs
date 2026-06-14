using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases.Ports;

public interface ICategoryRepository
{
    IReadOnlyList<Category> GetAll();

    Category? GetById(Guid id);

    Category? GetBySlotKey(SlotKey slotKey);

    Category Create(
        SlotKey slotKey,
        string name,
        string? description,
        string? imagePath = null,
        string? thumbnailPath = null);

    Category Update(
        Guid id,
        string name,
        string? description,
        string? imagePath,
        string? thumbnailPath);

    IReadOnlyList<ImageFileReference> Delete(Guid id);

    CategoryTransferRepositoryResult CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<ImageFileReference, ImageFileReference> copyImageFiles);

    CategoryTransferRepositoryResult MoveToSlot(Guid sourceId, SlotKey targetSlotKey);
}

public interface ISnippetRepository
{
    IReadOnlyList<Snippet> GetByCategoryId(Guid categoryId);

    Snippet? GetById(Guid id);

    Snippet Create(
        Guid categoryId,
        SlotKey slotKey,
        string title,
        string content,
        string? description,
        string? imagePath = null,
        string? thumbnailPath = null,
        SnippetActionType actionType = SnippetActionType.PasteText,
        string? launchPath = null,
        SlotImageMode slotImageMode = SlotImageMode.Auto,
        AutoIconCacheEntry? autoIcon = null,
        string? launchUrl = null,
        SnippetMediaProvider? mediaProvider = null,
        SnippetMediaCommand? mediaCommand = null);

    Snippet Update(
        Guid id,
        string title,
        string content,
        string? description,
        string? imagePath,
        string? thumbnailPath,
        SnippetActionType actionType = SnippetActionType.PasteText,
        string? launchPath = null,
        SlotImageMode slotImageMode = SlotImageMode.Auto,
        AutoIconCacheEntry? autoIcon = null,
        string? launchUrl = null,
        SnippetMediaProvider? mediaProvider = null,
        SnippetMediaCommand? mediaCommand = null);

    ImageFileReference Delete(Guid id);

    SnippetTransferRepositoryResult CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<ImageFileReference, ImageFileReference> copyImageFiles);

    SnippetTransferRepositoryResult MoveToSlot(Guid sourceId, SlotKey targetSlotKey);
}

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);

    void SetCategorySlotEnabled(SlotKey slotKey, bool enabled);

    void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled);
}

public interface IImageFileManager
{
    StoredImageReference StoreImage(string sourcePath);

    void DeleteImageFiles(ImageFileReference imageFiles);
}

public interface IBackupGateway
{
    string? ValidateBackupFolder(string? backupFolderPath);

    BackupGatewayResult CreateManualBackup(string backupFolderPath);

    RestoreBackupGatewayResult RestoreBackup(string backupZipPath);
}

public interface IAutoBackupRequester
{
    void RequestAutoBackup();
}

public interface IClipboardPasteGateway
{
    Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings);
}

public interface IFileLaunchGateway
{
    bool TryLaunch(string path);
}

public interface IUrlLaunchGateway
{
    bool TryLaunch(string url);
}

public interface IMediaActionGateway
{
    bool TryExecute(SnippetMediaCommand command);
}

public interface ISpotifyMediaActionGateway
{
    Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(
        SnippetMediaCommand command,
        CancellationToken cancellationToken = default);
}

public interface ISpotifyConnectionGateway
{
    string DashboardUrl { get; }

    string RedirectUri { get; }

    Task<SpotifyConnectionGatewayResult> ConnectAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    void Disconnect();
}

public sealed record ImageFileReference(string? ImagePath, string? ThumbnailPath);

public sealed record StoredImageReference(string ImagePath, string ThumbnailPath);

public sealed record CategoryTransferRepositoryResult(
    Category Category,
    IReadOnlyList<ImageFileReference> OverwrittenImageFiles);

public sealed record SnippetTransferRepositoryResult(
    Snippet Snippet,
    IReadOnlyList<ImageFileReference> OverwrittenImageFiles);

public sealed record BackupGatewayResult(
    bool Succeeded,
    string? BackupPath = null,
    string? ErrorMessage = null);

public sealed record RestoreBackupGatewayResult(
    bool Succeeded,
    string? SafetyBackupPath = null,
    string? ErrorMessage = null);

public sealed record SpotifyMediaActionGatewayResult(
    bool Succeeded,
    string? ErrorMessage = null);

public sealed record SpotifyConnectionGatewayResult(
    bool Succeeded,
    string? ErrorMessage = null);
