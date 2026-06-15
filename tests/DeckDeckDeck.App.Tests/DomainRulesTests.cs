using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Tests;

public sealed class DomainRulesTests
{
    [Fact]
    public void SnippetPasteTextRequiresContent()
    {
        var result = SnippetRules.ValidateForSave(
            "Paste",
            string.Empty,
            SnippetActionType.PasteText,
            launchPath: null,
            launchUrl: null,
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause);

        Assert.False(result.Succeeded);
        Assert.Equal("붙여넣을 문구를 입력해 주세요.", result.ErrorMessage);
    }

    [Fact]
    public void SnippetLaunchUrlNormalizesHttpAddress()
    {
        var result = SnippetRules.ValidateForSave(
            "Docs",
            string.Empty,
            SnippetActionType.LaunchUrl,
            launchPath: null,
            launchUrl: "example.com/docs",
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause);

        Assert.True(result.Succeeded);
        Assert.Equal("https://example.com/docs", result.NormalizedLaunchUrl);
    }

    [Fact]
    public void MediaCommandFallsBackWhenProviderDoesNotSupportCommand()
    {
        var command = MediaCommandRules.GetValidCommandForProvider(
            SnippetMediaProvider.System,
            SnippetMediaCommand.CycleRepeat);

        Assert.Equal(SnippetMediaCommand.PlayPause, command);
    }

    [Fact]
    public void SlotIsEnabledWhenStateIsMissing()
    {
        Assert.True(SlotRules.IsEnabled(SlotKey.Numpad3, new Dictionary<SlotKey, bool>()));
    }
}

