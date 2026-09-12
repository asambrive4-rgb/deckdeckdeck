// 역할: 화면 모서리에 토스트 알림 메시지를 표시하기 위한 출력 규격을 정의합니다.
namespace DeckDeckDeck.App.UseCases.Ports;

public interface IToastNotificationPort
{
    void ShowToast(string title, string message);
    void PlayCompletionSound();
}
