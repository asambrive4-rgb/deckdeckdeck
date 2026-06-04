using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.Tests;

public sealed class FileLaunchServiceTests
{
    [Fact]
    public void MissingPathReturnsFalse()
    {
        var service = new FileLaunchService();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe");

        var launched = service.TryLaunch(missingPath);

        Assert.False(launched);
    }
}
