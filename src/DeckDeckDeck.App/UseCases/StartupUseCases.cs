using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class AppStartupUseCase
{
    private readonly IAppInstanceCoordinator _instanceCoordinator;

    public AppStartupUseCase(IAppInstanceCoordinator instanceCoordinator)
    {
        _instanceCoordinator = instanceCoordinator;
    }

    public async Task<AppStartupDecision> ExecuteAsync(
        AppStartupRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_instanceCoordinator.TryBecomePrimary())
            {
                return AppStartupDecision.RunPrimary();
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(request.ForwardTimeout);

            return await _instanceCoordinator.RequestShowPrimaryAsync(timeout.Token).ConfigureAwait(false)
                ? AppStartupDecision.ForwardedToPrimaryAndExit()
                : AppStartupDecision.FailedButExit(
                    "DeckDeckDeck이 이미 실행 중이지만 기존 창을 여는 요청에 실패했습니다.");
        }
        catch (Exception ex)
        {
            return AppStartupDecision.FailedButExit(
                "DeckDeckDeck 시작 상태를 확인하지 못했습니다.",
                ex.Message);
        }
    }
}

public sealed class StartupMaintenanceUseCase
{
    public const string StoredPathMigrationMaintenanceKey = "storedPathMigration";
    public const int CurrentStoredPathMigrationVersion = 1;

    private readonly IStartupMaintenanceStateRepository _stateRepository;
    private readonly IStoredPathMigrationGateway _storedPathMigrationGateway;

    public StartupMaintenanceUseCase(
        IStartupMaintenanceStateRepository stateRepository,
        IStoredPathMigrationGateway storedPathMigrationGateway)
    {
        _stateRepository = stateRepository;
        _storedPathMigrationGateway = storedPathMigrationGateway;
    }

    public void Execute()
    {
        if (_stateRepository.GetCompletedVersion(StoredPathMigrationMaintenanceKey)
            >= CurrentStoredPathMigrationVersion)
        {
            return;
        }

        _storedPathMigrationGateway.NormalizeManagedPaths();
        _stateRepository.SetCompletedVersion(
            StoredPathMigrationMaintenanceKey,
            CurrentStoredPathMigrationVersion);
    }
}

public sealed record AppStartupRequest(TimeSpan ForwardTimeout)
{
    public static AppStartupRequest Default { get; } = new(TimeSpan.FromMilliseconds(800));
}

public sealed record AppStartupDecision(
    AppStartupDecisionKind Kind,
    string? Message = null,
    string? Detail = null)
{
    public static AppStartupDecision RunPrimary()
    {
        return new AppStartupDecision(AppStartupDecisionKind.RunPrimary);
    }

    public static AppStartupDecision ForwardedToPrimaryAndExit()
    {
        return new AppStartupDecision(AppStartupDecisionKind.ForwardedToPrimaryAndExit);
    }

    public static AppStartupDecision FailedButExit(string message, string? detail = null)
    {
        return new AppStartupDecision(AppStartupDecisionKind.FailedButExit, message, detail);
    }
}

public enum AppStartupDecisionKind
{
    RunPrimary,
    ForwardedToPrimaryAndExit,
    FailedButExit
}
