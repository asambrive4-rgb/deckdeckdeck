// 역할: 전역 단축키가 눌렸을 때 어떤 키 조합이 입력되었는지 전달하는 이벤트 데이터입니다.
namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class DirectHotkeyPressedEventArgs : EventArgs
{
    public DirectHotkeyPressedEventArgs(Guid hotkeyActionId)
    {
        HotkeyActionId = hotkeyActionId;
    }

    public Guid HotkeyActionId { get; }
}
