using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class HotkeyMappingTests
{
    private const int AsyncEventTimeoutMilliseconds = 5000;

    [Fact]
    public void HotkeyServiceRegistersDirectCategorySymbolHotkeys()
    {
        var registeredSlotKeys = HotkeyService.GetRegisteredHotkeys()
            .Select(hotkey => hotkey.SlotKey)
            .ToArray();

        Assert.Equal(SlotKeyCatalog.All, registeredSlotKeys);
    }

    [Fact]
    public void HomeHotkeyShortPressDoesNotRaiseLongPress()
    {
        var keysDown = new HashSet<int>
        {
            Win32Constants.VkControl,
            (int)Win32Constants.VkNumpad0
        };
        using var service = CreateTestHotkeyService(
            keysDown,
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                keysDown.Remove((int)Win32Constants.VkNumpad0);
                return Task.CompletedTask;
            });
        var events = new List<string>();

        service.HotkeyPressed += (_, e) => events.Add($"pressed:{e.SlotKey}");
        service.HotkeyLongPressed += (_, e) => events.Add($"long:{e.SlotKey}");

        service.RaiseHotkeyPressed(SlotKey.Numpad0);

        Assert.True(SpinWait.SpinUntil(
            () => !keysDown.Contains((int)Win32Constants.VkNumpad0),
            AsyncEventTimeoutMilliseconds));
        Assert.Equal(["pressed:Numpad0"], events);
    }

    [Fact]
    public void HomeHotkeyLongPressRaisesLongPressOnceAfterImmediatePress()
    {
        var keysDown = new HashSet<int>
        {
            Win32Constants.VkControl,
            (int)Win32Constants.VkNumpad0
        };
        using var service = CreateTestHotkeyService(keysDown);
        var events = new List<string>();

        service.HotkeyPressed += (_, e) => events.Add($"pressed:{e.SlotKey}");
        service.HotkeyLongPressed += (_, e) =>
        {
            events.Add($"long:{e.SlotKey}");
            keysDown.Remove((int)Win32Constants.VkNumpad0);
        };

        service.RaiseHotkeyPressed(SlotKey.Numpad0);

        Assert.True(SpinWait.SpinUntil(() => events.Count == 2, AsyncEventTimeoutMilliseconds));
        Assert.Equal(["pressed:Numpad0", "long:Numpad0"], events);
    }

    [Fact]
    public void HomeHotkeyLongPressTrackingStartsBeforePressedHandlerReturns()
    {
        var keysDown = new HashSet<int>
        {
            Win32Constants.VkControl,
            (int)Win32Constants.VkNumpad0
        };
        var delayStarted = false;
        using var service = CreateTestHotkeyService(
            keysDown,
            (_, cancellationToken) =>
            {
                delayStarted = true;
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        var trackingStartedBeforeHandlerReturned = false;

        service.HotkeyPressed += (_, _) =>
        {
            trackingStartedBeforeHandlerReturned = SpinWait.SpinUntil(
                () => delayStarted,
                AsyncEventTimeoutMilliseconds);
            keysDown.Remove((int)Win32Constants.VkNumpad0);
        };

        service.RaiseHotkeyPressed(SlotKey.Numpad0);

        Assert.True(trackingStartedBeforeHandlerReturned);
    }

    [Fact]
    public void HomeHotkeyLongPressSuppressesRetriggerUntilReleased()
    {
        var keysDown = new HashSet<int>
        {
            Win32Constants.VkControl,
            (int)Win32Constants.VkNumpad0
        };
        using var service = CreateTestHotkeyService(keysDown);
        var events = new List<string>();

        service.HotkeyPressed += (_, e) => events.Add($"pressed:{e.SlotKey}");
        service.HotkeyLongPressed += (_, e) =>
        {
            events.Add($"long:{e.SlotKey}");
            service.RaiseHotkeyPressed(SlotKey.Numpad0);
            keysDown.Remove((int)Win32Constants.VkNumpad0);
        };

        service.RaiseHotkeyPressed(SlotKey.Numpad0);
        Assert.True(SpinWait.SpinUntil(() => events.Count == 2, AsyncEventTimeoutMilliseconds));
        keysDown.Add((int)Win32Constants.VkNumpad0);
        service.RaiseHotkeyPressed(SlotKey.Numpad0);
        Assert.True(SpinWait.SpinUntil(() => events.Count == 4, AsyncEventTimeoutMilliseconds));

        Assert.Equal(
            ["pressed:Numpad0", "long:Numpad0", "pressed:Numpad0", "long:Numpad0"],
            events);
    }

    [Fact]
    public void OtherHotkeysDoNotRaiseLongPress()
    {
        var keysDown = new HashSet<int>
        {
            Win32Constants.VkControl,
            (int)Win32Constants.VkNumpad0 + 1
        };
        using var service = CreateTestHotkeyService(keysDown);
        var events = new List<string>();

        service.HotkeyPressed += (_, e) => events.Add($"pressed:{e.SlotKey}");
        service.HotkeyLongPressed += (_, e) => events.Add($"long:{e.SlotKey}");

        service.RaiseHotkeyPressed(SlotKey.Numpad1);

        Assert.Equal(["pressed:Numpad1"], events);
    }

    private static HotkeyService CreateTestHotkeyService(
        ISet<int> keysDown,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        return new HotkeyService(
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromMilliseconds(5),
            keysDown.Contains,
            delayAsync ?? CompleteDelay);
    }

    private static async Task CompleteDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }
}
