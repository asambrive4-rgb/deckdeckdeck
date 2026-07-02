using DeckDeckDeck.App.Infrastructure.Platform;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class AppIconProviderTests
{
    [Fact]
    public void WindowIconIsLoadedOnceAndReused()
    {
        RunInSta(() =>
        {
            var provider = new AppIconProvider();

            var first = provider.GetWindowIcon();
            var second = provider.GetWindowIcon();

            Assert.NotNull(first);
            Assert.Same(first, second);
            return true;
        });
    }

    [Fact]
    public void TrayIconCanBeCreatedFromSharedProvider()
    {
        RunInSta(() =>
        {
            var provider = new AppIconProvider();

            using var first = provider.CreateTrayIcon();
            using var second = provider.CreateTrayIcon();

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            return true;
        });
    }
}
