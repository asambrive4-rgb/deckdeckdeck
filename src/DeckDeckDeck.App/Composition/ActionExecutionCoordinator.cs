// 역할: 슬롯 동작 및 다이렉트 핫키 실행을 단일 진입점에서 조율하고 세션, 중복 실행 방지, 오류 처리 및 로깅을 통합 관리합니다.
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;

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
    private readonly ResolveExecutableHotkeyActionUseCase? _resolveExecutableHotkeyActionUseCase;
    private int _isExecuting;

    public ActionExecutionCoordinator(
        ILoadSettingsUseCase loadSettingsUseCase,
        PrepareSnippetActionUseCase prepareSnippetActionUseCase,
        ExecuteSnippetActionUseCase executeSnippetActionUseCase,
        MainViewModelCallbacks callbacks,
        Action<string> showStatus,
        IAppLogger? logger,
        ResolveExecutableHotkeyActionUseCase? resolveExecutableHotkeyActionUseCase = null)
    {
        _loadSettingsUseCase = loadSettingsUseCase;
        _prepareSnippetActionUseCase = prepareSnippetActionUseCase;
        _executeSnippetActionUseCase = executeSnippetActionUseCase;
        _callbacks = callbacks;
        _showStatus = showStatus;
        _logger = logger;
        _resolveExecutableHotkeyActionUseCase = resolveExecutableHotkeyActionUseCase;
    }

    public async Task ExecuteDirectHotkeyAsync(Guid hotkeyActionId)
    {
        var action = _resolveExecutableHotkeyActionUseCase?.Execute(hotkeyActionId);
        if (action is null) return;

        await ExecuteAsync(action);
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
        catch (Exception ex)
        {
            _showStatus("핫키 실행 중 오류가 발생했습니다.");
            _logger?.Log("핫키 실행 중 오류가 발생했습니다.", ex);
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
            _logger?.Log(result.LogMessage);
        else
            _logger?.Log(result.LogMessage, result.Exception);
    }
}
