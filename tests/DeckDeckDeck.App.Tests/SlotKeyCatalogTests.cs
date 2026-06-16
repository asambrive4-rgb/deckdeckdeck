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
