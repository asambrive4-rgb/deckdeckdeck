using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using System.Diagnostics;

namespace DeckDeckDeck.App.Tests;

public sealed class FileLaunchGatewayAdapterTests
{
    [Fact]
    public void MissingPathReturnsFalse()
    {
        var service = new FileLaunchGatewayAdapter();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe");

        var launched = service.TryLaunch(missingPath);

        Assert.False(launched);
    }

    [Fact]
    public void ShellLaunchReturningNoProcessStillReturnsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, string.Empty);
        ProcessStartInfo? startInfo = null;
        var service = new FileLaunchGatewayAdapter(info =>
        {
            startInfo = info;
            return null;
        });

        try
        {
            var launched = service.TryLaunch(path);

            Assert.True(launched);
            Assert.NotNull(startInfo);
            Assert.Equal(path, startInfo.FileName);
            Assert.True(startInfo.UseShellExecute);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
