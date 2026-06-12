namespace DeckDeckDeck.App.Services;

public interface ISpotifyConnectionService
{
    string DashboardUrl { get; }

    string RedirectUri { get; }

    Task<SpotifyConnectionResult> ConnectAsync(string clientId, CancellationToken cancellationToken = default);

    Task<SpotifyConnectionCheckResult> CheckConnectionAsync(CancellationToken cancellationToken = default);

    void Disconnect();
}

public sealed record SpotifyConnectionResult(bool Succeeded, string? ErrorMessage = null);

public enum SpotifyConnectionCheckState
{
    Connected,
    Disconnected,
    Unknown
}

public sealed record SpotifyConnectionCheckResult(
    SpotifyConnectionCheckState State,
    string? ErrorMessage = null);
