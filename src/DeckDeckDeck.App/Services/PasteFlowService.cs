using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

internal sealed class PasteFlowService
{
    private readonly IClipboardPasteService _clipboardPasteService;
    private readonly Func<Action> _createPasteSelectionCompletion;
    private readonly IFileLaunchService _fileLaunchService;
    private readonly Func<IntPtr> _getPasteTargetWindowHandle;
    private readonly Action _hideWindowAfterPaste;
    private readonly LoggingService? _loggingService;
    private readonly IMediaActionService _mediaActionService;
    private readonly ISpotifyMediaActionService _spotifyMediaActionService;
    private readonly SettingsService _settingsService;
    private readonly Action<string> _showStatus;
    private readonly IUrlLaunchService _urlLaunchService;

    public PasteFlowService(
        IClipboardPasteService clipboardPasteService,
        IFileLaunchService fileLaunchService,
        IUrlLaunchService urlLaunchService,
        IMediaActionService mediaActionService,
        ISpotifyMediaActionService spotifyMediaActionService,
        SettingsService settingsService,
        Func<IntPtr> getPasteTargetWindowHandle,
        Action hideWindowAfterPaste,
        Func<Action> createPasteSelectionCompletion,
        Action<string> showStatus,
        LoggingService? loggingService)
    {
        _clipboardPasteService = clipboardPasteService;
        _fileLaunchService = fileLaunchService;
        _urlLaunchService = urlLaunchService;
        _mediaActionService = mediaActionService;
        _spotifyMediaActionService = spotifyMediaActionService;
        _settingsService = settingsService;
        _getPasteTargetWindowHandle = getPasteTargetWindowHandle;
        _hideWindowAfterPaste = hideWindowAfterPaste;
        _createPasteSelectionCompletion = createPasteSelectionCompletion;
        _showStatus = showStatus;
        _loggingService = loggingService;
    }

    public async Task PasteSnippetAsync(Snippet snippet)
    {
        var settings = _settingsService.Load();
        var completePasteSelection = _createPasteSelectionCompletion();

        try
        {
            if (snippet.ActionType == SnippetActionType.LaunchFile)
            {
                LaunchSnippet(snippet, settings);
                return;
            }

            if (snippet.ActionType == SnippetActionType.LaunchUrl)
            {
                LaunchUrlSnippet(snippet, settings);
                return;
            }

            if (snippet.ActionType == SnippetActionType.MediaAction)
            {
                await ExecuteMediaSnippetAsync(snippet, settings);
                return;
            }

            if (settings.AutoHideAfterPaste)
            {
                _hideWindowAfterPaste();
            }

            var pasted = await _clipboardPasteService.PasteSnippetAsync(
                snippet,
                _getPasteTargetWindowHandle(),
                settings);

            if (!pasted)
            {
                _loggingService?.Log($"Paste failed for snippet {snippet.Id}.");
            }
        }
        catch (Exception ex)
        {
            _loggingService?.Log($"Paste failed for snippet {snippet.Id}.", ex);
            throw;
        }
        finally
        {
            completePasteSelection();
        }
    }

    private void LaunchSnippet(Snippet snippet, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(snippet.LaunchPath))
        {
            ReportLaunchFailure(snippet, "실행할 파일, 폴더 또는 바로 가기 경로가 없습니다.");
            return;
        }

        try
        {
            var launched = _fileLaunchService.TryLaunch(snippet.LaunchPath);
            if (!launched)
            {
                ReportLaunchFailure(snippet, "실행할 파일, 폴더 또는 바로 가기를 찾지 못했습니다.");
                return;
            }

            if (settings.AutoHideAfterPaste)
            {
                _hideWindowAfterPaste();
            }

            _showStatus($"{snippet.Title} 실행됨.");
        }
        catch (Exception ex)
        {
            ReportLaunchFailure(snippet, "실행하지 못했습니다.", ex);
        }
    }

    private void ReportLaunchFailure(Snippet snippet, string message, Exception? exception = null)
    {
        _showStatus($"{snippet.Title} 실행 실패: {message}");

        if (exception is null)
        {
            _loggingService?.Log($"Launch failed for snippet {snippet.Id}: {message}");
            return;
        }

        _loggingService?.Log($"Launch failed for snippet {snippet.Id}: {message}", exception);
    }

    private void LaunchUrlSnippet(Snippet snippet, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(snippet.LaunchUrl))
        {
            ReportUrlLaunchFailure(snippet, "열 웹페이지 주소가 없습니다.");
            return;
        }

        try
        {
            var launched = _urlLaunchService.TryLaunch(snippet.LaunchUrl);
            if (!launched)
            {
                ReportUrlLaunchFailure(snippet, "웹페이지 주소 형식이 올바르지 않습니다.");
                return;
            }

            if (settings.AutoHideAfterPaste)
            {
                _hideWindowAfterPaste();
            }

            _showStatus($"{snippet.Title} 웹페이지를 열었습니다.");
        }
        catch (Exception ex)
        {
            ReportUrlLaunchFailure(snippet, "웹페이지를 열지 못했습니다.", ex);
        }
    }

    private void ReportUrlLaunchFailure(Snippet snippet, string message, Exception? exception = null)
    {
        _showStatus($"{snippet.Title} 웹 주소 열기 실패: {message}");

        if (exception is null)
        {
            _loggingService?.Log($"Launch URL failed for snippet {snippet.Id}: {message}");
            return;
        }

        _loggingService?.Log($"Launch URL failed for snippet {snippet.Id}: {message}", exception);
    }

    private async Task ExecuteMediaSnippetAsync(Snippet snippet, AppSettings settings)
    {
        var provider = snippet.MediaProvider ?? SnippetMediaProvider.System;
        if (provider is SnippetMediaProvider.Spotify)
        {
            await ExecuteSpotifyMediaSnippetAsync(snippet, settings);
            return;
        }

        if (provider is not SnippetMediaProvider.System)
        {
            ReportMediaActionFailure(snippet, "지원하지 않는 미디어 제공자입니다.");
            return;
        }

        var command = snippet.MediaCommand ?? SnippetMediaCommand.PlayPause;

        try
        {
            var executed = _mediaActionService.TryExecute(command);
            if (!executed)
            {
                ReportMediaActionFailure(snippet, "Windows 미디어 키 입력을 보내지 못했습니다.");
                return;
            }

            if (settings.AutoHideAfterPaste)
            {
                _hideWindowAfterPaste();
            }

            _showStatus($"{snippet.Title} 미디어 명령 실행됨.");
        }
        catch (Exception ex)
        {
            ReportMediaActionFailure(snippet, "미디어 명령을 실행하지 못했습니다.", ex);
        }
    }

    private async Task ExecuteSpotifyMediaSnippetAsync(Snippet snippet, AppSettings settings)
    {
        var command = snippet.MediaCommand ?? SnippetMediaCommand.PlayPause;

        try
        {
            var executed = await _spotifyMediaActionService.TryExecuteAsync(command);
            if (!executed.Succeeded)
            {
                ReportMediaActionFailure(snippet, executed.ErrorMessage ?? "Spotify 명령을 실행하지 못했습니다.");
                return;
            }

            if (settings.AutoHideAfterPaste)
            {
                _hideWindowAfterPaste();
            }

            _showStatus($"{snippet.Title} Spotify 명령 실행됨.");
        }
        catch (Exception ex)
        {
            ReportMediaActionFailure(snippet, "Spotify 명령을 실행하지 못했습니다.", ex);
        }
    }

    private void ReportMediaActionFailure(Snippet snippet, string message, Exception? exception = null)
    {
        _showStatus($"{snippet.Title} 미디어 제어 실패: {message}");

        if (exception is null)
        {
            _loggingService?.Log($"Media action failed for snippet {snippet.Id}: {message}");
            return;
        }

        _loggingService?.Log($"Media action failed for snippet {snippet.Id}: {message}", exception);
    }
}
