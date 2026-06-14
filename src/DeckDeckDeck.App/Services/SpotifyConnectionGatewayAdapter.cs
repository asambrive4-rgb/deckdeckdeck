using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Services;

public sealed class SpotifyConnectionGatewayAdapter : ISpotifyConnectionGateway
{
    private readonly ISpotifyConnectionService _spotifyConnectionService;

    public SpotifyConnectionGatewayAdapter(ISpotifyConnectionService spotifyConnectionService)
    {
        _spotifyConnectionService = spotifyConnectionService;
    }

    public string DashboardUrl => _spotifyConnectionService.DashboardUrl;

    public string RedirectUri => _spotifyConnectionService.RedirectUri;

    public async Task<SpotifyConnectionGatewayResult> ConnectAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var result = await _spotifyConnectionService.ConnectAsync(clientId, cancellationToken);
        return new SpotifyConnectionGatewayResult(result.Succeeded, result.ErrorMessage);
    }

    public void Disconnect()
    {
        _spotifyConnectionService.Disconnect();
    }
}
