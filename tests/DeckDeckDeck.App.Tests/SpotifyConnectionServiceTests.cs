using System.Net;
using System.Net.Http;
using System.Text;
using DeckDeckDeck.App.Services;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class SpotifyConnectionServiceTests
{
    [Fact]
    public async Task ConnectUsesPkceAndSavesTokens()
    {
        var services = CreateServices();
        var urlLaunchService = new RecordingUrlLaunchService();
        var callbackListener = new StubSpotifyCallbackListener("authorization-code");
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"access_token":"access-token","refresh_token":"refresh-token","expires_in":3600}""");
        var service = new SpotifyConnectionService(
            services.SettingsService,
            urlLaunchService,
            new HttpClient(handler),
            callbackListener);

        var result = await service.ConnectAsync("client-id");

        var settings = services.SettingsService.Load();
        var authorizeUrl = Assert.Single(urlLaunchService.Urls);
        Assert.True(result.Succeeded);
        Assert.Contains("https://accounts.spotify.com/authorize?", authorizeUrl);
        Assert.Contains("client_id=client-id", authorizeUrl);
        Assert.Contains("code_challenge_method=S256", authorizeUrl);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A53682%2Fspotify-callback%2F", authorizeUrl);
        Assert.Equal("client-id", settings.SpotifyClientId);
        Assert.Equal("access-token", settings.SpotifyAccessToken);
        Assert.Equal("refresh-token", settings.SpotifyRefreshToken);
        Assert.NotNull(settings.SpotifyTokenExpiresAt);
    }

    [Fact]
    public async Task ConnectRequiresClientId()
    {
        var services = CreateServices();
        var urlLaunchService = new RecordingUrlLaunchService();
        var service = new SpotifyConnectionService(
            services.SettingsService,
            urlLaunchService,
            new HttpClient(new QueueHttpMessageHandler()),
            new StubSpotifyCallbackListener("authorization-code"));

        var result = await service.ConnectAsync(" ");

        Assert.False(result.Succeeded);
        Assert.Equal("Spotify Client ID를 입력해 주세요.", result.ErrorMessage);
        Assert.Empty(urlLaunchService.Urls);
    }

    [Fact]
    public void DisconnectClearsSpotifySettings()
    {
        var services = CreateServices();
        var settings = services.SettingsService.Load();
        settings.SpotifyClientId = "client-id";
        settings.SpotifyAccessToken = "access-token";
        settings.SpotifyRefreshToken = "refresh-token";
        settings.SpotifyTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        settings.SpotifyConnectedUserDisplayName = "Spotify 계정";
        services.SettingsService.Save(settings);
        var service = new SpotifyConnectionService(
            services.SettingsService,
            new RecordingUrlLaunchService(),
            new HttpClient(new QueueHttpMessageHandler()),
            new StubSpotifyCallbackListener("authorization-code"));

        service.Disconnect();

        var reloaded = services.SettingsService.Load();
        Assert.Equal(string.Empty, reloaded.SpotifyClientId);
        Assert.Equal(string.Empty, reloaded.SpotifyAccessToken);
        Assert.Equal(string.Empty, reloaded.SpotifyRefreshToken);
        Assert.Null(reloaded.SpotifyTokenExpiresAt);
        Assert.Equal(string.Empty, reloaded.SpotifyConnectedUserDisplayName);
    }

    private sealed class StubSpotifyCallbackListener : ISpotifyAuthorizationCallbackListener
    {
        private readonly string _code;

        public StubSpotifyCallbackListener(string code)
        {
            _code = code;
        }

        public Task<SpotifyAuthorizationCallbackResult> WaitForCallbackAsync(
            Uri redirectUri,
            string expectedState,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new SpotifyAuthorizationCallbackResult(true, Code: _code));
        }
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = [];

        public void EnqueueJson(HttpStatusCode statusCode, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
