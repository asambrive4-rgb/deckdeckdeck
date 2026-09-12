// 역할: 설정 변경 시 자동 백업을 요청하고 실행을 조율하는 인터페이스를 정의합니다.
namespace DeckDeckDeck.App.UseCases.Ports;

public interface IAutoBackupRequester
{
    void RequestAutoBackup();
}

public interface IAutoBackupCoordinator : IAutoBackupRequester
{
}
