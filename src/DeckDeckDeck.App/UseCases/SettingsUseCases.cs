using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class SaveSettingsUseCase
{
    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly IBackupGateway? _backupGateway;
    private readonly ISettingsStore _settingsStore;

    public SaveSettingsUseCase(
        ISettingsStore settingsStore,
        IBackupGateway? backupGateway = null,
        IAutoBackupRequester? autoBackupRequester = null)
    {
        _settingsStore = settingsStore;
        _backupGateway = backupGateway;
        _autoBackupRequester = autoBackupRequester;
    }

    public SaveSettingsResult Execute(SaveSettingsRequest request)
    {
        var validationError = ValidateBackupSettings(
            request.BackupFolderPath,
            requireFolder: request.AutoBackupEnabled);
        if (validationError is not null)
        {
            return SaveSettingsResult.Failure(validationError);
        }

        var latestSettings = _settingsStore.Load();
        latestSettings.BringWindowToFrontOnHotkey = request.BringWindowToFrontOnHotkey;
        latestSettings.AutoHideAfterPaste = request.AutoHideAfterPaste;
        latestSettings.RestoreClipboardAfterPaste = request.RestoreClipboardAfterPaste;
        latestSettings.AutoBackupEnabled = request.AutoBackupEnabled;
        latestSettings.BackupFolderPath = request.BackupFolderPath.Trim();
        _settingsStore.Save(latestSettings);
        _autoBackupRequester?.RequestAutoBackup();

        return SaveSettingsResult.Success();
    }

    private string? ValidateBackupSettings(string backupFolderPath, bool requireFolder)
    {
        if (requireFolder && string.IsNullOrWhiteSpace(backupFolderPath))
        {
            return "백업 폴더를 선택해 주세요.";
        }

        if (string.IsNullOrWhiteSpace(backupFolderPath))
        {
            return null;
        }

        return _backupGateway?.ValidateBackupFolder(backupFolderPath);
    }
}

public sealed class CreateManualBackupUseCase
{
    private readonly IBackupGateway? _backupGateway;

    public CreateManualBackupUseCase(IBackupGateway? backupGateway)
    {
        _backupGateway = backupGateway;
    }

    public CreateManualBackupResult Execute(string backupFolderPath)
    {
        var validationError = ValidateBackupSettings(backupFolderPath);
        if (validationError is not null)
        {
            return CreateManualBackupResult.Failure(validationError);
        }

        if (_backupGateway is null)
        {
            return CreateManualBackupResult.Failure("백업 서비스가 준비되지 않았습니다.");
        }

        var result = _backupGateway.CreateManualBackup(backupFolderPath);
        return result.Succeeded
            ? CreateManualBackupResult.Success(result.BackupPath!)
            : CreateManualBackupResult.Failure(
                result.ErrorMessage ?? "백업을 만들지 못했습니다.");
    }

    private string? ValidateBackupSettings(string backupFolderPath)
    {
        if (string.IsNullOrWhiteSpace(backupFolderPath))
        {
            return "백업 폴더를 선택해 주세요.";
        }

        return _backupGateway?.ValidateBackupFolder(backupFolderPath);
    }
}

public sealed record SaveSettingsRequest(
    bool BringWindowToFrontOnHotkey,
    bool AutoHideAfterPaste,
    bool RestoreClipboardAfterPaste,
    bool AutoBackupEnabled,
    string BackupFolderPath);

public sealed record SaveSettingsResult(bool Succeeded, string? ErrorMessage = null)
{
    public static SaveSettingsResult Success()
    {
        return new SaveSettingsResult(true);
    }

    public static SaveSettingsResult Failure(string errorMessage)
    {
        return new SaveSettingsResult(false, errorMessage);
    }
}

public sealed record CreateManualBackupResult(
    bool Succeeded,
    string? BackupPath = null,
    string? ErrorMessage = null)
{
    public static CreateManualBackupResult Success(string backupPath)
    {
        return new CreateManualBackupResult(true, backupPath);
    }

    public static CreateManualBackupResult Failure(string errorMessage)
    {
        return new CreateManualBackupResult(false, ErrorMessage: errorMessage);
    }
}
