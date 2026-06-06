using DeckDeckDeck.App.Services;
using System.Diagnostics;

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

    [Fact]
    public void ShellLaunchReturningNoProcessStillReturnsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, string.Empty);
        ProcessStartInfo? startInfo = null;
        var service = new FileLaunchService(info =>
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
