// 역할: 슬롯 번호와 단축키 등 슬롯 구성 데이터의 유효성을 검증하는 규칙을 정의합니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public static class SlotRules
{
    public static bool IsEnabled(
        SlotKey slotKey,
        IReadOnlyDictionary<SlotKey, bool> enabledSlotKeys) =>
        !enabledSlotKeys.TryGetValue(slotKey, out var enabled) || enabled;
}
