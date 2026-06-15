using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class WindowPlacementResolverTests
{
    [Fact]
    public void WindowPlacementUsesSavedPositionInsideWorkArea()
    {
        var settings = new AppSettings
        {
            LastWindowLeft = 2100,
            LastWindowTop = 120,
            LastWindowScreenDeviceName = "Secondary"
        };
        var workAreas = new[]
        {
            new WindowWorkArea("Primary", 0, 0, 1920, 1080, true),
            new WindowWorkArea("Secondary", 1920, 0, 1920, 1080)
        };

        var placement = WindowPlacementResolver.Resolve(settings, 560, 680, workAreas);

        Assert.Equal(2100, placement.Left);
        Assert.Equal(120, placement.Top);
        Assert.Equal("Secondary", placement.ScreenDeviceName);
    }

    [Fact]
    public void WindowPlacementFallsBackToBottomRightWhenSavedPositionIsOffScreen()
    {
        var settings = new AppSettings
        {
            LastWindowLeft = 5000,
            LastWindowTop = 3000,
            LastWindowScreenDeviceName = "Missing"
        };
        var workAreas = new[]
        {
            new WindowWorkArea("Primary", 0, 0, 1920, 1080, true)
        };

        var placement = WindowPlacementResolver.Resolve(settings, 560, 680, workAreas);

        Assert.Equal(1336, placement.Left);
        Assert.Equal(376, placement.Top);
        Assert.Equal("Primary", placement.ScreenDeviceName);
    }

    [Fact]
    public void WindowPlacementUsesFallbackWorkAreaWhenNoSavedPositionExists()
    {
        var settings = new AppSettings();
        var primary = new WindowWorkArea("Primary", 0, 0, 1920, 1080, true);
        var secondary = new WindowWorkArea("Secondary", 1920, 0, 1920, 1080);

        var placement = WindowPlacementResolver.Resolve(
            settings,
            560,
            680,
            [primary, secondary],
            secondary);

        Assert.Equal(3256, placement.Left);
        Assert.Equal(376, placement.Top);
        Assert.Equal("Secondary", placement.ScreenDeviceName);
    }
}
