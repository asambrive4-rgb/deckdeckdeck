using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using Xunit;

namespace DeckDeckDeck.App.Tests;

public sealed class SlotTimerUseCasesTests
{
    private sealed class FakeToastPort : IToastNotificationPort
    {
        public List<(string Title, string Message)> ShowToastCalls { get; } = new();
        public int PlayCompletionSoundCalls { get; private set; }

        public void ShowToast(string title, string message)
        {
            ShowToastCalls.Add((title, message));
        }

        public void PlayCompletionSound()
        {
            PlayCompletionSoundCalls++;
        }
    }

    [Fact]
    public void StartTimer_ShouldStartTimerAndNotifyStarted()
    {
        // Arrange
        var fakeToast = new FakeToastPort();
        using var useCases = new SlotTimerUseCases(fakeToast);
        var slotKey = SlotKey.Numpad1;

        // Act
        useCases.StartTimer(slotKey, TimeSpan.FromMinutes(5));

        // Assert
        Assert.True(useCases.IsRunning(slotKey));
        var state = useCases.GetTimerState(slotKey);
        Assert.True(state.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(5), state.TotalDuration);
        Assert.Single(fakeToast.ShowToastCalls);
        Assert.Contains("타이머 시작", fakeToast.ShowToastCalls[0].Title);
    }

    [Fact]
    public void StopTimer_ShouldStopTimerAndNotifyStopped()
    {
        // Arrange
        var fakeToast = new FakeToastPort();
        using var useCases = new SlotTimerUseCases(fakeToast);
        var slotKey = SlotKey.Numpad1;
        useCases.StartTimer(slotKey, TimeSpan.FromMinutes(5));

        // Act
        useCases.StopTimer(slotKey);

        // Assert
        Assert.False(useCases.IsRunning(slotKey));
        var state = useCases.GetTimerState(slotKey);
        Assert.False(state.IsRunning);
        Assert.Equal(2, fakeToast.ShowToastCalls.Count);
        Assert.Contains("타이머 정지", fakeToast.ShowToastCalls[1].Title);
    }

    [Fact]
    public async Task TimerExpiration_ShouldNotifyCompletion()
    {
        // Arrange
        var fakeToast = new FakeToastPort();
        using var useCases = new SlotTimerUseCases(fakeToast);
        var slotKey = SlotKey.Numpad2;

        // Act
        useCases.StartTimer(slotKey, TimeSpan.FromMilliseconds(200));

        // Wait for tick
        await Task.Delay(1000);

        // Assert
        Assert.False(useCases.IsRunning(slotKey));
        Assert.Equal(2, fakeToast.ShowToastCalls.Count);
        Assert.Contains("타이머 완료!", fakeToast.ShowToastCalls[1].Title);
        Assert.Equal(1, fakeToast.PlayCompletionSoundCalls);
    }

    [Fact]
    public void MultipleTimers_ShouldRunIndependently()
    {
        // Arrange
        var fakeToast = new FakeToastPort();
        using var useCases = new SlotTimerUseCases(fakeToast);
        var slot1 = SlotKey.Numpad1;
        var slot2 = SlotKey.Numpad2;

        // Act
        useCases.StartTimer(slot1, TimeSpan.FromMinutes(3));
        useCases.StartTimer(slot2, TimeSpan.FromMinutes(10));

        // Assert
        Assert.True(useCases.IsRunning(slot1));
        Assert.True(useCases.IsRunning(slot2));

        useCases.StopTimer(slot1);
        Assert.False(useCases.IsRunning(slot1));
        Assert.True(useCases.IsRunning(slot2));
    }
}
