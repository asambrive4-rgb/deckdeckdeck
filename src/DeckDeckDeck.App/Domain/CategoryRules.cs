namespace DeckDeckDeck.App.Domain;

public static class CategoryRules
{
    public const string NameRequiredMessage = "카테고리 이름을 입력해 주세요.";

    public static string? ValidateName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? NameRequiredMessage : null;
    }
}
