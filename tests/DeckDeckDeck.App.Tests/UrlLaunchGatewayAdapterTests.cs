using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using System.Diagnostics;

namespace DeckDeckDeck.App.Tests;

public sealed class UrlLaunchGatewayAdapterTests
{
    [Fact]
    public void ShellLaunchReturningNoProcessStillReturnsTrue()
    {
        ProcessStartInfo? startInfo = null;
        var service = new UrlLaunchGatewayAdapter(info =>
        {
            startInfo = info;
            return null;
        });

        var launched = service.TryLaunch("https://example.com");

        Assert.True(launched);
        Assert.NotNull(startInfo);
        Assert.Equal("https://example.com", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }
}
