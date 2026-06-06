namespace DeckDeckDeck.App.Services;

internal sealed class PasteSelectionSession
{
    private int _currentSessionId;

    public void Start()
    {
        _currentSessionId++;
    }

    public Action CreateCompletion(Action completeSelection)
    {
        var sessionId = _currentSessionId;

        return () =>
        {
            if (sessionId == _currentSessionId)
            {
                completeSelection();
            }
        };
    }
}
