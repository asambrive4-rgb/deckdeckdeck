// 역할: 단축키 키 조합(보조 키 + 일반 키)의 파싱과 동등성 비교가 정확한지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Tests;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData(0x4B, HotkeyModifiers.Control | HotkeyModifiers.Shift, "Ctrl + Shift + K")]
    [InlineData(0x27, HotkeyModifiers.None, "Right Arrow")]
    [InlineData(0x67, HotkeyModifiers.None, "Numpad 7")]
    public void DisplayTextShowsCommonHotkeys(uint virtualKey, HotkeyModifiers modifiers, string expected)
    {
        var gesture = new HotkeyGesture(virtualKey, modifiers);

        Assert.True(gesture.IsComplete);
        Assert.Equal(expected, gesture.DisplayText);
    }

    [Fact]
    public void ModifierOnlyKeyIsNotComplete()
    {
        var gesture = new HotkeyGesture(Win32Constants.VkControl, HotkeyModifiers.Control);

        Assert.False(gesture.IsComplete);
    }
}
