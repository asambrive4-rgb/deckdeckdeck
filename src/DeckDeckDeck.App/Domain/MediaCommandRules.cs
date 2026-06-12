using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public static class MediaCommandRules
{
    private static readonly HashSet<SnippetMediaCommand> SystemCommands =
    [
        SnippetMediaCommand.PlayPause,
        SnippetMediaCommand.PreviousTrack,
        SnippetMediaCommand.NextTrack,
        SnippetMediaCommand.Stop,
        SnippetMediaCommand.Mute,
        SnippetMediaCommand.VolumeUp,
        SnippetMediaCommand.VolumeDown
    ];

    private static readonly HashSet<SnippetMediaCommand> SpotifyCommands =
    [
        SnippetMediaCommand.PlayPause,
        SnippetMediaCommand.PreviousTrack,
        SnippetMediaCommand.NextTrack,
        SnippetMediaCommand.ToggleShuffle,
        SnippetMediaCommand.CycleRepeat,
        SnippetMediaCommand.OpenSpotifyAndResume
    ];

    public static bool IsValidForProvider(
        SnippetMediaProvider provider,
        SnippetMediaCommand command)
    {
        return provider == SnippetMediaProvider.Spotify
            ? SpotifyCommands.Contains(command)
            : SystemCommands.Contains(command);
    }

    public static SnippetMediaCommand GetValidCommandForProvider(
        SnippetMediaProvider provider,
        SnippetMediaCommand command)
    {
        return IsValidForProvider(provider, command)
            ? command
            : SnippetMediaCommand.PlayPause;
    }
}
