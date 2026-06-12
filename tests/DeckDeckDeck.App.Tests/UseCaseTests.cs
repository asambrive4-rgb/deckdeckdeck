using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class UseCaseTests
{
    [Fact]
    public void SaveSnippetUseCaseSavesSnippetAndRequestsBackup()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new SaveSnippetUseCase(
            services.SnippetService,
            services.SettingsService,
            autoBackup);

        var result = useCase.Execute(new SaveSnippetRequest(
            category.Id,
            SlotKey.Numpad3,
            SnippetId: null,
            "Paste",
            "Hello",
            Description: null,
            ImagePath: null,
            ThumbnailPath: null,
            SnippetActionType.PasteText,
            LaunchPath: string.Empty,
            SlotImageMode.Auto,
            AutoIcon: null,
            LaunchUrl: null,
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause,
            IsSlotEnabled: true,
            OriginalIsSlotEnabled: true));

        Assert.True(result.Succeeded);
        Assert.Equal("Paste", result.Snippet!.Title);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCaseReturnsLaunchFailureWithoutCallingPaste()
    {
        var pasteService = new RecordingClipboardPasteService();
        var launchService = new RecordingFileLaunchService { Result = false };
        var useCase = new ExecuteSnippetActionUseCase(
            pasteService,
            launchService,
            new RecordingUrlLaunchService(),
            new RecordingMediaActionService(),
            new TestSpotifyMediaActionGateway());

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "Missing file",
                ActionType = SnippetActionType.LaunchFile,
                LaunchPath = @"C:\missing.exe"
            },
            new AppSettings(),
            new IntPtr(123)));

        Assert.False(result.Succeeded);
        Assert.Contains("실행 실패", result.StatusMessage);
        Assert.Empty(pasteService.Calls);
    }

    private sealed class TestSpotifyMediaActionGateway : ISpotifyMediaActionGateway
    {
        public Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(
            SnippetMediaCommand command,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpotifyMediaActionGatewayResult(true));
        }
    }
}
