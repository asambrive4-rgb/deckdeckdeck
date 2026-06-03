namespace DeckDeckDeck.App.Services;

public interface IWindowFocusService
{
    IntPtr GetForegroundWindow();

    bool TryActivate(IntPtr windowHandle);
}
