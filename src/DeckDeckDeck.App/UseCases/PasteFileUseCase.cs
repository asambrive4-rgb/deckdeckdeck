using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class PasteFileUseCase
{
    private readonly IFilePasteGateway _filePasteGateway;

    public PasteFileUseCase(IFilePasteGateway filePasteGateway)
    {
        _filePasteGateway = filePasteGateway;
    }

    public async Task<PasteFileResult> ExecuteAsync(
        PasteFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return Failure(request, "붙여넣을 파일 경로가 없습니다.");
        }

        if (request.TargetWindowHandle == IntPtr.Zero)
        {
            return Failure(request, "붙여넣을 대상 창을 찾지 못했습니다.");
        }

        try
        {
            var gatewayResult = await _filePasteGateway.PasteFileAsync(
                request.FilePath,
                request.TargetWindowHandle,
                request.Settings);

            return gatewayResult.Status switch
            {
                FilePasteGatewayStatus.Succeeded => PasteFileResult.Success(
                    $"{request.Title} 파일 붙여넣기 요청됨"),
                FilePasteGatewayStatus.FileNotFound => Failure(
                    request,
                    "붙여넣을 파일을 찾지 못했습니다."),
                _ => Failure(
                    request,
                    "파일 붙여넣기를 요청하지 못했습니다.",
                    gatewayResult.Exception)
            };
        }
        catch (Exception ex)
        {
            return Failure(request, "파일 붙여넣기를 요청하지 못했습니다.", ex);
        }
    }

    private static PasteFileResult Failure(
        PasteFileRequest request,
        string message,
        Exception? exception = null)
    {
        return PasteFileResult.Failure(
            statusMessage: $"{request.Title} 파일 붙여넣기 실패: {message}",
            logMessage: $"File paste failed for action {request.ActionId}: {message}",
            exception: exception);
    }
}

public sealed record PasteFileRequest(
    Guid ActionId,
    string Title,
    string? FilePath,
    IntPtr TargetWindowHandle,
    AppSettings Settings);

public sealed record PasteFileResult(
    bool Succeeded,
    string StatusMessage,
    string? LogMessage = null,
    Exception? Exception = null)
{
    public static PasteFileResult Success(string statusMessage)
    {
        return new PasteFileResult(true, statusMessage);
    }

    public static PasteFileResult Failure(
        string statusMessage,
        string logMessage,
        Exception? exception = null)
    {
        return new PasteFileResult(false, statusMessage, logMessage, exception);
    }
}
