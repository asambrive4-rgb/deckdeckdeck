namespace DeckDeckDeck.App.UseCases.Ports;

public interface IAutoBackupRequester
{
    void RequestAutoBackup();
}

public interface IAutoBackupCoordinator : IAutoBackupRequester
{
}
