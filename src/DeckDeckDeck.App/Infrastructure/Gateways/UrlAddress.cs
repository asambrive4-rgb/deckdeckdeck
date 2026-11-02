// 역할: 웹 주소(URL) 유효성 검증 및 표준화를 도메인 규칙에 위임하는 도우미입니다.
using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public static class UrlAddress
{
    public static bool TryNormalize(string? value, out string normalizedUrl) =>
        UrlRules.TryNormalize(value, out normalizedUrl);
}

