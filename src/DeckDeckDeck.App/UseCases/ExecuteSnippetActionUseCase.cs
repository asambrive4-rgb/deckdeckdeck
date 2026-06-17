using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class ExecuteSnippetActionUseCase
{
    private readonly IClipboardPasteGateway _clipboardPasteGateway;
    private readonly IFileLaunchGateway _fileLaunchGateway;
    private readonly IMediaActionGateway _mediaActionGateway;
    private readonly ISpotifyMediaActionGateway _spotifyMediaActionGateway;
    private readonly ITerminalCommandGateway _terminalCommandGateway;
    private readonly IUrlLaunchGateway _urlLaunchGateway;

    public ExecuteSnippetActionUseCase(
        IClipboardPasteGateway clipboardPasteGateway,
        IFileLaunchGateway fileLaunchGateway,
        IUrlLaunchGateway urlLaunchGateway,
        IMediaActionGateway mediaActionGateway,
        ISpotifyMediaActionGateway spotifyMediaActionGateway,
        ITerminalCommandGateway terminalCommandGateway)
    {
        _clipboardPasteGateway = clipboardPasteGateway;
        _fileLaunchGateway = fileLaunchGateway;
        _urlLaunchGateway = urlLaunchGateway;
        _mediaActionGateway = mediaActionGateway;
        _spotifyMediaActionGateway = spotifyMediaActionGateway;
        _terminalCommandGateway = terminalCommandGateway;
    }

    public async Task<ExecuteSnippetActionResult> ExecuteAsync(
        ExecuteSnippetActionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return request.Action.ActionType switch
            {
                SnippetActionType.LaunchFile => LaunchSnippet(request.Action, request.Settings),
                SnippetActionType.LaunchUrl => LaunchUrlSnippet(request.Action, request.Settings),
                SnippetActionType.MediaAction => await ExecuteMediaSnippetAsync(
                    request.Action,
                    request.Settings,
                    cancellationToken),
                SnippetActionType.TerminalCommand => ExecuteTerminalCommandSnippet(
                    request.Action,
                    request.Settings),
                _ => await PasteTextSnippetAsync(request)
            };
        }
        catch (Exception ex)
        {
            return ExecuteSnippetActionResult.Failure(
                logMessage: $"Paste failed for action {request.Action.Id}.",
                exception: ex);
        }
    }

    private async Task<ExecuteSnippetActionResult> PasteTextSnippetAsync(
        ExecuteSnippetActionRequest request)
    {
        var pasted = await _clipboardPasteGateway.PasteActionAsync(
            request.Action,
            request.TargetWindowHandle,
            request.Settings);

        return pasted
            ? ExecuteSnippetActionResult.Noop()
            : ExecuteSnippetActionResult.Failure($"Paste failed for action {request.Action.Id}.");
    }

    private ExecuteSnippetActionResult LaunchSnippet(ExecutableAction action, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(action.LaunchPath))
        {
            return ReportLaunchFailure(
                action,
                "실행할 파일, 폴더 또는 바로 가기 경로가 없습니다.");
        }

        try
        {
            var launched = _fileLaunchGateway.TryLaunch(action.LaunchPath);
            if (!launched)
            {
                return ReportLaunchFailure(
                    action,
                    "실행할 파일, 폴더 또는 바로 가기를 찾지 못했습니다.");
            }

            return ExecuteSnippetActionResult.Success(
                shouldHideWindow: settings.AutoHideAfterPaste,
                statusMessage: $"{action.Title} 실행됨");
        }
        catch (Exception ex)
        {
            return ReportLaunchFailure(action, "실행하지 못했습니다.", ex);
        }
    }

    private static ExecuteSnippetActionResult ReportLaunchFailure(
        ExecutableAction action,
        string message,
        Exception? exception = null)
    {
        return ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 실행 실패: {message}",
            logMessage: $"Launch failed for action {action.Id}: {message}",
            exception: exception);
    }

    private ExecuteSnippetActionResult LaunchUrlSnippet(ExecutableAction action, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(action.LaunchUrl))
        {
            return ReportUrlLaunchFailure(action, "웹페이지 주소가 없습니다.");
        }

        try
        {
            var launched = _urlLaunchGateway.TryLaunch(action.LaunchUrl);
            if (!launched)
            {
                return ReportUrlLaunchFailure(
                    action,
                    "웹페이지 주소 형식이 올바르지 않습니다.");
            }

            return ExecuteSnippetActionResult.Success(
                shouldHideWindow: settings.AutoHideAfterPaste,
                statusMessage: $"{action.Title} 웹페이지를 열었습니다.");
        }
        catch (Exception ex)
        {
            return ReportUrlLaunchFailure(action, "웹페이지를 열지 못했습니다.", ex);
        }
    }

    private static ExecuteSnippetActionResult ReportUrlLaunchFailure(
        ExecutableAction action,
        string message,
        Exception? exception = null)
    {
        return ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 웹 주소 열기 실패: {message}",
            logMessage: $"Launch URL failed for action {action.Id}: {message}",
            exception: exception);
    }

    private async Task<ExecuteSnippetActionResult> ExecuteMediaSnippetAsync(
        ExecutableAction action,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var provider = action.MediaProvider ?? SnippetMediaProvider.System;
        if (provider is SnippetMediaProvider.Spotify)
        {
            return await ExecuteSpotifyMediaSnippetAsync(action, settings, cancellationToken);
        }

        if (provider is not SnippetMediaProvider.System)
        {
            return ReportMediaActionFailure(action, "지원하지 않는 미디어 제공자입니다.");
        }

        var command = action.MediaCommand ?? SnippetMediaCommand.PlayPause;

        try
        {
            var executed = _mediaActionGateway.TryExecute(command);
            if (!executed)
            {
                return ReportMediaActionFailure(
                    action,
                    "Windows 미디어 키 입력을 보내지 못했습니다.");
            }

            return ExecuteSnippetActionResult.Success(
                shouldHideWindow: settings.AutoHideAfterPaste,
                statusMessage: $"{action.Title} 미디어 명령 실행됨");
        }
        catch (Exception ex)
        {
            return ReportMediaActionFailure(
                action,
                "미디어 명령을 실행하지 못했습니다.",
                ex);
        }
    }

    private async Task<ExecuteSnippetActionResult> ExecuteSpotifyMediaSnippetAsync(
        ExecutableAction action,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var command = action.MediaCommand ?? SnippetMediaCommand.PlayPause;

        try
        {
            var executed = await _spotifyMediaActionGateway.TryExecuteAsync(command, cancellationToken);
            if (!executed.Succeeded)
            {
                return ReportMediaActionFailure(
                    action,
                    executed.ErrorMessage ?? "Spotify 명령을 실행하지 못했습니다.");
            }

            return ExecuteSnippetActionResult.Success(
                shouldHideWindow: settings.AutoHideAfterPaste,
                statusMessage: $"{action.Title} Spotify 명령 실행됨");
        }
        catch (Exception ex)
        {
            return ReportMediaActionFailure(
                action,
                "Spotify 명령을 실행하지 못했습니다.",
                ex);
        }
    }

    private static ExecuteSnippetActionResult ReportMediaActionFailure(
        ExecutableAction action,
        string message,
        Exception? exception = null)
    {
        return ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 미디어 제어 실패: {message}",
            logMessage: $"Media action failed for action {action.Id}: {message}",
            exception: exception);
    }

    private ExecuteSnippetActionResult ExecuteTerminalCommandSnippet(
        ExecutableAction action,
        AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(action.TerminalCommand))
        {
            return ReportTerminalCommandFailure(action, "실행할 터미널 명령이 없습니다.");
        }

        var shell = action.TerminalShell ?? SnippetTerminalShell.Cmd;

        try
        {
            var executed = _terminalCommandGateway.TryExecute(
                action.TerminalCommand,
                shell,
                action.RunAsAdministrator);
            if (!executed)
            {
                return ReportTerminalCommandFailure(
                    action,
                    "터미널 명령을 시작하지 못했습니다.");
            }

            return ExecuteSnippetActionResult.Success(
                shouldHideWindow: settings.AutoHideAfterPaste,
                statusMessage: $"{action.Title} 터미널 명령 실행됨");
        }
        catch (Exception ex)
        {
            return ReportTerminalCommandFailure(
                action,
                "터미널 명령을 실행하지 못했습니다.",
                ex);
        }
    }

    private static ExecuteSnippetActionResult ReportTerminalCommandFailure(
        ExecutableAction action,
        string message,
        Exception? exception = null)
    {
        return ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 터미널 명령 실행 실패: {message}",
            logMessage: $"Terminal command failed for action {action.Id}: {message}",
            exception: exception);
    }
}

public sealed record ExecuteSnippetActionRequest(
    ExecutableAction Action,
    AppSettings Settings,
    IntPtr TargetWindowHandle)
{
    public ExecuteSnippetActionRequest(
        Snippet snippet,
        AppSettings settings,
        IntPtr targetWindowHandle)
        : this(ExecutableAction.FromSnippet(snippet), settings, targetWindowHandle)
    {
    }
}

public sealed record ExecuteSnippetActionResult(
    bool Succeeded,
    bool ShouldHideWindow,
    string? StatusMessage = null,
    string? LogMessage = null,
    Exception? Exception = null)
{
    public static ExecuteSnippetActionResult Noop()
    {
        return new ExecuteSnippetActionResult(true, ShouldHideWindow: false);
    }

    public static ExecuteSnippetActionResult Success(
        bool shouldHideWindow,
        string statusMessage)
    {
        return new ExecuteSnippetActionResult(true, shouldHideWindow, statusMessage);
    }

    public static ExecuteSnippetActionResult Failure(
        string? logMessage = null,
        string? statusMessage = null,
        Exception? exception = null)
    {
        return new ExecuteSnippetActionResult(
            false,
            ShouldHideWindow: false,
            StatusMessage: statusMessage,
            LogMessage: logMessage,
            Exception: exception);
    }
}

