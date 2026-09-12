// 역할: 슬롯 타이머 시작부터 종료까지의 진행 상태 변화와 토스트 알림 연동이 정확한지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using Xunit;

namespace DeckDeckDeck.App.Tests;

public sealed class SlotTimerCoordinatorTests
{
    private sealed class FakeToastPort : IToastNotificationPort
    {
        public void ShowToast(string title, string message) { }
        public void PlayCompletionSound() { }
    }

    [Fact]
    public void HandleTimerSlotClick_WhenTimerRunning_StopsTimerWithoutPrompt()
    {
        var toast = new FakeToastPort();
        using var useCases = new SlotTimerUseCases(toast);
        var slotKey = SlotKey.Numpad3;
        useCases.StartTimer(slotKey, TimeSpan.FromMinutes(10));

        var promptCalled = false;
        var coordinator = new SlotTimerCoordinator(useCases, () =>
        {
            promptCalled = true;
            return TimeSpan.FromMinutes(5);
        });

        coordinator.HandleTimerSlotClick(slotKey);

        Assert.False(useCases.IsRunning(slotKey));
        Assert.False(promptCalled);
    }

    [Fact]
    public void HandleTimerSlotClick_WhenTimerNotRunning_StartsTimerWithPromptedDuration()
    {
        var toast = new FakeToastPort();
        using var useCases = new SlotTimerUseCases(toast);
        var slotKey = SlotKey.Numpad3;

        var coordinator = new SlotTimerCoordinator(useCases, () => TimeSpan.FromMinutes(15));

        coordinator.HandleTimerSlotClick(slotKey);

        Assert.True(useCases.IsRunning(slotKey));
        var state = useCases.GetTimerState(slotKey);
        Assert.Equal(TimeSpan.FromMinutes(15), state.TotalDuration);
    }

    [Fact]
    public void HandleTimerSlotClick_WhenPromptCancelled_DoesNotStartTimer()
    {
        var toast = new FakeToastPort();
        using var useCases = new SlotTimerUseCases(toast);
        var slotKey = SlotKey.Numpad3;

        var coordinator = new SlotTimerCoordinator(useCases, () => null);

        coordinator.HandleTimerSlotClick(slotKey);

        Assert.False(useCases.IsRunning(slotKey));
    }
}
