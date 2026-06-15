using DeckDeckDeck.App.Composition;
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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public enum SpotifyConnectionCheckState
{
    Connected,
    Disconnected,
    Unknown
}

public sealed record SpotifyConnectionCheckResult(
    SpotifyConnectionCheckState State,
    string? ErrorMessage = null);

public sealed class SpotifyConnectionGatewayAdapter : ISpotifyConnectionGateway
{
    private const string AuthorizeEndpoint = "https://accounts.spotify.com/authorize";
    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    private const string DevicesEndpoint = "https://api.spotify.com/v1/me/player/devices";
    private const string Scopes = "user-read-playback-state user-modify-playback-state";

    private readonly HttpClient _httpClient;
    private readonly ISpotifyAuthorizationCallbackListener _callbackListener;
    private readonly SettingsRepository _settingsService;
    private readonly IUrlLaunchGateway _urlLaunchService;

    public SpotifyConnectionGatewayAdapter(
        SettingsRepository settingsService,
        IUrlLaunchGateway urlLaunchService,
        HttpClient? httpClient = null)
        : this(settingsService, urlLaunchService, httpClient, callbackListener: null)
    {
    }

    internal SpotifyConnectionGatewayAdapter(
        SettingsRepository settingsService,
        IUrlLaunchGateway urlLaunchService,
        HttpClient? httpClient,
        ISpotifyAuthorizationCallbackListener? callbackListener)
    {
        _settingsService = settingsService;
        _urlLaunchService = urlLaunchService;
        _httpClient = httpClient ?? new HttpClient();
        _callbackListener = callbackListener ?? new SpotifyAuthorizationCallbackListener();
    }

    public string DashboardUrl => "https://developer.spotify.com/dashboard";

    public string RedirectUri => "http://127.0.0.1:53682/spotify-callback/";

    public async Task<SpotifyConnectionGatewayResult> ConnectAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new SpotifyConnectionGatewayResult(false, "Spotify Client ID를 입력해 주세요.");
        }

        try
        {
            var trimmedClientId = clientId.Trim();
            var redirectUri = new Uri(RedirectUri);
            var state = CreateRandomBase64UrlString(32);
            var codeVerifier = CreateRandomBase64UrlString(64);
            var codeChallenge = CreateCodeChallenge(codeVerifier);
            var authorizeUrl = BuildAuthorizeUrl(trimmedClientId, redirectUri, state, codeChallenge);
            using var callbackCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var callbackTask = _callbackListener.WaitForCallbackAsync(redirectUri, state, callbackCancellation.Token);

            if (!await TryLaunchAuthorizeUrlAsync(authorizeUrl, callbackCancellation, callbackTask))
            {
                return new SpotifyConnectionGatewayResult(false, "Spotify 로그인 페이지를 열지 못했습니다.");
            }

            var callback = await callbackTask;
            if (!callback.Succeeded || string.IsNullOrWhiteSpace(callback.Code))
            {
                return new SpotifyConnectionGatewayResult(false, callback.ErrorMessage ?? "Spotify 인증에 실패했습니다.");
            }

            var token = await ExchangeCodeAsync(trimmedClientId, redirectUri, callback.Code, codeVerifier, cancellationToken);
            if (token is null)
            {
                return new SpotifyConnectionGatewayResult(false, "Spotify 토큰을 받지 못했습니다.");
            }

            var settings = _settingsService.Load();
            settings.SpotifyClientId = trimmedClientId;
            settings.SpotifyAccessToken = token.AccessToken;
            settings.SpotifyRefreshToken = token.RefreshToken;
            settings.SpotifyTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            settings.SpotifyConnectedUserDisplayName = "Spotify 계정";
            _settingsService.Save(settings);

            return new SpotifyConnectionGatewayResult(true);
        }
        catch (HttpRequestException)
        {
            return new SpotifyConnectionGatewayResult(false, "Spotify 서버에 연결하지 못했습니다. 네트워크를 확인해 주세요.");
        }
        catch (JsonException)
        {
            return new SpotifyConnectionGatewayResult(false, "Spotify 응답을 읽지 못했습니다.");
        }
        catch (TaskCanceledException)
        {
            return new SpotifyConnectionGatewayResult(false, "Spotify 연결 시간이 초과되었습니다.");
        }
    }

    public void Disconnect()
    {
        var settings = _settingsService.Load();
        settings.SpotifyClientId = string.Empty;
        settings.SpotifyAccessToken = string.Empty;
        settings.SpotifyRefreshToken = string.Empty;
        settings.SpotifyTokenExpiresAt = null;
        settings.SpotifyConnectedUserDisplayName = string.Empty;
        _settingsService.Save(settings);
    }

    public async Task<SpotifyConnectionCheckResult> CheckConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Load();
        if (!HasStoredConnection(settings))
        {
            return new SpotifyConnectionCheckResult(SpotifyConnectionCheckState.Disconnected);
        }

        try
        {
            var accessToken = settings.SpotifyAccessToken;
            if (settings.SpotifyTokenExpiresAt is null
                || settings.SpotifyTokenExpiresAt.Value <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                var refreshResult = await RefreshAccessTokenAsync(settings, cancellationToken);
                if (!refreshResult.Succeeded)
                {
                    return new SpotifyConnectionCheckResult(
                        SpotifyConnectionCheckState.Disconnected,
                        refreshResult.ErrorMessage);
                }

                accessToken = refreshResult.AccessToken!;
            }

            var statusCode = await CheckDevicesEndpointAsync(accessToken, cancellationToken);
            if (statusCode is HttpStatusCode.Unauthorized)
            {
                var refreshResult = await RefreshAccessTokenAsync(settings, cancellationToken);
                if (!refreshResult.Succeeded)
                {
                    return new SpotifyConnectionCheckResult(
                        SpotifyConnectionCheckState.Disconnected,
                        refreshResult.ErrorMessage);
                }

                statusCode = await CheckDevicesEndpointAsync(refreshResult.AccessToken!, cancellationToken);
            }

            return statusCode switch
            {
                HttpStatusCode.OK => new SpotifyConnectionCheckResult(SpotifyConnectionCheckState.Connected),
                HttpStatusCode.Forbidden => new SpotifyConnectionCheckResult(
                    SpotifyConnectionCheckState.Disconnected,
                    "Spotify 권한을 다시 허용해야 합니다. 다시 연결해 주세요."),
                HttpStatusCode.Unauthorized => new SpotifyConnectionCheckResult(
                    SpotifyConnectionCheckState.Disconnected,
                    "Spotify 인증이 만료되었습니다. 다시 연결해 주세요."),
                HttpStatusCode.TooManyRequests => new SpotifyConnectionCheckResult(
                    SpotifyConnectionCheckState.Unknown,
                    "Spotify 요청이 너무 많습니다. 잠시 후 다시 확인해 주세요."),
                _ => new SpotifyConnectionCheckResult(
                    SpotifyConnectionCheckState.Unknown,
                    "Spotify 연결 상태를 확인하지 못했습니다. 잠시 후 다시 시도해 주세요.")
            };
        }
        catch (HttpRequestException)
        {
            return new SpotifyConnectionCheckResult(
                SpotifyConnectionCheckState.Unknown,
                "Spotify 서버에 연결하지 못했습니다. 네트워크를 확인해 주세요.");
        }
        catch (TaskCanceledException)
        {
            return new SpotifyConnectionCheckResult(
                SpotifyConnectionCheckState.Unknown,
                "Spotify 연결 상태 확인 시간이 초과되었습니다.");
        }
    }

    private async Task<bool> TryLaunchAuthorizeUrlAsync(
        string authorizeUrl,
        CancellationTokenSource callbackCancellation,
        Task<SpotifyAuthorizationCallbackResult> callbackTask)
    {
        try
        {
            if (_urlLaunchService.TryLaunch(authorizeUrl))
            {
                return true;
            }
        }
        catch (Exception) when (!callbackTask.IsCompleted)
        {
        }
        catch (Exception)
        {
        }

        await CancelCallbackAsync(callbackCancellation, callbackTask);
        return false;
    }

    private static async Task CancelCallbackAsync(
        CancellationTokenSource callbackCancellation,
        Task<SpotifyAuthorizationCallbackResult> callbackTask)
    {
        await callbackCancellation.CancelAsync();
        try
        {
            await callbackTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string BuildAuthorizeUrl(
        string clientId,
        Uri redirectUri,
        string state,
        string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["scope"] = Scopes,
            ["redirect_uri"] = redirectUri.ToString(),
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = codeChallenge,
            ["state"] = state
        };

        return $"{AuthorizeEndpoint}?{BuildQuery(query)}";
    }

    private async Task<SpotifyTokenResponse?> ExchangeCodeAsync(
        string clientId,
        Uri redirectUri,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri.ToString(),
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier
        });
        using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return SpotifyTokenResponse.FromJson(document.RootElement);
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

    private async Task<HttpStatusCode> CheckDevicesEndpointAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DevicesEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        return response.StatusCode;
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));

        return Base64UrlEncode(bytes);
    }

    private static string CreateRandomBase64UrlString(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);

        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildQuery(IReadOnlyDictionary<string, string> values)
    {
        return string.Join(
            "&",
            values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static bool HasStoredConnection(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.SpotifyClientId)
            && !string.IsNullOrWhiteSpace(settings.SpotifyAccessToken)
            && !string.IsNullOrWhiteSpace(settings.SpotifyRefreshToken);
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

    private sealed record SpotifyTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn)
    {
        public static SpotifyTokenResponse? FromJson(JsonElement element)
        {
            if (!element.TryGetProperty("access_token", out var accessTokenElement)
                || !element.TryGetProperty("refresh_token", out var refreshTokenElement))
            {
                return null;
            }

            var accessToken = accessTokenElement.GetString();
            var refreshToken = refreshTokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            var expiresIn = element.TryGetProperty("expires_in", out var expiresInElement)
                && expiresInElement.TryGetInt32(out var parsedExpiresIn)
                    ? parsedExpiresIn
                    : 3600;

            return new SpotifyTokenResponse(accessToken, refreshToken, expiresIn);
        }
    }
}

