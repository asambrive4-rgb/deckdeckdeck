using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class ExecuteSnippetActionFilePasteTests
{
    [Fact]
    public async Task FilePasteRequestsGatewayAndReturnsUserStatus()
    {
        var gateway = new RecordingFilePasteGateway();
        var useCase = CreateUseCase(gateway);
        var settings = new AppSettings();

        var result = await useCase.ExecuteAsync(CreateRequest(
            @"C:\notes\memo.md",
            new IntPtr(123),
            settings));

        Assert.True(result.Succeeded);
        Assert.False(result.ShouldHideWindow);
        Assert.Equal("Paste file 파일 붙여넣기 요청됨", result.StatusMessage);
        var call = Assert.Single(gateway.Calls);
        Assert.Equal(@"C:\notes\memo.md", call.FilePath);
        Assert.Equal(new IntPtr(123), call.TargetWindowHandle);
        Assert.Same(settings, call.Settings);
    }

    [Fact]
    public async Task FilePasteRejectsMissingPathBeforeCallingGateway()
    {
        var gateway = new RecordingFilePasteGateway();
        var useCase = CreateUseCase(gateway);

        var result = await useCase.ExecuteAsync(CreateRequest(" ", new IntPtr(123)));

        Assert.False(result.Succeeded);
        Assert.Contains("File paste failed", result.LogMessage);
        Assert.Empty(gateway.Calls);
    }

    [Fact]
    public async Task FilePasteRejectsMissingTargetWindowBeforeCallingGateway()
    {
        var gateway = new RecordingFilePasteGateway();
        var useCase = CreateUseCase(gateway);

        var result = await useCase.ExecuteAsync(CreateRequest(
            @"C:\notes\memo.md",
            IntPtr.Zero));

        Assert.False(result.Succeeded);
        Assert.Contains("File paste failed", result.LogMessage);
        Assert.Empty(gateway.Calls);
    }

    [Fact]
    public async Task FilePasteReportsMissingFileFromGateway()
    {
        var gateway = new RecordingFilePasteGateway
        {
            Result = FilePasteGatewayResult.FileNotFound()
        };
        var useCase = CreateUseCase(gateway);

        var result = await useCase.ExecuteAsync(CreateRequest(
            @"C:\missing.md",
            new IntPtr(123)));

        Assert.False(result.Succeeded);
        Assert.Contains("File paste failed", result.LogMessage);
    }

    [Fact]
    public async Task FilePasteReportsGatewayFailureAndPreservesException()
    {
        var exception = new InvalidOperationException("File paste failed.");
        var gateway = new RecordingFilePasteGateway
        {
            Result = FilePasteGatewayResult.Failure(exception)
        };
        var useCase = CreateUseCase(gateway);

        var result = await useCase.ExecuteAsync(CreateRequest(
            @"C:\notes\memo.md",
            new IntPtr(123)));

        Assert.False(result.Succeeded);
        Assert.Contains("File paste failed", result.LogMessage);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task FilePasteReportsThrownGatewayException()
    {
        var exception = new InvalidOperationException("Gateway crashed.");
        var useCase = CreateUseCase(new ThrowingFilePasteGateway(exception));

        var result = await useCase.ExecuteAsync(CreateRequest(
            @"C:\notes\memo.md",
            new IntPtr(123)));

        Assert.False(result.Succeeded);
        Assert.Contains("File paste failed", result.LogMessage);
        Assert.Same(exception, result.Exception);
    }

    private static ExecuteSnippetActionUseCase CreateUseCase(IFilePasteGateway filePasteGateway)
    {
        return new ExecuteSnippetActionUseCase(
            new RecordingClipboardPasteGateway(),
            new RecordingFileLaunchGatewayAdapter(),
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new RecordingSpotifyMediaActionGatewayAdapter(),
            new RecordingTerminalCommandGatewayAdapter(),
            filePasteGateway);
    }

    private static ExecuteSnippetActionRequest CreateRequest(
        string? filePath,
        IntPtr targetWindowHandle,
        AppSettings? settings = null)
    {
        return new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "Paste file",
                ActionType = SnippetActionType.LaunchFile,
                FileActionMode = FileActionMode.Paste,
                LaunchPath = filePath
            },
            settings ?? new AppSettings(),
            targetWindowHandle);
    }

    private sealed class ThrowingFilePasteGateway : IFilePasteGateway
    {
        private readonly Exception _exception;

        public ThrowingFilePasteGateway(Exception exception)
        {
            _exception = exception;
        }

        public Task<FilePasteGatewayResult> PasteFileAsync(
            string filePath,
            IntPtr targetWindowHandle,
            AppSettings settings)
        {
            return Task.FromException<FilePasteGatewayResult>(_exception);
        }
    }
}
