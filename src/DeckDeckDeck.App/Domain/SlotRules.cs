// 역할: 슬롯 번호, 단축키 유효성 검증 및 슬롯 표시 텍스트 규칙을 정의합니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public static class SlotRules
{
    public static bool IsEnabled(
        SlotKey slotKey,
        IReadOnlyDictionary<SlotKey, bool> enabledSlotKeys) =>
        !enabledSlotKeys.TryGetValue(slotKey, out var enabled) || enabled;

    public static string FormatTransferTargetLabel(SlotKey slotKey, string? existingTitle) =>
        $"슬롯 {slotKey.GetDisplayText()} - {(string.IsNullOrWhiteSpace(existingTitle) ? "비어 있음" : existingTitle)}";
}

