using System.Net;
using System.Net.Http;
using System.Text;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class SpotifyMediaActionGatewayAdapterTests
{
    [Fact]
    public async Task ExecuteReturnsReconnectMessageWhenSpotifyIsDisconnected()
    {
        var services = CreateServices();
        var handler = new QueueHttpMessageHandler();
        var service = new SpotifyMediaActionGatewayAdapter(services.SettingsRepository, new HttpClient(handler));

        var result = await service.TryExecuteAsync(SnippetMediaCommand.NextTrack);

        Assert.False(result.Succeeded);
        Assert.Equal("Spotify를 다시 연결해 주세요.", result.ErrorMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ExpiredTokenRefreshesBeforeExecutingCommand()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"new-access","expires_in":3600}""");
        handler.Enqueue(HttpStatusCode.NoContent);
        var service = new SpotifyMediaActionGatewayAdapter(services.SettingsRepository, new HttpClient(handler));

        var result = await service.TryExecuteAsync(SnippetMediaCommand.NextTrack);

        Assert.True(result.Succeeded);
        Assert.Collection(
            handler.Requests,
            refresh => Assert.Equal("https://accounts.spotify.com/api/token", refresh.Url),
            next =>
            {
                Assert.Equal("https://api.spotify.com/v1/me/player/next", next.Url);
                Assert.Equal("Bearer new-access", next.Authorization);
            });
        Assert.Equal("new-access", services.SettingsRepository.Load().SpotifyAccessToken);
    }

    [Fact]
    public async Task UnauthorizedCommandRefreshesTokenAndRetriesOnce()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"new-access","expires_in":3600}""");
        handler.Enqueue(HttpStatusCode.NoContent);
        var service = new SpotifyMediaActionGatewayAdapter(services.SettingsRepository, new HttpClient(handler));

        var result = await service.TryExecuteAsync(SnippetMediaCommand.NextTrack);

        Assert.True(result.Succeeded);
        Assert.Collection(
            handler.Requests,
            first =>
            {
                Assert.Equal("https://api.spotify.com/v1/me/player/next", first.Url);
                Assert.Equal("Bearer old-access", first.Authorization);
            },
            refresh => Assert.Equal("https://accounts.spotify.com/api/token", refresh.Url),
            retry =>
            {
                Assert.Equal("https://api.spotify.com/v1/me/player/next", retry.Url);
                Assert.Equal("Bearer new-access", retry.Authorization);
            });
    }

    [Fact]
    public async Task ToggleShuffleReadsPlaybackStateAndSendsOppositeState()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"is_playing":true,"shuffle_state":false,"repeat_state":"off"}""");
        handler.Enqueue(HttpStatusCode.NoContent);
        var service = new SpotifyMediaActionGatewayAdapter(services.SettingsRepository, new HttpClient(handler));

        var result = await service.TryExecuteAsync(SnippetMediaCommand.ToggleShuffle);

        Assert.True(result.Succeeded);
        Assert.Collection(
            handler.Requests,
            playback => Assert.Equal("https://api.spotify.com/v1/me/player", playback.Url),
            shuffle => Assert.Equal("https://api.spotify.com/v1/me/player/shuffle?state=true", shuffle.Url));
    }

    [Fact]
    public async Task ForbiddenCommandReturnsPremiumMessage()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Forbidden);
        var service = new SpotifyMediaActionGatewayAdapter(services.SettingsRepository, new HttpClient(handler));

        var result = await service.TryExecuteAsync(SnippetMediaCommand.NextTrack);

        Assert.False(result.Succeeded);
        Assert.Equal("Spotify Premium 계정 또는 권한이 필요합니다.", result.ErrorMessage);
    }

    [Fact]
    public async Task OpenSpotifyAndResumeStopsWhenSpotifyAppLaunchFails()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        var appLaunchService = new StubSpotifyAppLaunchGatewayAdapter { Result = false };
        var service = CreateSpotifyMediaActionGatewayAdapter(services, handler, appLaunchService);

        var result = await service.TryExecuteAsync(SnippetMediaCommand.OpenSpotifyAndResume);

        Assert.False(result.Succeeded);
        Assert.Equal("Spotify 앱을 열지 못했습니다. PC에 Spotify 앱이 설치되어 있는지 확인해 주세요.", result.ErrorMessage);
        Assert.Equal(1, appLaunchService.CallCount);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OpenSpotifyAndResumePollsDevicesAndTransfersInactiveComputerDevice()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"devices":[]}""");
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"devices":[{"id":"computer-1","type":"Computer","is_active":false,"is_restricted":false}]}""");
        handler.Enqueue(HttpStatusCode.NoContent);
        var appLaunchService = new StubSpotifyAppLaunchGatewayAdapter();
        var service = CreateSpotifyMediaActionGatewayAdapter(services, handler, appLaunchService, attempts: 2);

        var result = await service.TryExecuteAsync(SnippetMediaCommand.OpenSpotifyAndResume);

        Assert.True(result.Succeeded);
        Assert.Equal(1, appLaunchService.CallCount);
        Assert.Collection(
            handler.Requests,
            firstDevices => Assert.Equal("https://api.spotify.com/v1/me/player/devices", firstDevices.Url),
            secondDevices => Assert.Equal("https://api.spotify.com/v1/me/player/devices", secondDevices.Url),
            transfer =>
            {
                Assert.Equal("PUT", transfer.Method);
                Assert.Equal("https://api.spotify.com/v1/me/player", transfer.Url);
                Assert.Contains("\"device_ids\":[\"computer-1\"]", transfer.Content);
                Assert.Contains("\"play\":true", transfer.Content);
            });
    }

    [Fact]
    public async Task OpenSpotifyAndResumePrefersComputerDeviceOverActivePhone()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"devices":[
                {"id":"phone-1","type":"Smartphone","is_active":true,"is_restricted":false},
                {"id":"computer-1","type":"Computer","is_active":false,"is_restricted":false}
            ]}
            """);
        handler.Enqueue(HttpStatusCode.NoContent);
        var service = CreateSpotifyMediaActionGatewayAdapter(services, handler, new StubSpotifyAppLaunchGatewayAdapter());

        var result = await service.TryExecuteAsync(SnippetMediaCommand.OpenSpotifyAndResume);

        Assert.True(result.Succeeded);
        Assert.Equal("https://api.spotify.com/v1/me/player", handler.Requests[1].Url);
        Assert.Contains("\"device_ids\":[\"computer-1\"]", handler.Requests[1].Content);
    }

    [Fact]
    public async Task OpenSpotifyAndResumePlaysActiveComputerDevice()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"devices":[{"id":"computer-1","type":"Computer","is_active":true,"is_restricted":false}]}""");
        handler.Enqueue(HttpStatusCode.NoContent);
        var service = CreateSpotifyMediaActionGatewayAdapter(services, handler, new StubSpotifyAppLaunchGatewayAdapter());

        var result = await service.TryExecuteAsync(SnippetMediaCommand.OpenSpotifyAndResume);

        Assert.True(result.Succeeded);
        Assert.Collection(
            handler.Requests,
            devices => Assert.Equal("https://api.spotify.com/v1/me/player/devices", devices.Url),
            play =>
            {
                Assert.Equal("PUT", play.Method);
                Assert.Equal("https://api.spotify.com/v1/me/player/play?device_id=computer-1", play.Url);
                Assert.Equal(string.Empty, play.Content);
            });
    }

    [Fact]
    public async Task OpenSpotifyAndResumeReturnsFriendlyMessageWhenNoDeviceAppears()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"devices":[]}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"devices":[]}""");
        var service = CreateSpotifyMediaActionGatewayAdapter(
            services,
            handler,
            new StubSpotifyAppLaunchGatewayAdapter(),
            attempts: 2);

        var result = await service.TryExecuteAsync(SnippetMediaCommand.OpenSpotifyAndResume);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "Spotify 앱은 열었지만 재생 장치를 찾지 못했습니다. Spotify 앱에서 로그인 후 음악을 한 번 재생한 뒤 다시 시도해 주세요.",
            result.ErrorMessage);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task OpenSpotifyAndResumeRefreshesTokenAfterUnauthorizedDeviceLookup()
    {
        var services = CreateServices();
        SaveSpotifySettings(services, expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"new-access","expires_in":3600}""");
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """{"devices":[{"id":"computer-1","type":"Computer","is_active":true,"is_restricted":false}]}""");
        handler.Enqueue(HttpStatusCode.NoContent);
        var service = CreateSpotifyMediaActionGatewayAdapter(services, handler, new StubSpotifyAppLaunchGatewayAdapter());

        var result = await service.TryExecuteAsync(SnippetMediaCommand.OpenSpotifyAndResume);

        Assert.True(result.Succeeded);
        Assert.Collection(
            handler.Requests,
            firstDevices =>
            {
                Assert.Equal("https://api.spotify.com/v1/me/player/devices", firstDevices.Url);
                Assert.Equal("Bearer old-access", firstDevices.Authorization);
            },
            refresh => Assert.Equal("https://accounts.spotify.com/api/token", refresh.Url),
            secondDevices =>
            {
                Assert.Equal("https://api.spotify.com/v1/me/player/devices", secondDevices.Url);
                Assert.Equal("Bearer new-access", secondDevices.Authorization);
            },
            play => Assert.Equal("https://api.spotify.com/v1/me/player/play?device_id=computer-1", play.Url));
    }

    private static SpotifyMediaActionGatewayAdapter CreateSpotifyMediaActionGatewayAdapter(
        TestServices services,
        QueueHttpMessageHandler handler,
        ISpotifyAppLaunchGateway appLaunchService,
        int attempts = 10)
    {
        return new SpotifyMediaActionGatewayAdapter(
            services.SettingsRepository,
            new HttpClient(handler),
            appLaunchService,
            TimeSpan.Zero,
            attempts);
    }

    private static void SaveSpotifySettings(TestServices services, DateTimeOffset expiresAt)
    {
        var settings = services.SettingsRepository.Load();
        settings.SpotifyClientId = "client-id";
        settings.SpotifyAccessToken = "old-access";
        settings.SpotifyRefreshToken = "refresh-token";
        settings.SpotifyTokenExpiresAt = expiresAt;
        services.SettingsRepository.Save(settings);
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = [];

        public List<RecordedHttpRequest> Requests { get; } = [];

        public void Enqueue(HttpStatusCode statusCode)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode));
        }

        public void EnqueueJson(HttpStatusCode statusCode, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedHttpRequest(
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                content));

            return _responses.Dequeue();
        }
    }

    private sealed record RecordedHttpRequest(
        string Method,
        string Url,
        string Authorization,
        string Content);

    private sealed class StubSpotifyAppLaunchGatewayAdapter : ISpotifyAppLaunchGateway
    {
        public bool Result { get; init; } = true;

        public int CallCount { get; private set; }

        public bool TryLaunch()
        {
            CallCount++;

            return Result;
        }
    }
}

