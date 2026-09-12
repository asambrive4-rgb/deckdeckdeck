// 역할: 타이머 시작 및 종료 시 시스템 알림이나 동작을 실행하는 연동 규격을 정의합니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases.Ports;

public interface ITimerActionGateway
{
    void ExecuteTimerAction(SlotKey slotKey);
}
