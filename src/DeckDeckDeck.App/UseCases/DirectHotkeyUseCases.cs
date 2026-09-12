// 역할: 현재 입력된 단축키가 프로그램 슬롯을 실행할 단축키인지 일반 프로그램으로 넘겨줄 키인지 판별합니다.
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class ShouldPassThroughDirectHotkeyUseCase
{
    private readonly ITextInputFocusDetector _textInputFocusDetector;

    public ShouldPassThroughDirectHotkeyUseCase(ITextInputFocusDetector textInputFocusDetector)
    {
        _textInputFocusDetector = textInputFocusDetector;
    }

    public bool Execute(HotkeyGesture gesture)
    {
        if (!gesture.IsUnmodifiedArrowKey)
        {
            return false;
        }

        try
        {
            return _textInputFocusDetector.IsTextInputFocused();
        }
        catch
        {
            return false;
        }
    }
}
