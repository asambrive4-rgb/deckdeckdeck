// 역할: 데이터 백업 및 복원 작업의 성공 여부와 결과 메시지를 담는 데이터 모델입니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
namespace DeckDeckDeck.App.Infrastructure.Gateways;

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
