using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
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
        var settings = services.SettingsRepository.Load();
        settings.AutoHideAfterPaste = false;
        services.SettingsRepository.Save(settings);

        var session = new PasteSelectionSession();
        var pasteService = new BlockingClipboardPasteGateway();
        var completedCount = 0;
        var useCase = new ExecuteSnippetActionUseCase(
            pasteService,
            new RecordingFileLaunchGatewayAdapter(),
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new RecordingSpotifyMediaActionGatewayAdapter(),
            new RecordingTerminalCommandGatewayAdapter(),
            new RecordingFilePasteGateway(),
            new RecordingDialogAdapter());

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

    [Fact]
    public async Task ActionExecutionCoordinator_CompletesPasteSelection_BeforeExecutingUseCase()
    {
        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        var session = new PasteSelectionSession();
        var completedCount = 0;
        var wasCompletedDuringExecution = false;

        session.Start();

        var promptInterceptorDialogAdapter = new DialogAdapterWithCallback(() =>
        {
            wasCompletedDuringExecution = completedCount > 0;
        });

        var useCase = new ExecuteSnippetActionUseCase(
            new RecordingClipboardPasteGateway(),
            new RecordingFileLaunchGatewayAdapter(),
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new RecordingSpotifyMediaActionGatewayAdapter(),
            new RecordingTerminalCommandGatewayAdapter(),
            new RecordingFilePasteGateway(),
            promptInterceptorDialogAdapter);

        var action = new ExecutableAction(
            Guid.NewGuid(),
            "ADB Connect",
            string.Empty,
            SnippetActionType.TerminalCommand,
            PasteShortcutMode.CtrlV,
            null,
            FileActionMode.Launch,
            null,
            null,
            null,
            DeckDeckDeck.App.Domain.TerminalCommandParameterRules.AdbWirelessPowerShellExample,
            SnippetTerminalShell.PowerShell,
            false,
            false,
            null,
            "192.168.1.100");

        var callbacks = new ViewModels.MainViewModelCallbacks(
            () => new IntPtr(123),
            () => { },
            () => { },
            () => session.CreateCompletion(() => completedCount++));

        var coordinator = new ActionExecutionCoordinator(
            new LoadSettingsUseCase(services.SettingsRepository),
            new PrepareSnippetActionUseCase(),
            useCase,
            callbacks,
            _ => { },
            null);

        await coordinator.ExecuteAsync(action);

        Assert.True(wasCompletedDuringExecution, "completePasteSelection should be invoked before dialog prompts");
        Assert.Equal(1, completedCount);
    }
}

internal class DialogAdapterWithCallback : IDialogAdapter
{
    private readonly Action _onPrompt;

    public DialogAdapterWithCallback(Action onPrompt)
    {
        _onPrompt = onPrompt;
    }

    public bool Confirm(string title, string message) => true;

    public void ShowInformation(string title, string message) { }

    public string? SelectImageFile() => null;

    public string? SelectLaunchFile() => null;

    public string? SelectPasteFile() => null;

    public string? SelectLaunchFolder() => null;

    public string? SelectBackupFolder() => null;

    public string? SelectBackupZipFile() => null;

    public bool TryPromptTextInputs(
        string title,
        string message,
        IReadOnlyList<string> fieldNames,
        out IReadOnlyDictionary<string, string> values)
    {
        values = new Dictionary<string, string>();
        return true;
    }

    public bool TryPromptAdbPort(string title, string fixedIp, out string port)
    {
        _onPrompt();
        port = "5555";
        return true;
    }
}

internal sealed class BlockingClipboardPasteGateway : IClipboardPasteGateway
{
    private readonly TaskCompletionSource<bool> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<PasteCall> Calls { get; } = [];

    public Task Started => _started.Task;

    public void Complete()
    {
        _completed.SetResult(true);
    }

    public async Task<bool> PasteActionAsync(ExecutableAction action, IntPtr targetWindowHandle, AppSettings settings)
    {
        Calls.Add(new PasteCall(action, targetWindowHandle, settings));
        _started.SetResult(true);
        return await _completed.Task;
    }
}

