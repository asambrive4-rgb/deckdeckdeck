using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.Tests;

public sealed class WindowPlacementRulesTests
{
    [Fact]
    public void IsUnsetOrWpfManualOriginMatchesKnownOrigins()
    {
        Assert.True(WindowPlacementRules.IsUnsetOrWpfManualOrigin(0, 0));
        Assert.True(WindowPlacementRules.IsUnsetOrWpfManualOrigin(0.1, -0.1));
        Assert.False(WindowPlacementRules.IsUnsetOrWpfManualOrigin(24, 24));
        Assert.False(WindowPlacementRules.IsUnsetOrWpfManualOrigin(1336, 376));
    }

    [Fact]
    public void HasUsableSavedCoordinatesRejectsUnsetAndTopLeftBand()
    {
        Assert.False(WindowPlacementRules.HasUsableSavedCoordinates(0, 0));
        Assert.False(WindowPlacementRules.HasUsableSavedCoordinates(130, 130));
        Assert.True(WindowPlacementRules.HasUsableSavedCoordinates(1336, 376));
        Assert.True(WindowPlacementRules.HasUsableSavedCoordinates(50, 250));
        Assert.True(WindowPlacementRules.HasUsableSavedCoordinates(250, 50));
    }

    [Fact]
    public void HasUsableSavedWindowPlacementRequiresBothCoordinates()
    {
        Assert.False(WindowPlacementRules.HasUsableSavedWindowPlacement(new AppSettings()));
        Assert.False(WindowPlacementRules.HasUsableSavedWindowPlacement(new AppSettings
        {
            LastWindowLeft = 1336
        }));
        Assert.False(WindowPlacementRules.HasUsableSavedWindowPlacement(new AppSettings
        {
            LastWindowTop = 376
        }));
        Assert.False(WindowPlacementRules.HasUsableSavedWindowPlacement(new AppSettings
        {
            LastWindowLeft = 130,
            LastWindowTop = 130
        }));
        Assert.True(WindowPlacementRules.HasUsableSavedWindowPlacement(new AppSettings
        {
            LastWindowLeft = 1336,
            LastWindowTop = 376
        }));
    }

    [Fact]
    public void PreparePastePalettePresentationBringsToFrontWhenSettingEnabled()
    {
        var useCase = new PreparePastePalettePresentationUseCase();
        var result = useCase.Execute(new PreparePastePalettePresentationRequest(
            new AppSettings { BringWindowToFrontOnHotkey = true }));

        Assert.Equal(PastePaletteZOrderMode.BringToFrontTemporarily, result.ZOrderMode);
    }

    [Fact]
    public void PreparePastePalettePresentationSendsToBottomWhenSettingDisabled()
    {
        var useCase = new PreparePastePalettePresentationUseCase();
        var result = useCase.Execute(new PreparePastePalettePresentationRequest(
            new AppSettings { BringWindowToFrontOnHotkey = false }));

        Assert.Equal(PastePaletteZOrderMode.SendToBottom, result.ZOrderMode);
    }
}
