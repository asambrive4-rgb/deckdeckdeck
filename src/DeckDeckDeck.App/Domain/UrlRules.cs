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

    private static bool ContainsWhitespace(string value)
    {
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSchemeSeparator(string value)
    {
        return value.Contains("://", StringComparison.Ordinal);
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

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
