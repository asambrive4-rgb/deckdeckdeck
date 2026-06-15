using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class UrlAddressTests
{
    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("example.com/docs", "https://example.com/docs")]
    [InlineData("localhost:5000", "https://localhost:5000")]
    [InlineData("127.0.0.1:5000", "https://127.0.0.1:5000")]
    public void MissingSchemeIsNormalizedToHttps(string input, string expected)
    {
        var valid = UrlAddress.TryNormalize(input, out var normalizedUrl);

        Assert.True(valid);
        Assert.Equal(expected, normalizedUrl);
    }

    [Theory]
    [InlineData("https://example.com/docs")]
    [InlineData("http://example.com/docs")]
    public void HttpAndHttpsUrlsAreAcceptedAsEntered(string input)
    {
        var valid = UrlAddress.TryNormalize(input, out var normalizedUrl);

        Assert.True(valid);
        Assert.Equal(input, normalizedUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://example.com")]
    [InlineData("search terms")]
    [InlineData("openai")]
    public void UnsupportedOrSearchLikeInputIsRejected(string input)
    {
        var valid = UrlAddress.TryNormalize(input, out var normalizedUrl);

        Assert.False(valid);
        Assert.Equal(string.Empty, normalizedUrl);
    }
}
