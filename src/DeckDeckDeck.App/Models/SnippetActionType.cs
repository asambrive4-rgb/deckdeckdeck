// 역할: 슬롯을 눌렀을 때 실행할 동작 종류(텍스트 붙여넣기, URL 열기, 파일 실행 등)를 구분하는 목록입니다.
namespace DeckDeckDeck.App.Models;

public enum SnippetActionType
{
    PasteText,
    LaunchFile,
    LaunchUrl,
    MediaAction,
    TerminalCommand,
    Timer
}
