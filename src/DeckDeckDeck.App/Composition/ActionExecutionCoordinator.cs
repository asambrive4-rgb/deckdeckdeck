// 역할: 슬롯에 등록된 실행 동작을 받아 적절한 처리 기능으로 연결하고 실행 흐름을 조율합니다.
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.Threading;

namespace DeckDeckDeck.App.Composition;

internal sealed class ActionExecutionCoordinator
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

    public ActionExecutionCoordinator(
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

    public async Task ExecuteAsync(ExecutableAction action)
    {
        var completePasteSelection = _callbacks.CreatePasteSelectionCompletion();
        if (Interlocked.CompareExchange(ref _isExecuting, Executing, NotExecuting) != NotExecuting)
        {
            completePasteSelection();
            _showStatus("Action is already running.");
            return;
        }

        var completedPasteSelection = 0;
        void FinishPasteSelection()
        {
            if (Interlocked.Exchange(ref completedPasteSelection, 1) == 0)
            {
                completePasteSelection();
            }
        }

        try
        {
            FinishPasteSelection();
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
                FinishPasteSelection();
            }
            finally
            {
                Interlocked.Exchange(ref _isExecuting, NotExecuting);
            }
        }
    }

    private void LogSnippetActionResult(ExecuteSnippetActionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.LogMessage)) return;

        if (result.Exception is null)
        {
            _logger?.Log(result.LogMessage);
        }
        else
        {
            _logger?.Log(result.LogMessage, result.Exception);
        }
    }
}
