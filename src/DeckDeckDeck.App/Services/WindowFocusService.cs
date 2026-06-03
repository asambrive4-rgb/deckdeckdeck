using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class WindowFocusService : IWindowFocusService
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
