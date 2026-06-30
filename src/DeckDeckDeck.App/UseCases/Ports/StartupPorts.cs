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
