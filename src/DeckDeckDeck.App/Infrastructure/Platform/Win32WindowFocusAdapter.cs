using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class Win32WindowFocusAdapter : IWin32WindowFocusAdapter
{
    public IntPtr GetForegroundWindow()
    {
        return User32.GetForegroundWindow();
    }

    public bool TryActivate(IntPtr windowHandle)
    {
        return windowHandle != IntPtr.Zero && User32.SetForegroundWindow(windowHandle);
    }
}
