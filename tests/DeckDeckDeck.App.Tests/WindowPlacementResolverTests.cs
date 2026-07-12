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

    [Fact]
    public void WindowPlacementDefaultMatchesCapturedBottomRightSize()
    {
        var settings = new AppSettings();
        var primary = new WindowWorkArea("Primary", 0, 0, 1920, 1032, true);

        var placement = WindowPlacementResolver.Resolve(settings, 440, 580, [primary]);

        Assert.Equal(1456, placement.Left);
        Assert.Equal(428, placement.Top);
        Assert.Equal("Primary", placement.ScreenDeviceName);
    }

    [Fact]
    public void WindowPlacementIgnoresWpfManualOriginAndUsesBottomRight()
    {
        // Corrupted save from shell-first startup (Manual default before settings load).
        var settings = new AppSettings
        {
            LastWindowLeft = 0,
            LastWindowTop = 0,
            LastWindowScreenDeviceName = "Primary"
        };
        var primary = new WindowWorkArea("Primary", 0, 0, 1920, 1080, true);

        var placement = WindowPlacementResolver.Resolve(settings, 560, 680, [primary]);

        Assert.Equal(1336, placement.Left);
        Assert.Equal(376, placement.Top);
        Assert.Equal("Primary", placement.ScreenDeviceName);
    }

    [Fact]
    public void IsUnsetOrWpfManualOriginDetectsZeroZero()
    {
        Assert.True(WindowPlacementResolver.IsUnsetOrWpfManualOrigin(0, 0));
        Assert.True(WindowPlacementResolver.IsUnsetOrWpfManualOrigin(0.1, -0.1));
        Assert.False(WindowPlacementResolver.IsUnsetOrWpfManualOrigin(24, 24));
        Assert.False(WindowPlacementResolver.IsUnsetOrWpfManualOrigin(1336, 376));
    }

    [Fact]
    public void WindowPlacementIgnoresNearTopLeftShellFirstCorruption()
    {
        // Real user DB after shell-first bug: lastWindowLeft/Top ≈ 130 (not exactly 0).
        var settings = new AppSettings
        {
            LastWindowLeft = 130,
            LastWindowTop = 130,
            LastWindowScreenDeviceName = @"\\.\DISPLAY1"
        };
        var primary = new WindowWorkArea("Primary", 0, 0, 1920, 1080, true);

        var placement = WindowPlacementResolver.Resolve(settings, 560, 680, [primary]);

        Assert.Equal(1336, placement.Left);
        Assert.Equal(376, placement.Top);
        Assert.True(WindowPlacementResolver.IsLikelyShellFirstCorruption(130, 130, [primary]));
        Assert.False(WindowPlacementResolver.IsLikelyShellFirstCorruption(1336, 376, [primary]));
    }
}
