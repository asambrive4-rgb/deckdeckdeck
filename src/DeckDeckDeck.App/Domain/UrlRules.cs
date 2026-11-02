// 역할: 웹 브라우저로 열려는 인터넷 주소(URL) 형식이 올바른지 검증하는 규칙을 정의합니다.
using System.Net;

namespace DeckDeckDeck.App.Domain;

public static class UrlRules
{
    public static bool TryNormalize(string? value, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (ContainsWhitespace(trimmed))
        {
            return false;
        }

        var candidate = HasSchemeSeparator(trimmed)
            ? trimmed
            : $"https://{trimmed}";

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || !IsHttpScheme(uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !HasSupportedHost(uri.Host))
        {
            return false;
        }

        normalizedUrl = candidate;
        return true;
    }

    private static bool ContainsWhitespace(string value) => value.Any(char.IsWhiteSpace);

    private static bool HasSchemeSeparator(string value) =>
        value.Contains("://", StringComparison.Ordinal);

    private static bool IsHttpScheme(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static bool HasSupportedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out _))
        {
            return true;
        }

        return host.Contains('.', StringComparison.Ordinal)
            && Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }
}
