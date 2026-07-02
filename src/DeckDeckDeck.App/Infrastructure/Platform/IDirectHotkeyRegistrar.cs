using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public interface IDirectHotkeyRegistrar : IDisposable
{
    event EventHandler<DirectHotkeyPressedEventArgs>? DirectHotkeyPressed;

    bool IsSuspended { get; set; }

    IReadOnlyList<string> Start();

    void Refresh(IReadOnlyList<DirectHotkeyRegistration> registrations);
}
