using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

internal interface ISpotifyAuthorizationCallbackListener
{
    Task<SpotifyAuthorizationCallbackResult> WaitForCallbackAsync(
        Uri redirectUri,
        string expectedState,
        CancellationToken cancellationToken);
}

internal sealed record SpotifyAuthorizationCallbackResult(
    bool Succeeded,
    string? Code = null,
    string? ErrorMessage = null);

internal sealed class SpotifyAuthorizationCallbackListener : ISpotifyAuthorizationCallbackListener
{
    public async Task<SpotifyAuthorizationCallbackResult> WaitForCallbackAsync(
        Uri redirectUri,
        string expectedState,
        CancellationToken cancellationToken)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, redirectUri.Port);
            listener.Start();

            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken);

            var result = ParseRequestLine(redirectUri, requestLine, expectedState);
            await WriteBrowserResponseAsync(stream, result.Succeeded, cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            return new SpotifyAuthorizationCallbackResult(false, ErrorMessage: "Spotify 연결이 취소되었습니다.");
        }
        catch (SocketException)
        {
            return new SpotifyAuthorizationCallbackResult(
                false,
                ErrorMessage: "Spotify 콜백 포트를 열 수 없습니다. 잠시 후 다시 시도해 주세요.");
        }
        finally
        {
            listener?.Stop();
        }
    }

    private static SpotifyAuthorizationCallbackResult ParseRequestLine(
        Uri redirectUri,
        string? requestLine,
        string expectedState)
    {
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return new SpotifyAuthorizationCallbackResult(false, ErrorMessage: "Spotify 인증 응답을 읽지 못했습니다.");
        }

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return new SpotifyAuthorizationCallbackResult(false, ErrorMessage: "Spotify 인증 응답 형식이 올바르지 않습니다.");
        }

        if (!Uri.TryCreate(redirectUri, parts[1], out var callbackUri))
        {
            return new SpotifyAuthorizationCallbackResult(false, ErrorMessage: "Spotify 인증 주소를 해석하지 못했습니다.");
        }

        var query = ParseQuery(callbackUri.Query);
        if (!query.TryGetValue("state", out var state) || !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            return new SpotifyAuthorizationCallbackResult(false, ErrorMessage: "Spotify 인증 상태값이 일치하지 않습니다.");
        }

        if (query.TryGetValue("error", out var error))
        {
            return new SpotifyAuthorizationCallbackResult(false, ErrorMessage: $"Spotify 인증이 거부되었습니다: {error}");
        }

        return query.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code)
            ? new SpotifyAuthorizationCallbackResult(true, Code: code)
            : new SpotifyAuthorizationCallbackResult(false, ErrorMessage: "Spotify 인증 코드를 받지 못했습니다.");
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmedQuery = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return values;
        }

        foreach (var pair in trimmedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex >= 0 ? pair[..separatorIndex] : pair;
            var value = separatorIndex >= 0 ? pair[(separatorIndex + 1)..] : string.Empty;
            values[WebUtility.UrlDecode(key)] = WebUtility.UrlDecode(value);
        }

        return values;
    }

    private static async Task WriteBrowserResponseAsync(
        Stream stream,
        bool succeeded,
        CancellationToken cancellationToken)
    {
        var title = succeeded ? "Spotify connected" : "Spotify connection failed";
        var body = succeeded
            ? "Spotify connection is complete. You can return to DeckDeckDeck."
            : "Spotify connection failed. Please return to DeckDeckDeck and try again.";
        var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>{title}</title></head><body>{body}</body></html>";
        var payload = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Connection: close\r\n\r\n");

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
    }
}

