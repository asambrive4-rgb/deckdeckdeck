using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public interface IMediaActionService
{
    bool TryExecute(SnippetMediaCommand command);
}
