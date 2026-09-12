// 역할: 특정 슬롯에서 실행 중인 타이머의 남은 시간과 진행 상태 정보를 보관하는 데이터 모델입니다.
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
