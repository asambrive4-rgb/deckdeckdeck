using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public interface ISpotifyMediaActionService
{
    Task<SpotifyMediaActionResult> TryExecuteAsync(
        SnippetMediaCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record SpotifyMediaActionResult(bool Succeeded, string? ErrorMessage = null);
