using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class StartupMaintenanceUseCaseTests
{
    [Fact]
    public void ExecuteRunsMigrationWhenVersionIsMissing()
    {
        var state = new RecordingStartupMaintenanceStateRepository();
        var migration = new RecordingStoredPathMigrationGateway();
        var useCase = new StartupMaintenanceUseCase(state, migration);

        useCase.Execute();

        Assert.Equal(1, migration.NormalizeManagedPathsCount);
        Assert.Equal(
            StartupMaintenanceUseCase.CurrentStoredPathMigrationVersion,
            state.GetCompletedVersion(StartupMaintenanceUseCase.StoredPathMigrationMaintenanceKey));
    }

    [Fact]
    public void ExecuteSkipsMigrationWhenVersionIsCurrent()
    {
        var state = new RecordingStartupMaintenanceStateRepository();
        state.SetCompletedVersion(
            StartupMaintenanceUseCase.StoredPathMigrationMaintenanceKey,
            StartupMaintenanceUseCase.CurrentStoredPathMigrationVersion);
        var migration = new RecordingStoredPathMigrationGateway();
        var useCase = new StartupMaintenanceUseCase(state, migration);

        useCase.Execute();

        Assert.Equal(0, migration.NormalizeManagedPathsCount);
    }

    [Fact]
    public void ExecuteDoesNotSaveVersionWhenMigrationFails()
    {
        var state = new RecordingStartupMaintenanceStateRepository();
        var migration = new RecordingStoredPathMigrationGateway
        {
            Exception = new InvalidOperationException("migration failed")
        };
        var useCase = new StartupMaintenanceUseCase(state, migration);

        Assert.Throws<InvalidOperationException>(() => useCase.Execute());
        Assert.Equal(0, state.GetCompletedVersion(StartupMaintenanceUseCase.StoredPathMigrationMaintenanceKey));
    }

    private sealed class RecordingStartupMaintenanceStateRepository : IStartupMaintenanceStateRepository
    {
        private readonly Dictionary<string, int> _versions = [];

        public int GetCompletedVersion(string maintenanceKey)
        {
            return _versions.GetValueOrDefault(maintenanceKey);
        }

        public void SetCompletedVersion(string maintenanceKey, int version)
        {
            _versions[maintenanceKey] = version;
        }
    }

    private sealed class RecordingStoredPathMigrationGateway : IStoredPathMigrationGateway
    {
        public Exception? Exception { get; set; }

        public int NormalizeManagedPathsCount { get; private set; }

        public void NormalizeManagedPaths()
        {
            NormalizeManagedPathsCount++;
            if (Exception is not null)
            {
                throw Exception;
            }
        }
    }
}
