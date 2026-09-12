// 역할: 현재 사용자의 커서가 텍스트 입력창에 위치해 있는지 판별하는 외부 감지 규격을 정의합니다.
namespace DeckDeckDeck.App.UseCases.Ports;

public interface ITextInputFocusDetector
{
    bool IsTextInputFocused();
}
