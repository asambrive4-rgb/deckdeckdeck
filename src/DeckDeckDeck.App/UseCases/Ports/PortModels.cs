using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases.Ports;

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
    bool RunAsAdministrator = true,
    FileActionMode FileActionMode = FileActionMode.Launch);

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
    bool RunAsAdministrator = true,
    FileActionMode FileActionMode = FileActionMode.Launch);

public enum FilePasteGatewayStatus
{
    Succeeded,
    FileNotFound,
    Failed
}

public sealed record FilePasteGatewayResult(
    FilePasteGatewayStatus Status,
    Exception? Exception = null)
{
    public static FilePasteGatewayResult Success()
    {
        return new FilePasteGatewayResult(FilePasteGatewayStatus.Succeeded);
    }

    public static FilePasteGatewayResult FileNotFound()
    {
        return new FilePasteGatewayResult(FilePasteGatewayStatus.FileNotFound);
    }

    public static FilePasteGatewayResult Failure(Exception? exception = null)
    {
        return new FilePasteGatewayResult(FilePasteGatewayStatus.Failed, exception);
    }
}

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
    string? ErrorMessage = null,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTimeOffset? ExpiresAt = null,
    string? DisplayName = null);
