using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Tests;

public sealed class DirectHotkeyRegistrarTests
{
    [Fact]
    public void ActiveSingleKeyHotkeyBlocksInputAndRunsOnlyOnceUntilKeyUp()
    {
        var actionId = Guid.NewGuid();
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(_ => false);
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([new DirectHotkeyRegistration(actionId, new HotkeyGesture(0x67, HotkeyModifiers.None))]);

        var firstBlocked = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x67);
        var repeatBlocked = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x67);
        var keyUpBlocked = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeyup, 0x67);
        var secondPressBlocked = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x67);

        Assert.True(firstBlocked);
        Assert.True(repeatBlocked);
        Assert.True(keyUpBlocked);
        Assert.True(secondPressBlocked);
        Assert.Equal([actionId, actionId], pressed);
    }

    [Fact]
    public void InactiveHotkeyDoesNotBlockInput()
    {
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(_ => false);
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([]);

        var blocked = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x67);

        Assert.False(blocked);
        Assert.Empty(pressed);
    }

    [Fact]
    public void SuspendedHotkeyDoesNotBlockOrRun()
    {
        var actionId = Guid.NewGuid();
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(_ => false)
        {
            IsSuspended = true
        };
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([new DirectHotkeyRegistration(actionId, new HotkeyGesture(0x67, HotkeyModifiers.None))]);

        var blocked = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x67);

        Assert.False(blocked);
        Assert.Empty(pressed);
    }

    [Fact]
    public void ModifiedHotkeyRequiresExactCurrentModifiers()
    {
        var actionId = Guid.NewGuid();
        var pressedKeys = new HashSet<int> { Win32Constants.VkControl, Win32Constants.VkShift };
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(pressedKeys.Contains);
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([
            new DirectHotkeyRegistration(
                actionId,
                new HotkeyGesture(0x4B, HotkeyModifiers.Control | HotkeyModifiers.Shift))
        ]);

        var blockedWithModifiers = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x4B);
        pressedKeys.Remove(Win32Constants.VkShift);
        registrar.HandleKeyboardEventForTest(Win32Constants.WmKeyup, 0x4B);
        var blockedWithoutShift = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x4B);

        Assert.True(blockedWithModifiers);
        Assert.False(blockedWithoutShift);
        Assert.Equal([actionId], pressed);
    }

    [Fact]
    public void UnmodifiedArrowHotkeyPassesThroughWhenTextInputIsFocused()
    {
        var actionId = Guid.NewGuid();
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(
            _ => false,
            new DirectHotkeyPassthroughPolicy(new StubTextInputFocusDetector(true)));
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([
            new DirectHotkeyRegistration(
                actionId,
                new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None))
        ]);

        var keyDownBlocked = registrar.HandleKeyboardEventForTest(
            Win32Constants.WmKeydown,
            Win32Constants.VkRight);
        var keyUpBlocked = registrar.HandleKeyboardEventForTest(
            Win32Constants.WmKeyup,
            Win32Constants.VkRight);

        Assert.False(keyDownBlocked);
        Assert.False(keyUpBlocked);
        Assert.Empty(pressed);
    }

    [Fact]
    public void UnmodifiedArrowHotkeyRunsWhenTextInputIsNotFocused()
    {
        var actionId = Guid.NewGuid();
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(
            _ => false,
            new DirectHotkeyPassthroughPolicy(new StubTextInputFocusDetector(false)));
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([
            new DirectHotkeyRegistration(
                actionId,
                new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None))
        ]);

        var blocked = registrar.HandleKeyboardEventForTest(
            Win32Constants.WmKeydown,
            Win32Constants.VkRight);

        Assert.True(blocked);
        Assert.Equal([actionId], pressed);
    }

    [Fact]
    public void ModifiedArrowHotkeyStillRunsWhenTextInputIsFocused()
    {
        var actionId = Guid.NewGuid();
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(
            key => key == Win32Constants.VkControl,
            new DirectHotkeyPassthroughPolicy(new StubTextInputFocusDetector(true)));
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([
            new DirectHotkeyRegistration(
                actionId,
                new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.Control))
        ]);

        var blocked = registrar.HandleKeyboardEventForTest(
            Win32Constants.WmKeydown,
            Win32Constants.VkRight);

        Assert.True(blocked);
        Assert.Equal([actionId], pressed);
    }

    [Fact]
    public void NonArrowHotkeyStillRunsWhenTextInputIsFocused()
    {
        var actionId = Guid.NewGuid();
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(
            _ => false,
            new DirectHotkeyPassthroughPolicy(new StubTextInputFocusDetector(true)));
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([new DirectHotkeyRegistration(actionId, new HotkeyGesture(0x67, HotkeyModifiers.None))]);

        var blocked = registrar.HandleKeyboardEventForTest(Win32Constants.WmKeydown, 0x67);

        Assert.True(blocked);
        Assert.Equal([actionId], pressed);
    }

    [Fact]
    public void TextInputDetectionFailureKeepsExistingHotkeyBehavior()
    {
        var actionId = Guid.NewGuid();
        var pressed = new List<Guid>();
        var registrar = new DirectHotkeyRegistrar(
            _ => false,
            new DirectHotkeyPassthroughPolicy(new ThrowingTextInputFocusDetector()));
        registrar.DirectHotkeyPressed += (_, e) => pressed.Add(e.HotkeyActionId);
        registrar.Refresh([
            new DirectHotkeyRegistration(
                actionId,
                new HotkeyGesture(Win32Constants.VkRight, HotkeyModifiers.None))
        ]);

        var blocked = registrar.HandleKeyboardEventForTest(
            Win32Constants.WmKeydown,
            Win32Constants.VkRight);

        Assert.True(blocked);
        Assert.Equal([actionId], pressed);
    }

    private sealed class StubTextInputFocusDetector : ITextInputFocusDetector
    {
        private readonly bool _isTextInputFocused;

        public StubTextInputFocusDetector(bool isTextInputFocused)
        {
            _isTextInputFocused = isTextInputFocused;
        }

        public bool IsTextInputFocused()
        {
            return _isTextInputFocused;
        }
    }

    private sealed class ThrowingTextInputFocusDetector : ITextInputFocusDetector
    {
        public bool IsTextInputFocused()
        {
            throw new InvalidOperationException("focus detection failed");
        }
    }
}
