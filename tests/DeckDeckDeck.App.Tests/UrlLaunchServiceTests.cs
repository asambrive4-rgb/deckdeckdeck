using DeckDeckDeck.App.Services;
using System.Diagnostics;

namespace DeckDeckDeck.App.Tests;

public sealed class UrlLaunchServiceTests
{
    [Fact]
    public void ShellLaunchReturningNoProcessStillReturnsTrue()
    {
        ProcessStartInfo? startInfo = null;
        var service = new UrlLaunchService(info =>
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
