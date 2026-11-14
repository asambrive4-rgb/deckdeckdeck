// 역할: 유스케이스와 외부 연동 장치 사이에서 주고받는 이미지 참조, 저장 데이터 등의 공통 데이터 규격을 정의합니다.
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
    FileActionMode FileActionMode = FileActionMode.Launch,
    bool OpenTerminalWindow = false,
    string? TerminalWorkingDirectory = null,
    string? AdbDeviceIp = null,
    string? RemoteInstallProjectPath = null,
    string? RemoteInstallModule = null,
    RemoteInstallDeviceKind? RemoteInstallDeviceKind = null,
    string? RemoteInstallTargetDevice = null,
    bool RemoteInstallLaunchAfterInstall = true);

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
    FileActionMode FileActionMode = FileActionMode.Launch,
    bool OpenTerminalWindow = false,
    string? TerminalWorkingDirectory = null,
    string? AdbDeviceIp = null,
    string? RemoteInstallProjectPath = null,
    string? RemoteInstallModule = null,
    RemoteInstallDeviceKind? RemoteInstallDeviceKind = null,
    string? RemoteInstallTargetDevice = null,
    bool RemoteInstallLaunchAfterInstall = true);

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

public sealed record InstalledAppSelection(
    string DisplayName,
    string LaunchPath);

public sealed record BluetoothDeviceSelection(
    string DisplayName,
    string MacAddress);

public enum AdbPromptMode
{
    Connect,
    Pair
}

public sealed record AdbPromptResult(
    AdbPromptMode Mode,
    string Port,
    string? PairCode = null);

public sealed record AndroidAppModuleInfo(
    string ModuleName,
    string RelativeDirectory,
    string? ApplicationId);

public sealed record AndroidConnectedDeviceInfo(
    string Serial,
    string Model,
    string Manufacturer,
    RemoteInstallDeviceKind DeviceKind,
    int? SmallestWidthDp,
    string ClassificationSource,
    string? IpAddress)
{
    public string DisplaySummary =>
        SmallestWidthDp.HasValue
            ? $"{Model} [{RemoteInstallDeviceKindText(DeviceKind)} · {SmallestWidthDp}dp · {Serial}]"
            : $"{Model} [{RemoteInstallDeviceKindText(DeviceKind)} · {Serial}]";

    private static string RemoteInstallDeviceKindText(RemoteInstallDeviceKind kind) => kind switch
    {
        RemoteInstallDeviceKind.Tablet => "태블릿",
        RemoteInstallDeviceKind.Phone => "폰",
        RemoteInstallDeviceKind.Watch => "워치",
        _ => "폰"
    };
}

public sealed record AndroidRemoteInstallRequest(
    string Title,
    string ProjectPath,
    string ModuleName,
    RemoteInstallDeviceKind TargetDeviceKind,
    string? TargetDeviceFilter,
    bool LaunchAfterInstall);
