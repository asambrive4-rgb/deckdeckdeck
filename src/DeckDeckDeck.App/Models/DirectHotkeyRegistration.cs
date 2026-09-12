// 역할: 특정 슬롯에 직접 매핑된 전역 단축키 등록 정보를 나타내는 데이터 모델입니다.
namespace DeckDeckDeck.App.Models;

public sealed record DirectHotkeyRegistration(Guid HotkeyActionId, HotkeyGesture Gesture);
