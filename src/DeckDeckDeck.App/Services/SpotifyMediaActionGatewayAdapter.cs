using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Services;

internal sealed class SpotifyMediaActionGatewayAdapter : ISpotifyMediaActionGateway
{
    private readonly ISpotifyMediaActionService _spotifyMediaActionService;

    public SpotifyMediaActionGatewayAdapter(ISpotifyMediaActionService spotifyMediaActionService)
    {
        _spotifyMediaActionService = spotifyMediaActionService;
    }

    public async Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(
        SnippetMediaCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _spotifyMediaActionService.TryExecuteAsync(command, cancellationToken);
        return new SpotifyMediaActionGatewayResult(result.Succeeded, result.ErrorMessage);
    }
}
