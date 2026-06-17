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
