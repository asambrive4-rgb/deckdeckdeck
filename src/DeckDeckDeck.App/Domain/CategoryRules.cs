// 역할: 카테고리 이름이나 색상 등 카테고리 정보가 올바른 형식인지 검증하는 비즈니스 규칙을 정의합니다.
namespace DeckDeckDeck.App.Domain;

public static class CategoryRules
{
    public const string NameRequiredMessage = "카테고리 이름을 입력해 주세요.";

    public static string? ValidateName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? NameRequiredMessage : null;
}
