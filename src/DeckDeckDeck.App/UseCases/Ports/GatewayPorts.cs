using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases.Ports;

public interface IBackupGateway
{
    string? ValidateBackupFolder(string? backupFolderPath);

    BackupGatewayResult CreateManualBackup(string backupFolderPath);

    RestoreBackupGatewayResult RestoreBackup(string backupZipPath);
}

public interface IDialogAdapter
{
    bool Confirm(string title, string message);

    void ShowInformation(string title, string message);

    string? SelectImageFile();

    string? SelectLaunchFile();

    string? SelectPasteFile();

    string? SelectLaunchFolder();

    string? SelectBackupFolder();

    string? SelectBackupZipFile();
}

public interface IAppLogger
{
    void Log(string message, Exception? exception = null);
}

public interface IClipboardTextWriter
{
    void SetText(string text);
}

public interface IClipboardPasteGateway
{
    Task<bool> PasteActionAsync(ExecutableAction action, IntPtr targetWindowHandle, AppSettings settings);
}

public interface IFilePasteGateway
{
    Task<FilePasteGatewayResult> PasteFileAsync(
        string filePath,
        IntPtr targetWindowHandle,
        AppSettings settings);
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
        bool runAsAdministrator,
        bool openTerminalWindow = false,
        string? workingDirectory = null);
}

public interface ISpotifyConnectionGateway
{
    string DashboardUrl { get; }

    string RedirectUri { get; }

    Task<SpotifyConnectionGatewayResult> ConnectAsync(
        string clientId,
        CancellationToken cancellationToken = default);
}
