using System.Collections.Concurrent;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class SlotTimerStateChangedEventArgs : EventArgs
{
    public SlotKey SlotKey { get; }
    public SlotTimerState State { get; }

    public SlotTimerStateChangedEventArgs(SlotKey slotKey, SlotTimerState state)
    {
        SlotKey = slotKey;
        State = state;
    }
}

public interface ISlotTimerUseCases
{
    event EventHandler<SlotTimerStateChangedEventArgs>? TimerStateChanged;
    SlotTimerState GetTimerState(SlotKey slotKey);
    bool IsRunning(SlotKey slotKey);
    void StartTimer(SlotKey slotKey, TimeSpan duration);
    void StopTimer(SlotKey slotKey);
}

public sealed class SlotTimerUseCases : ISlotTimerUseCases, IDisposable
{
    private readonly IToastNotificationPort _toastPort;
    private readonly ConcurrentDictionary<SlotKey, ActiveTimerInfo> _activeTimers = new();
    private readonly Timer _tickTimer;
    private readonly object _lock = new();

    public event EventHandler<SlotTimerStateChangedEventArgs>? TimerStateChanged;

    private sealed class ActiveTimerInfo
    {
        public SlotKey SlotKey { get; }
        public TimeSpan TotalDuration { get; }
        public DateTimeOffset TargetEndTimeUtc { get; }

        public ActiveTimerInfo(SlotKey slotKey, TimeSpan totalDuration, DateTimeOffset targetEndTimeUtc)
        {
            SlotKey = slotKey;
            TotalDuration = totalDuration;
            TargetEndTimeUtc = targetEndTimeUtc;
        }
    }

    public SlotTimerUseCases(IToastNotificationPort toastPort)
    {
        _toastPort = toastPort ?? throw new ArgumentNullException(nameof(toastPort));
        _tickTimer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public SlotTimerState GetTimerState(SlotKey slotKey)
    {
        if (_activeTimers.TryGetValue(slotKey, out var info))
        {
            var remaining = info.TargetEndTimeUtc - DateTimeOffset.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            return new SlotTimerState(slotKey, info.TotalDuration, remaining, true, info.TargetEndTimeUtc);
        }

        return SlotTimerState.Stopped(slotKey);
    }

    public bool IsRunning(SlotKey slotKey)
    {
        return _activeTimers.ContainsKey(slotKey);
    }

    public void StartTimer(SlotKey slotKey, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;

        var targetEndTimeUtc = DateTimeOffset.UtcNow.Add(duration);
        var info = new ActiveTimerInfo(slotKey, duration, targetEndTimeUtc);

        _activeTimers[slotKey] = info;
        EnsureTickerRunning();

        var state = new SlotTimerState(slotKey, duration, duration, true, targetEndTimeUtc);
        TimerStateChanged?.Invoke(this, new SlotTimerStateChangedEventArgs(slotKey, state));

        string displayDuration = duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:F0}초"
            : $"{duration.TotalMinutes:F0}분";

        _toastPort.ShowToast("타이머 시작", $"{displayDuration} 타이머가 시작되었습니다.");
    }

    public void StopTimer(SlotKey slotKey)
    {
        if (_activeTimers.TryRemove(slotKey, out _))
        {
            var stoppedState = SlotTimerState.Stopped(slotKey);
            TimerStateChanged?.Invoke(this, new SlotTimerStateChangedEventArgs(slotKey, stoppedState));

            _toastPort.ShowToast("타이머 정지", "타이머가 정지되었습니다.");
        }

        if (_activeTimers.IsEmpty)
        {
            _tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void EnsureTickerRunning()
    {
        lock (_lock)
        {
            _tickTimer.Change(0, 500);
        }
    }

    private void OnTick(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredList = new List<SlotKey>();

        foreach (var kvp in _activeTimers)
        {
            var slotKey = kvp.Key;
            var info = kvp.Value;

            var remaining = info.TargetEndTimeUtc - now;
            if (remaining <= TimeSpan.Zero)
            {
                expiredList.Add(slotKey);
            }
            else
            {
                var timerState = new SlotTimerState(slotKey, info.TotalDuration, remaining, true, info.TargetEndTimeUtc);
                TimerStateChanged?.Invoke(this, new SlotTimerStateChangedEventArgs(slotKey, timerState));
            }
        }

        foreach (var expiredSlot in expiredList)
        {
            if (_activeTimers.TryRemove(expiredSlot, out var info))
            {
                var completedState = SlotTimerState.Stopped(expiredSlot);
                TimerStateChanged?.Invoke(this, new SlotTimerStateChangedEventArgs(expiredSlot, completedState));

                string displayDuration = info.TotalDuration.TotalSeconds < 60
                    ? $"{info.TotalDuration.TotalSeconds:F0}초"
                    : $"{info.TotalDuration.TotalMinutes:F0}분";

                _toastPort.ShowToast("타이머 완료!", $"{displayDuration} 타이머가 종료되었습니다.");
                _toastPort.PlayCompletionSound();
            }
        }

        if (_activeTimers.IsEmpty)
        {
            lock (_lock)
            {
                _tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
    }

    public void Dispose()
    {
        _tickTimer.Dispose();
        _activeTimers.Clear();
    }
}
