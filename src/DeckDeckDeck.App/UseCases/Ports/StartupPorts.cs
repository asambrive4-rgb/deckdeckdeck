// 역할: 프로그램 단일 실행 보장, 시작 프로그램 등록, 데이터 마이그레이션 등 시작 단계에 필요한 외부 연동 인터페이스를 정의합니다.
namespace DeckDeckDeck.App.UseCases.Ports;

public interface IAppInstanceCoordinator : IDisposable
{
    bool TryBecomePrimary();

    Task<bool> RequestShowPrimaryAsync(CancellationToken cancellationToken);

    void StartListening(Action onShowRequested);
}

public interface IStartupMaintenanceStateRepository
{
    int GetCompletedVersion(string maintenanceKey);

    void SetCompletedVersion(string maintenanceKey, int version);
}

public interface IStoredPathMigrationGateway
{
    void NormalizeManagedPaths();
}

public interface IStartupRegistrationGateway
{
    StartupRegistrationState GetState();

    StartupRegistrationResult Save(StartupRegistrationSettings settings);
}

public sealed record StartupRegistrationSettings(
    bool IsEnabled,
    bool RunAsAdministrator);

public sealed record StartupRegistrationState(
    bool IsEnabled,
    bool RunAsAdministrator)
{
    public static StartupRegistrationState Disabled { get; } = new(false, false);
}

public sealed record StartupRegistrationResult(
    bool Succeeded,
    string? ErrorMessage = null)
{
    public static StartupRegistrationResult Success()
    {
        return new StartupRegistrationResult(true);
    }

    public static StartupRegistrationResult Failure(string errorMessage)
    {
        return new StartupRegistrationResult(false, errorMessage);
    }
}
