using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public interface ISaveSettingsUseCase
{
    SaveSettingsResult Execute(SaveSettingsRequest request);
}

public interface ILoadSettingsUseCase
{
    AppSettings Execute();
}

public interface ICreateManualBackupUseCase
{
    CreateManualBackupResult Execute(string backupFolderPath);
}

public interface IRestoreBackupUseCase
{
    RestoreBackupUseCaseResult Execute(string backupZipPath);
}

public interface ISpotifyConnectionUseCase
{
    string DashboardUrl { get; }

    string RedirectUri { get; }

    SpotifyConnectionState GetState();

    string GetSavedClientId();

    bool OpenDashboard();

    Task<SpotifyConnectionUseCaseResult> ConnectAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    SpotifyConnectionState Disconnect();
}

public sealed class SaveSettingsUseCase : ISaveSettingsUseCase
{
    private readonly IAutoBackupRequester? _autoBackupRequester;
    private readonly IBackupGateway? _backupGateway;
    private readonly ISettingsRepository _settingsStore;

    public SaveSettingsUseCase(
        ISettingsRepository settingsStore,
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

public sealed class CreateManualBackupUseCase : ICreateManualBackupUseCase
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

public sealed class RestoreBackupUseCase : IRestoreBackupUseCase
{
    private readonly IBackupGateway? _backupGateway;

    public RestoreBackupUseCase(IBackupGateway? backupGateway)
    {
        _backupGateway = backupGateway;
    }

    public RestoreBackupUseCaseResult Execute(string backupZipPath)
    {
        if (string.IsNullOrWhiteSpace(backupZipPath))
        {
            return RestoreBackupUseCaseResult.Failure("복원할 백업 ZIP을 선택해 주세요.");
        }

        if (_backupGateway is null)
        {
            return RestoreBackupUseCaseResult.Failure("백업 서비스가 준비되지 않았습니다.");
        }

        var result = _backupGateway.RestoreBackup(backupZipPath);
        return result.Succeeded
            ? RestoreBackupUseCaseResult.Success(result.SafetyBackupPath!)
            : RestoreBackupUseCaseResult.Failure(
                result.ErrorMessage ?? "백업 ZIP을 복원하지 못했습니다.",
                result.SafetyBackupPath);
    }
}

public sealed class SpotifyConnectionUseCase : ISpotifyConnectionUseCase
{
    private readonly ISpotifyConnectionGateway _spotifyConnectionGateway;
    private readonly ISettingsRepository _settingsStore;
    private readonly IUrlLaunchGateway _urlLaunchGateway;

    public SpotifyConnectionUseCase(
        ISettingsRepository settingsStore,
        ISpotifyConnectionGateway spotifyConnectionGateway,
        IUrlLaunchGateway urlLaunchGateway)
    {
        _settingsStore = settingsStore;
        _spotifyConnectionGateway = spotifyConnectionGateway;
        _urlLaunchGateway = urlLaunchGateway;
    }

    public string DashboardUrl => _spotifyConnectionGateway.DashboardUrl;

    public string RedirectUri => _spotifyConnectionGateway.RedirectUri;

    public SpotifyConnectionState GetState()
    {
        return SpotifyConnectionState.FromSettings(_settingsStore.Load());
    }

    public string GetSavedClientId()
    {
        return _settingsStore.Load().SpotifyClientId;
    }

    public bool OpenDashboard()
    {
        return _urlLaunchGateway.TryLaunch(DashboardUrl);
    }

    public async Task<SpotifyConnectionUseCaseResult> ConnectAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return SpotifyConnectionUseCaseResult.Failure("Spotify Client ID를 입력해 주세요.");
        }

        var trimmedClientId = clientId.Trim();
        var result = await _spotifyConnectionGateway.ConnectAsync(trimmedClientId, cancellationToken);
        if (!result.Succeeded)
        {
            return SpotifyConnectionUseCaseResult.Failure(
                result.ErrorMessage ?? "Spotify 연결에 실패했습니다.");
        }

        if (string.IsNullOrWhiteSpace(result.AccessToken)
            || string.IsNullOrWhiteSpace(result.RefreshToken)
            || result.ExpiresAt is null)
        {
            return SpotifyConnectionUseCaseResult.Failure("Spotify 토큰을 받지 못했습니다.");
        }

        var settings = _settingsStore.Load();
        settings.SpotifyClientId = trimmedClientId;
        settings.SpotifyAccessToken = result.AccessToken;
        settings.SpotifyRefreshToken = result.RefreshToken;
        settings.SpotifyTokenExpiresAt = result.ExpiresAt;
        settings.SpotifyConnectedUserDisplayName = string.IsNullOrWhiteSpace(result.DisplayName)
            ? "Spotify 계정"
            : result.DisplayName;
        _settingsStore.Save(settings);

        return SpotifyConnectionUseCaseResult.Success(GetState());
    }

    public SpotifyConnectionState Disconnect()
    {
        var settings = _settingsStore.Load();
        settings.SpotifyClientId = string.Empty;
        settings.SpotifyAccessToken = string.Empty;
        settings.SpotifyRefreshToken = string.Empty;
        settings.SpotifyTokenExpiresAt = null;
        settings.SpotifyConnectedUserDisplayName = string.Empty;
        _settingsStore.Save(settings);

        return GetState();
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

public sealed record RestoreBackupUseCaseResult(
    bool Succeeded,
    string? SafetyBackupPath = null,
    string? ErrorMessage = null)
{
    public static RestoreBackupUseCaseResult Success(string safetyBackupPath)
    {
        return new RestoreBackupUseCaseResult(true, safetyBackupPath);
    }

    public static RestoreBackupUseCaseResult Failure(string errorMessage, string? safetyBackupPath = null)
    {
        return new RestoreBackupUseCaseResult(false, safetyBackupPath, errorMessage);
    }
}

public sealed record SpotifyConnectionUseCaseResult(
    bool Succeeded,
    SpotifyConnectionState? State = null,
    string? ErrorMessage = null)
{
    public static SpotifyConnectionUseCaseResult Success(SpotifyConnectionState state)
    {
        return new SpotifyConnectionUseCaseResult(true, state);
    }

    public static SpotifyConnectionUseCaseResult Failure(string errorMessage)
    {
        return new SpotifyConnectionUseCaseResult(false, ErrorMessage: errorMessage);
    }
}

