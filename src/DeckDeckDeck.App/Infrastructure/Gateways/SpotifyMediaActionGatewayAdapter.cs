using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class SpotifyMediaActionGatewayAdapter : ISpotifyMediaActionGateway
{
    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    private const string PlayerEndpoint = "https://api.spotify.com/v1/me/player";
    private const string DevicesEndpoint = "https://api.spotify.com/v1/me/player/devices";
    private readonly HttpClient _httpClient;
    private readonly ISpotifyAppLaunchGateway _spotifyAppLaunchService;
    private readonly TimeSpan _devicePollingDelay;
    private readonly int _devicePollingAttempts;
    private readonly SettingsRepository _settingsService;

    public SpotifyMediaActionGatewayAdapter(
        SettingsRepository settingsService,
        HttpClient? httpClient = null,
        ISpotifyAppLaunchGateway? spotifyAppLaunchService = null,
        TimeSpan? devicePollingDelay = null,
        int devicePollingAttempts = 10)
    {
        _settingsService = settingsService;
        _httpClient = httpClient ?? new HttpClient();
        _spotifyAppLaunchService = spotifyAppLaunchService ?? new SpotifyAppLaunchGatewayAdapter();
        _devicePollingDelay = devicePollingDelay ?? TimeSpan.FromMilliseconds(500);
        _devicePollingAttempts = devicePollingAttempts;
    }

    public async Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(
        SnippetMediaCommand command,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Load();
        if (!IsConnected(settings))
        {
            return new SpotifyMediaActionGatewayResult(false, "Spotify를 다시 연결해 주세요.");
        }

        var tokenResult = await GetValidAccessTokenAsync(settings, cancellationToken);
        if (!tokenResult.Succeeded)
        {
            return new SpotifyMediaActionGatewayResult(false, tokenResult.ErrorMessage);
        }

        var result = await ExecuteCommandAsync(command, tokenResult.AccessToken!, cancellationToken);
        if (result.StatusCode is HttpStatusCode.Unauthorized)
        {
            var refreshed = await RefreshAccessTokenAsync(settings, cancellationToken);
            if (!refreshed.Succeeded)
            {
                return new SpotifyMediaActionGatewayResult(false, refreshed.ErrorMessage);
            }

            result = await ExecuteCommandAsync(command, refreshed.AccessToken!, cancellationToken);
        }

        return result.ToMediaActionResult();
    }

    private static bool IsConnected(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.SpotifyClientId)
            && !string.IsNullOrWhiteSpace(settings.SpotifyAccessToken)
            && !string.IsNullOrWhiteSpace(settings.SpotifyRefreshToken);
    }

    private async Task<SpotifyAccessTokenResult> GetValidAccessTokenAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.SpotifyTokenExpiresAt is not null
            && settings.SpotifyTokenExpiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return SpotifyAccessTokenResult.Success(settings.SpotifyAccessToken);
        }

        return await RefreshAccessTokenAsync(settings, cancellationToken);
    }

    private async Task<SpotifyAccessTokenResult> RefreshAccessTokenAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.SpotifyRefreshToken)
            || string.IsNullOrWhiteSpace(settings.SpotifyClientId))
        {
            return SpotifyAccessTokenResult.Failure("Spotify를 다시 연결해 주세요.");
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = settings.SpotifyRefreshToken,
                ["client_id"] = settings.SpotifyClientId
            });
            using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return SpotifyAccessTokenResult.Failure("Spotify 인증이 만료되었습니다. 다시 연결해 주세요.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            {
                return SpotifyAccessTokenResult.Failure("Spotify 토큰 갱신 응답을 읽지 못했습니다.");
            }

            var accessToken = accessTokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return SpotifyAccessTokenResult.Failure("Spotify 토큰 갱신 응답을 읽지 못했습니다.");
            }

            settings.SpotifyAccessToken = accessToken;
            if (document.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement)
                && !string.IsNullOrWhiteSpace(refreshTokenElement.GetString()))
            {
                settings.SpotifyRefreshToken = refreshTokenElement.GetString()!;
            }

            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresInElement)
                && expiresInElement.TryGetInt32(out var parsedExpiresIn)
                    ? parsedExpiresIn
                    : 3600;
            settings.SpotifyTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            _settingsService.Save(settings);

            return SpotifyAccessTokenResult.Success(accessToken);
        }
        catch (HttpRequestException)
        {
            return SpotifyAccessTokenResult.Failure("Spotify 서버에 연결하지 못했습니다. 네트워크를 확인해 주세요.");
        }
        catch (JsonException)
        {
            return SpotifyAccessTokenResult.Failure("Spotify 토큰 갱신 응답을 읽지 못했습니다.");
        }
        catch (TaskCanceledException)
        {
            return SpotifyAccessTokenResult.Failure("Spotify 요청 시간이 초과되었습니다.");
        }
    }

    private async Task<SpotifyCommandHttpResult> ExecuteCommandAsync(
        SnippetMediaCommand command,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            return command switch
            {
                SnippetMediaCommand.PlayPause => await ExecutePlayPauseAsync(accessToken, cancellationToken),
                SnippetMediaCommand.PreviousTrack => await SendCommandAsync(HttpMethod.Post, $"{PlayerEndpoint}/previous", accessToken, cancellationToken),
                SnippetMediaCommand.NextTrack => await SendCommandAsync(HttpMethod.Post, $"{PlayerEndpoint}/next", accessToken, cancellationToken),
                SnippetMediaCommand.ToggleShuffle => await ExecuteToggleShuffleAsync(accessToken, cancellationToken),
                SnippetMediaCommand.CycleRepeat => await ExecuteCycleRepeatAsync(accessToken, cancellationToken),
                SnippetMediaCommand.OpenSpotifyAndResume => await ExecuteOpenSpotifyAndResumeAsync(accessToken, cancellationToken),
                _ => SpotifyCommandHttpResult.Failure("Spotify에서 지원하지 않는 미디어 명령입니다.")
            };
        }
        catch (HttpRequestException)
        {
            return SpotifyCommandHttpResult.Failure("Spotify 서버에 연결하지 못했습니다. 네트워크를 확인해 주세요.");
        }
        catch (JsonException)
        {
            return SpotifyCommandHttpResult.Failure("Spotify 재생 상태를 읽지 못했습니다.");
        }
        catch (TaskCanceledException)
        {
            return SpotifyCommandHttpResult.Failure("Spotify 요청 시간이 초과되었습니다.");
        }
    }

    private async Task<SpotifyCommandHttpResult> ExecutePlayPauseAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var playbackState = await GetPlaybackStateAsync(accessToken, cancellationToken);
        if (!playbackState.Succeeded)
        {
            return playbackState.ToCommandResult();
        }

        var path = playbackState.State!.IsPlaying ? "pause" : "play";

        return await SendCommandAsync(HttpMethod.Put, $"{PlayerEndpoint}/{path}", accessToken, cancellationToken);
    }

    private async Task<SpotifyCommandHttpResult> ExecuteToggleShuffleAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var playbackState = await GetPlaybackStateAsync(accessToken, cancellationToken);
        if (!playbackState.Succeeded)
        {
            return playbackState.ToCommandResult();
        }

        var nextState = !playbackState.State!.ShuffleState;

        return await SendCommandAsync(
            HttpMethod.Put,
            $"{PlayerEndpoint}/shuffle?state={nextState.ToString().ToLowerInvariant()}",
            accessToken,
            cancellationToken);
    }

    private async Task<SpotifyCommandHttpResult> ExecuteCycleRepeatAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var playbackState = await GetPlaybackStateAsync(accessToken, cancellationToken);
        if (!playbackState.Succeeded)
        {
            return playbackState.ToCommandResult();
        }

        var nextState = playbackState.State!.RepeatState switch
        {
            "off" => "context",
            "context" => "track",
            _ => "off"
        };

        return await SendCommandAsync(
            HttpMethod.Put,
            $"{PlayerEndpoint}/repeat?state={nextState}",
            accessToken,
            cancellationToken);
    }

    private async Task<SpotifyCommandHttpResult> ExecuteOpenSpotifyAndResumeAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (!_spotifyAppLaunchService.TryLaunch())
        {
            return SpotifyCommandHttpResult.Failure("Spotify 앱을 열지 못했습니다. PC에 Spotify 앱이 설치되어 있는지 확인해 주세요.");
        }

        var deviceResult = await WaitForPreferredDeviceAsync(accessToken, cancellationToken);
        if (!deviceResult.Succeeded)
        {
            return deviceResult.ToCommandResult();
        }

        var device = deviceResult.Device!;
        return device.IsActive
            ? await SendCommandAsync(
                HttpMethod.Put,
                $"{PlayerEndpoint}/play?device_id={Uri.EscapeDataString(device.Id)}",
                accessToken,
                cancellationToken)
            : await TransferPlaybackAndPlayAsync(device.Id, accessToken, cancellationToken);
    }

    private async Task<SpotifyDeviceResult> WaitForPreferredDeviceAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < _devicePollingAttempts; attempt++)
        {
            var devicesResult = await GetAvailableDevicesAsync(accessToken, cancellationToken);
            if (!devicesResult.Succeeded)
            {
                return SpotifyDeviceResult.Failure(
                    devicesResult.ErrorMessage ?? "Spotify 재생 장치를 읽지 못했습니다.",
                    devicesResult.StatusCode);
            }

            var preferredDevice = SelectPreferredDevice(devicesResult.Devices);
            if (preferredDevice is not null)
            {
                return SpotifyDeviceResult.Success(preferredDevice, devicesResult.StatusCode);
            }

            if (attempt < _devicePollingAttempts - 1 && _devicePollingDelay > TimeSpan.Zero)
            {
                await Task.Delay(_devicePollingDelay, cancellationToken);
            }
        }

        return SpotifyDeviceResult.Failure(
            "Spotify 앱은 열었지만 재생 장치를 찾지 못했습니다. Spotify 앱에서 로그인 후 음악을 한 번 재생한 뒤 다시 시도해 주세요.",
            HttpStatusCode.NotFound);
    }

    private async Task<SpotifyDevicesResult> GetAvailableDevicesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, DevicesEndpoint, accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return SpotifyDevicesResult.Failure(GetFriendlyApiError(response.StatusCode), response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("devices", out var devicesElement)
            || devicesElement.ValueKind != JsonValueKind.Array)
        {
            return SpotifyDevicesResult.Success([], response.StatusCode);
        }

        var devices = new List<SpotifyDevice>();
        foreach (var deviceElement in devicesElement.EnumerateArray())
        {
            var id = deviceElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var type = deviceElement.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;
            var isActive = deviceElement.TryGetProperty("is_active", out var activeElement)
                && activeElement.ValueKind == JsonValueKind.True;
            var isRestricted = deviceElement.TryGetProperty("is_restricted", out var restrictedElement)
                && restrictedElement.ValueKind == JsonValueKind.True;

            devices.Add(new SpotifyDevice(id, type, isActive, isRestricted));
        }

        return SpotifyDevicesResult.Success(devices, response.StatusCode);
    }

    private async Task<SpotifyCommandHttpResult> TransferPlaybackAndPlayAsync(
        string deviceId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            device_ids = new[] { deviceId },
            play = true
        });
        using var request = CreateRequest(HttpMethod.Put, PlayerEndpoint, accessToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? SpotifyCommandHttpResult.Success(response.StatusCode)
            : SpotifyCommandHttpResult.Failure(GetFriendlyApiError(response.StatusCode), response.StatusCode);
    }

    private static SpotifyDevice? SelectPreferredDevice(IReadOnlyList<SpotifyDevice> devices)
    {
        return devices
            .Where(device => !device.IsRestricted)
            .OrderBy(GetDevicePriority)
            .FirstOrDefault();
    }

    private static int GetDevicePriority(SpotifyDevice device)
    {
        var isComputer = device.Type.Equals("computer", StringComparison.OrdinalIgnoreCase);
        return (isComputer, device.IsActive) switch
        {
            (true, true) => 0,
            (true, false) => 1,
            (false, true) => 2,
            _ => 3
        };
    }

    private async Task<SpotifyPlaybackStateResult> GetPlaybackStateAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, PlayerEndpoint, accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return SpotifyPlaybackStateResult.Failure(
                "활성 Spotify 재생 장치를 찾지 못했습니다.",
                response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return SpotifyPlaybackStateResult.Failure(GetFriendlyApiError(response.StatusCode), response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var isPlaying = root.TryGetProperty("is_playing", out var isPlayingElement)
            && isPlayingElement.ValueKind == JsonValueKind.True;
        var shuffleState = root.TryGetProperty("shuffle_state", out var shuffleElement)
            && shuffleElement.ValueKind == JsonValueKind.True;
        var repeatState = root.TryGetProperty("repeat_state", out var repeatElement)
            ? repeatElement.GetString() ?? "off"
            : "off";

        return SpotifyPlaybackStateResult.Success(
            new SpotifyPlaybackState(isPlaying, shuffleState, repeatState),
            response.StatusCode);
    }

    private async Task<SpotifyCommandHttpResult> SendCommandAsync(
        HttpMethod method,
        string url,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, url, accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? SpotifyCommandHttpResult.Success(response.StatusCode)
            : SpotifyCommandHttpResult.Failure(GetFriendlyApiError(response.StatusCode), response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    private static string GetFriendlyApiError(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "Spotify 인증이 만료되었습니다. 다시 연결해 주세요.",
            HttpStatusCode.Forbidden => "Spotify Premium 계정 또는 권한이 필요합니다.",
            HttpStatusCode.NotFound => "활성 Spotify 재생 장치를 찾지 못했습니다.",
            HttpStatusCode.TooManyRequests => "Spotify 요청이 너무 많습니다. 잠시 후 다시 시도해 주세요.",
            _ => $"Spotify 명령을 실행하지 못했습니다. (HTTP {(int)statusCode})"
        };
    }

    private sealed record SpotifyAccessTokenResult(
        bool Succeeded,
        string? AccessToken = null,
        string? ErrorMessage = null)
    {
        public static SpotifyAccessTokenResult Success(string accessToken)
        {
            return new SpotifyAccessTokenResult(true, accessToken);
        }

        public static SpotifyAccessTokenResult Failure(string errorMessage)
        {
            return new SpotifyAccessTokenResult(false, ErrorMessage: errorMessage);
        }
    }

    private sealed record SpotifyPlaybackState(bool IsPlaying, bool ShuffleState, string RepeatState);

    private sealed record SpotifyDevice(
        string Id,
        string Type,
        bool IsActive,
        bool IsRestricted);

    private sealed record SpotifyDevicesResult(
        bool Succeeded,
        IReadOnlyList<SpotifyDevice> Devices,
        string? ErrorMessage,
        HttpStatusCode? StatusCode)
    {
        public static SpotifyDevicesResult Success(IReadOnlyList<SpotifyDevice> devices, HttpStatusCode statusCode)
        {
            return new SpotifyDevicesResult(true, devices, null, statusCode);
        }

        public static SpotifyDevicesResult Failure(string errorMessage, HttpStatusCode? statusCode = null)
        {
            return new SpotifyDevicesResult(false, [], errorMessage, statusCode);
        }
    }

    private sealed record SpotifyDeviceResult(
        bool Succeeded,
        SpotifyDevice? Device,
        string? ErrorMessage,
        HttpStatusCode? StatusCode)
    {
        public static SpotifyDeviceResult Success(SpotifyDevice device, HttpStatusCode? statusCode)
        {
            return new SpotifyDeviceResult(true, device, null, statusCode);
        }

        public static SpotifyDeviceResult Failure(string errorMessage, HttpStatusCode? statusCode = null)
        {
            return new SpotifyDeviceResult(false, null, errorMessage, statusCode);
        }

        public SpotifyCommandHttpResult ToCommandResult()
        {
            return Succeeded
                ? SpotifyCommandHttpResult.Success(StatusCode ?? HttpStatusCode.OK)
                : SpotifyCommandHttpResult.Failure(ErrorMessage ?? "Spotify 재생 장치를 찾지 못했습니다.", StatusCode);
        }
    }

    private sealed record SpotifyPlaybackStateResult(
        bool Succeeded,
        SpotifyPlaybackState? State,
        string? ErrorMessage,
        HttpStatusCode? StatusCode)
    {
        public static SpotifyPlaybackStateResult Success(SpotifyPlaybackState state, HttpStatusCode statusCode)
        {
            return new SpotifyPlaybackStateResult(true, state, null, statusCode);
        }

        public static SpotifyPlaybackStateResult Failure(string errorMessage, HttpStatusCode? statusCode = null)
        {
            return new SpotifyPlaybackStateResult(false, null, errorMessage, statusCode);
        }

        public SpotifyCommandHttpResult ToCommandResult()
        {
            return Succeeded
                ? SpotifyCommandHttpResult.Success(StatusCode ?? HttpStatusCode.OK)
                : SpotifyCommandHttpResult.Failure(ErrorMessage ?? "Spotify 재생 상태를 읽지 못했습니다.", StatusCode);
        }
    }

    private sealed record SpotifyCommandHttpResult(
        bool Succeeded,
        string? ErrorMessage,
        HttpStatusCode? StatusCode)
    {
        public static SpotifyCommandHttpResult Success(HttpStatusCode statusCode)
        {
            return new SpotifyCommandHttpResult(true, null, statusCode);
        }

        public static SpotifyCommandHttpResult Failure(string errorMessage, HttpStatusCode? statusCode = null)
        {
            return new SpotifyCommandHttpResult(false, errorMessage, statusCode);
        }

        public SpotifyMediaActionGatewayResult ToMediaActionResult()
        {
            return Succeeded
                ? new SpotifyMediaActionGatewayResult(true)
                : new SpotifyMediaActionGatewayResult(false, ErrorMessage);
        }
    }
}

