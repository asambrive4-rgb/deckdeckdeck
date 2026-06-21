using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class PasteFileUseCaseTests
{
    [Fact]
    public async Task PasteFileRequestsGatewayAndReturnsUserStatus()
    {
        var gateway = new RecordingFilePasteGateway();
        var useCase = new PasteFileUseCase(gateway);

        var result = await useCase.ExecuteAsync(new PasteFileRequest(
            Guid.NewGuid(),
            "메모",
            @"C:\notes\memo.md",
            new IntPtr(123),
            new AppSettings()));

        Assert.True(result.Succeeded);
        Assert.Equal("메모 파일 붙여넣기 요청됨", result.StatusMessage);
        var call = Assert.Single(gateway.Calls);
        Assert.Equal(@"C:\notes\memo.md", call.FilePath);
        Assert.Equal(new IntPtr(123), call.TargetWindowHandle);
    }

    [Fact]
    public async Task PasteFileRejectsMissingTargetWindowBeforeCallingGateway()
    {
        var gateway = new RecordingFilePasteGateway();
        var useCase = new PasteFileUseCase(gateway);

        var result = await useCase.ExecuteAsync(new PasteFileRequest(
            Guid.NewGuid(),
            "메모",
            @"C:\notes\memo.md",
            IntPtr.Zero,
            new AppSettings()));

        Assert.False(result.Succeeded);
        Assert.Contains("대상 창을 찾지 못했습니다", result.StatusMessage);
        Assert.Empty(gateway.Calls);
    }

    [Fact]
    public async Task PasteFileReportsMissingFileFromGateway()
    {
        var gateway = new RecordingFilePasteGateway
        {
            Result = FilePasteGatewayResult.FileNotFound()
        };
        var useCase = new PasteFileUseCase(gateway);

        var result = await useCase.ExecuteAsync(new PasteFileRequest(
            Guid.NewGuid(),
            "메모",
            @"C:\missing.md",
            new IntPtr(123),
            new AppSettings()));

        Assert.False(result.Succeeded);
        Assert.Contains("붙여넣을 파일을 찾지 못했습니다", result.StatusMessage);
        Assert.Contains("File paste failed", result.LogMessage);
    }
}
