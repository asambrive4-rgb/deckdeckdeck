// 역할: 슬롯 번호(1~9)와 키패드 키 매핑 목록이 누락 없이 올바르게 구성되어 있는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Tests;

public sealed class SlotKeyCatalogTests
{
    [Fact]
    public void SymbolKeysArePlacedAfterHotkeyPlaceholderOnTopRow()
    {
        Assert.Equal(0, SlotKey.NumpadDivide.GetGridRow());
        Assert.Equal(1, SlotKey.NumpadDivide.GetGridColumn());

        Assert.Equal(0, SlotKey.NumpadMultiply.GetGridRow());
        Assert.Equal(2, SlotKey.NumpadMultiply.GetGridColumn());

        Assert.Equal(0, SlotKey.NumpadSubtract.GetGridRow());
        Assert.Equal(3, SlotKey.NumpadSubtract.GetGridColumn());
    }
}
