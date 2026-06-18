using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Threading;

namespace DeckDeckDeck.App.ViewModels;

internal sealed class SnippetActionRunner
{
    private const int NotExecuting = 0;
    private const int Executing = 1;

    private readonly MainViewModelCallbacks _callbacks;
    private readonly ExecuteSnippetActionUseCase _executeSnippetActionUseCase;
    private readonly ILoadSettingsUseCase _loadSettingsUseCase;
    private readonly IAppLogger? _logger;
    private readonly PrepareSnippetActionUseCase _prepareSnippetActionUseCase;
    private readonly Action<string> _showStatus;
    private int _isExecuting;

    public SnippetActionRunner(
        ILoadSettingsUseCase loadSettingsUseCase,
        PrepareSnippetActionUseCase prepareSnippetActionUseCase,
        ExecuteSnippetActionUseCase executeSnippetActionUseCase,
        MainViewModelCallbacks callbacks,
        Action<string> showStatus,
        IAppLogger? logger)
    {
        _loadSettingsUseCase = loadSettingsUseCase;
        _prepareSnippetActionUseCase = prepareSnippetActionUseCase;
        _executeSnippetActionUseCase = executeSnippetActionUseCase;
        _callbacks = callbacks;
        _showStatus = showStatus;
        _logger = logger;
    }

    public async Task ExecuteAsync(Snippet snippet)
    {
        await ExecuteAsync(ExecutableAction.FromSnippet(snippet));
    }

    public async Task ExecuteAsync(ExecutableAction action)
    {
        var completePasteSelection = _callbacks.CreatePasteSelectionCompletion();
        if (Interlocked.CompareExchange(ref _isExecuting, Executing, NotExecuting) != NotExecuting)
        {
            completePasteSelection();
            _showStatus("Action is already running.");
            return;
        }

        try
        {
            var settings = _loadSettingsUseCase.Execute();
            var preparation = _prepareSnippetActionUseCase.Execute(
                new PrepareSnippetActionRequest(action, settings));
            if (preparation.ShouldHideBeforeExecute)
            {
                _callbacks.HideWindowAfterPaste();
            }

            var result = await _executeSnippetActionUseCase.ExecuteAsync(
                new ExecuteSnippetActionRequest(
                    action,
                    settings,
                    _callbacks.GetPasteTargetWindowHandle()));

            if (result.ShouldHideWindow)
            {
                _callbacks.HideWindowAfterPaste();
            }

            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                _showStatus(result.StatusMessage);
            }

            LogSnippetActionResult(result);
        }
        finally
        {
            try
            {
                completePasteSelection();
            }
            finally
            {
                Interlocked.Exchange(ref _isExecuting, NotExecuting);
            }
        }
    }

    private void LogSnippetActionResult(ExecuteSnippetActionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.LogMessage))
        {
            return;
        }

        if (result.Exception is null)
        {
            _logger?.Log(result.LogMessage);
            return;
        }

        _logger?.Log(result.LogMessage, result.Exception);
    }
}
