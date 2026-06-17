using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public interface IDirectHotkeyPassthroughPolicy
{
    bool ShouldPassThrough(HotkeyGesture gesture);
}

public sealed class DirectHotkeyPassthroughPolicy : IDirectHotkeyPassthroughPolicy
{
    private readonly ITextInputFocusDetector _textInputFocusDetector;

    public DirectHotkeyPassthroughPolicy(ITextInputFocusDetector textInputFocusDetector)
    {
        _textInputFocusDetector = textInputFocusDetector;
    }

    public bool ShouldPassThrough(HotkeyGesture gesture)
    {
        return IsUnmodifiedArrowKey(gesture) && _textInputFocusDetector.IsTextInputFocused();
    }

    internal static bool IsUnmodifiedArrowKey(HotkeyGesture gesture)
    {
        return gesture.Modifiers == HotkeyModifiers.None
            && gesture.VirtualKey is Win32Constants.VkLeft
                or Win32Constants.VkUp
                or Win32Constants.VkRight
                or Win32Constants.VkDown;
    }
}

internal sealed class NoopDirectHotkeyPassthroughPolicy : IDirectHotkeyPassthroughPolicy
{
    public static NoopDirectHotkeyPassthroughPolicy Instance { get; } = new();

    private NoopDirectHotkeyPassthroughPolicy()
    {
    }

    public bool ShouldPassThrough(HotkeyGesture gesture)
    {
        return false;
    }
}
