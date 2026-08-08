using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Tests;

public sealed class NumpadCaptureTests
{
    [Fact]
    public void KeyboardHookCapturesNumpadEvenWhenGlobalRegistrationSucceeds()
    {
        var capturedSlots = new List<SlotKey>();
        using var capture = CreateCapture(
            registerHotKey: (_, _, _, _) => true,
            isKeyDown: _ => false);
        capture.SlotCaptured += (_, e) => capturedSlots.Add(e.SlotKey);

        capture.Start(IntPtr.Zero);
        var blocked = capture.HandleKeyboardEventForTest(
            Win32Constants.WmKeydown,
            Win32Constants.VkNumpad0 + 1);

        Assert.True(blocked);
        Assert.Equal([SlotKey.Numpad1], capturedSlots);
    }

    [Fact]
    public void RegistrationFailureFallsBackToKeyboardHook()
    {
        var capturedSlots = new List<SlotKey>();
        using var capture = CreateCapture(
            registerHotKey: (_, _, _, _) => false,
            isKeyDown: _ => false);
        capture.SlotCaptured += (_, e) => capturedSlots.Add(e.SlotKey);

        capture.Start(IntPtr.Zero);
        var blocked = capture.HandleKeyboardEventForTest(
            Win32Constants.WmKeydown,
            Win32Constants.VkNumpad0 + 1);

        Assert.True(capture.IsCapturing);
        Assert.True(blocked);
        Assert.Equal([SlotKey.Numpad1], capturedSlots);
    }

    [Fact]
    public void FallbackIgnoresNumpadWhenModifierIsPressed()
    {
        var capturedSlots = new List<SlotKey>();
        using var capture = CreateCapture(
            registerHotKey: (_, _, _, _) => false,
            isKeyDown: key => key == Win32Constants.VkControl);
        capture.SlotCaptured += (_, e) => capturedSlots.Add(e.SlotKey);

        capture.Start(IntPtr.Zero);
        var blocked = capture.HandleKeyboardEventForTest(
            Win32Constants.WmKeydown,
            Win32Constants.VkNumpad0 + 1);

        Assert.False(blocked);
        Assert.Empty(capturedSlots);
    }

    private static NumpadCapture CreateCapture(
        Func<IntPtr, int, uint, uint, bool> registerHotKey,
        Func<int, bool> isKeyDown)
    {
        return new NumpadCapture(
            registerHotKey,
            unregisterHotKey: (_, _) => true,
            installKeyboardHook: _ => new IntPtr(123),
            uninstallKeyboardHook: _ => true,
            isKeyDown);
    }
}
