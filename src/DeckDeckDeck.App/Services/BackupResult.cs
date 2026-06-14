namespace DeckDeckDeck.App.Services;

public sealed record BackupResult(
    bool Succeeded,
    bool Skipped,
    string? BackupPath,
    string? ErrorMessage)
{
    public static BackupResult Success(string backupPath)
    {
        return new BackupResult(true, false, backupPath, null);
    }

    public static BackupResult Failure(string errorMessage)
    {
        return new BackupResult(false, false, null, errorMessage);
    }

    public static BackupResult Skip()
    {
        return new BackupResult(false, true, null, null);
    }
}

public sealed record RestoreBackupResult(
    bool Succeeded,
    string? SafetyBackupPath,
    string? ErrorMessage)
{
    public static RestoreBackupResult Success(string safetyBackupPath)
    {
        return new RestoreBackupResult(true, safetyBackupPath, null);
    }

    public static RestoreBackupResult Failure(string errorMessage, string? safetyBackupPath = null)
    {
        return new RestoreBackupResult(false, safetyBackupPath, errorMessage);
    }
}
