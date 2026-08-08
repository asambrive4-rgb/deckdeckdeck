namespace DeckDeckDeck.App.Models;

public sealed record SlotTimerState(
    SlotKey SlotKey,
    TimeSpan TotalDuration,
    TimeSpan RemainingTime,
    bool IsRunning,
    DateTimeOffset? TargetEndTimeUtc = null)
{
    public string FormattedRemainingTime =>
        RemainingTime.TotalHours >= 1
            ? RemainingTime.ToString(@"hh\:mm\:ss")
            : RemainingTime.ToString(@"mm\:ss");

    public static SlotTimerState Stopped(SlotKey slotKey) =>
        new(slotKey, TimeSpan.Zero, TimeSpan.Zero, false, null);

    public static SlotTimerState Started(SlotKey slotKey, TimeSpan duration, DateTimeOffset targetEndTimeUtc) =>
        new(slotKey, duration, duration, true, targetEndTimeUtc);
}
