// 역할: 데이터베이스에 저장되는 개별 설정의 키(이름)와 값 데이터를 나타내는 모델입니다.
namespace DeckDeckDeck.App.Models;

public sealed class SettingEntry
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
