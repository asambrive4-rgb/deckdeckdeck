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

    Snippet Create(Guid categoryId, SlotKey slotKey, SnippetSaveData data);

    Snippet Update(Guid id, SnippetSaveData data);

    ImageFileReference Delete(Guid id);

    SnippetTransferRepositoryResult CopyToSlot(
        Guid sourceId,
        SlotKey targetSlotKey,
        Func<ImageFileReference, ImageFileReference> copyImageFiles);

    SnippetTransferRepositoryResult MoveToSlot(Guid sourceId, SlotKey targetSlotKey);
}

public interface IHotkeyActionRepository
{
    IReadOnlyList<HotkeyAction> GetAll();

    HotkeyAction? GetById(Guid id);

    HotkeyAction Create(HotkeyActionSaveData data);

    HotkeyAction Update(Guid id, HotkeyActionSaveData data);

    HotkeyAction SetEnabled(Guid id, bool isEnabled);

    ImageFileReference Delete(Guid id);
}

public interface ISettingsRepository
{
    AppSettings Load();

    void EnsureDefaults();

    void Save(AppSettings settings);

    void SaveWindowPlacement(double left, double top, string screenDeviceName);

    void SetCategorySlotEnabled(SlotKey slotKey, bool enabled);

    void SetSnippetSlotEnabled(SlotKey slotKey, bool enabled);
}

public interface IImageFileRepository
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

public interface IAutoBackupCoordinator : IAutoBackupRequester
{
}

public interface IDialogAdapter
{
    bool Confirm(string title, string message);

    void ShowInformation(string title, string message);

    string? SelectImageFile();

    string? SelectLaunchFile();

    string? SelectLaunchFolder();

    string? SelectBackupFolder();

    string? SelectBackupZipFile();
}

public interface IAppLogger
{
    void Log(string message, Exception? exception = null);
}

public interface IStoredImagePathResolver
{
    string? ResolveDisplayPath(string? storedPath);

    bool FileExists(string? storedPath);
}

public interface ISnippetImageResolver
{
    string? GetDisplayImagePath(Snippet? snippet);

    AutoIconCacheEntry? PrepareAutoIcon(
        SnippetActionType actionType,
        string? launchPath,
        AutoIconCacheEntry? current);
}

public interface IClipboardTextWriter
{
    void SetText(string text);
}

public interface IClipboardPasteGateway
{
    Task<bool> PasteActionAsync(ExecutableAction action, IntPtr targetWindowHandle, AppSettings settings);
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

public interface ITerminalCommandGateway
{
    bool TryExecute(
        string command,
        SnippetTerminalShell shell,
        bool runAsAdministrator);
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

public sealed record SnippetSaveData(
    string Title,
    string Content,
    string? Description,
    string? ImagePath,
    string? ThumbnailPath,
    SnippetActionType ActionType = SnippetActionType.PasteText,
    string? LaunchPath = null,
    SlotImageMode SlotImageMode = SlotImageMode.Auto,
    AutoIconCacheEntry? AutoIcon = null,
    string? LaunchUrl = null,
    SnippetMediaProvider? MediaProvider = null,
    SnippetMediaCommand? MediaCommand = null,
    PasteShortcutMode PasteShortcutMode = PasteShortcutMode.CtrlV,
    string? TerminalCommand = null,
    SnippetTerminalShell? TerminalShell = null,
    bool RunAsAdministrator = true);

public sealed record HotkeyActionSaveData(
    string Title,
    HotkeyGesture? Gesture,
    bool IsEnabled,
    string Content,
    string? Description,
    string? ImagePath,
    string? ThumbnailPath,
    SnippetActionType ActionType = SnippetActionType.PasteText,
    string? LaunchPath = null,
    SlotImageMode SlotImageMode = SlotImageMode.Auto,
    AutoIconCacheEntry? AutoIcon = null,
    string? LaunchUrl = null,
    SnippetMediaProvider? MediaProvider = null,
    SnippetMediaCommand? MediaCommand = null,
    PasteShortcutMode PasteShortcutMode = PasteShortcutMode.CtrlV,
    string? TerminalCommand = null,
    SnippetTerminalShell? TerminalShell = null,
    bool RunAsAdministrator = true);

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
