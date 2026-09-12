// 역할: 슬롯에서 실행되는 타이머 상태 변화를 감지하여 화면에 남은 시간을 갱신하고 알림을 띄우는 중계자 역할을 합니다.
using System.Windows;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public interface ISlotTimerCoordinator : ITimerActionGateway
{
    void HandleTimerSlotClick(SlotKey slotKey);
}

public sealed class SlotTimerCoordinator : ISlotTimerCoordinator
{
    private readonly ISlotTimerUseCases _timerUseCases;
    private readonly Func<TimeSpan?>? _promptDuration;

    public SlotTimerCoordinator(
        ISlotTimerUseCases timerUseCases,
        Func<TimeSpan?>? promptDuration = null)
    {
        _timerUseCases = timerUseCases ?? throw new ArgumentNullException(nameof(timerUseCases));
        _promptDuration = promptDuration;
    }

    public void ExecuteTimerAction(SlotKey slotKey)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => HandleTimerSlotClick(slotKey));
        }
        else
        {
            HandleTimerSlotClick(slotKey);
        }
    }

    public void HandleTimerSlotClick(SlotKey slotKey)
    {
        if (_timerUseCases.IsRunning(slotKey))
        {
            _timerUseCases.StopTimer(slotKey);
            return;
        }

        var duration = _promptDuration?.Invoke();
        if (duration.HasValue && duration.Value > TimeSpan.Zero)
        {
            _timerUseCases.StartTimer(slotKey, duration.Value);
        }
    }
}
