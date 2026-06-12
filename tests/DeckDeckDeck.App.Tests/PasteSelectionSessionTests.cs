using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.UseCases;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class PasteSelectionSessionTests
{
    [Fact]
    public void StaleCompletionDoesNotCompleteNewerSelection()
    {
        var session = new PasteSelectionSession();
        var completedCount = 0;

        session.Start();
        var staleCompletion = session.CreateCompletion(() => completedCount++);

        session.Start();
        staleCompletion();

        Assert.Equal(0, completedCount);

        var currentCompletion = session.CreateCompletion(() => completedCount++);
        currentCompletion();

        Assert.Equal(1, completedCount);
    }

    [Fact]
    public async Task PasteFlowUsesCompletionCapturedAtPasteStart()
    {
        var services = CreateServices();
        var settings = services.SettingsService.Load();
        settings.AutoHideAfterPaste = false;
        services.SettingsService.Save(settings);

        var session = new PasteSelectionSession();
        var pasteService = new BlockingClipboardPasteService();
        var completedCount = 0;
        var useCase = new ExecuteSnippetActionUseCase(
            pasteService,
            new RecordingFileLaunchService(),
            new RecordingUrlLaunchService(),
            new RecordingMediaActionService(),
            new SpotifyMediaActionGatewayAdapter(new RecordingSpotifyMediaActionService()));

        session.Start();
        var pasteTask = ExecuteWithCapturedCompletionAsync(
            useCase,
            new Snippet { Content = "Paste me" },
            settings,
            () => session.CreateCompletion(() => completedCount++));
        await pasteService.Started;

        session.Start();
        pasteService.Complete();
        await pasteTask;

        Assert.Equal(0, completedCount);

        var currentCompletion = session.CreateCompletion(() => completedCount++);
        currentCompletion();

        Assert.Equal(1, completedCount);
    }

    private static async Task ExecuteWithCapturedCompletionAsync(
        ExecuteSnippetActionUseCase useCase,
        Snippet snippet,
        AppSettings settings,
        Func<Action> createCompletion)
    {
        var complete = createCompletion();
        try
        {
            await useCase.ExecuteAsync(
                new ExecuteSnippetActionRequest(snippet, settings, new IntPtr(123)));
        }
        finally
        {
            complete();
        }
    }
}

internal sealed class BlockingClipboardPasteService : IClipboardPasteService
{
    private readonly TaskCompletionSource<bool> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public void Complete()
    {
        _completed.SetResult(true);
    }

    public async Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
    {
        _started.SetResult(true);
        return await _completed.Task;
    }
}
