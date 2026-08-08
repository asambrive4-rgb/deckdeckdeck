namespace DeckDeckDeck.App.UseCases.Ports;

public interface IToastNotificationPort
{
    void ShowToast(string title, string message);
    void PlayCompletionSound();
}
