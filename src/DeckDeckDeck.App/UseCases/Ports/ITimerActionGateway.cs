using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.UseCases.Ports;

public interface ITimerActionGateway
{
    void ExecuteTimerAction(SlotKey slotKey);
}
