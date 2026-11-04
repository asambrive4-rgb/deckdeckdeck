// 역할: 슬롯 실행 요청을 받아 텍스트·파일 붙여넣기, 앱 실행, URL 열기, 미디어 제어, 터미널 명령 및 블루투스 기기 제어 등 대상 동작을 조율하고 실행합니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

/// <summary>
/// Runs one executable action. When adding <see cref="SnippetActionType"/>,
/// update <see cref="Domain.ExecutableActionTypeCatalog"/> first, then add a case here.
/// </summary>
public sealed class ExecuteSnippetActionUseCase(
    IClipboardPasteGateway clipboardPasteGateway,
    IFileLaunchGateway fileLaunchGateway,
    IUrlLaunchGateway urlLaunchGateway,
    IMediaActionGateway mediaActionGateway,
    ISpotifyMediaActionGateway spotifyMediaActionGateway,
    ITerminalCommandGateway terminalCommandGateway,
    IFilePasteGateway filePasteGateway,
    IDialogAdapter dialogAdapter,
    ITimerActionGateway? timerActionGateway = null,
    IDisplayPowerGateway? displayPowerGateway = null,
    IBluetoothDeviceControlGateway? bluetoothDeviceControlGateway = null)
{
    public async Task<ExecuteSnippetActionResult> ExecuteAsync(
        ExecuteSnippetActionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return request.Action.ActionType switch
            {
                SnippetActionType.LaunchFile when request.Action.FileActionMode == FileActionMode.Paste =>
                    await PasteFileSnippetAsync(request, cancellationToken),
                SnippetActionType.LaunchFile => LaunchSnippet(request.Action, request.Settings),
                SnippetActionType.LaunchUrl => LaunchUrlSnippet(request.Action, request.Settings),
                SnippetActionType.MediaAction => await ExecuteMediaSnippetAsync(request.Action, request.Settings, cancellationToken),
                SnippetActionType.TerminalCommand => await ExecuteTerminalCommandSnippetAsync(request.Action, request.Settings, cancellationToken),
                SnippetActionType.Timer => ExecuteTimerSnippet(request.Action),
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

    private ExecuteSnippetActionResult ExecuteTimerSnippet(ExecutableAction action)
    {
        if (action.TargetSlotKey is { } slotKey)
        {
            timerActionGateway?.ExecuteTimerAction(slotKey);
        }

        return ExecuteSnippetActionResult.Success(shouldHideWindow: false, statusMessage: "타이머 실행");
    }

    private async Task<ExecuteSnippetActionResult> PasteFileSnippetAsync(
        ExecuteSnippetActionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Action.LaunchPath))
            return ReportFilePasteFailure(request.Action, "붙여넣을 파일 경로가 없습니다.");

        if (request.TargetWindowHandle == IntPtr.Zero)
            return ReportFilePasteFailure(request.Action, "붙여넣을 대상 창을 찾지 못했습니다.");

        try
        {
            var gatewayResult = await filePasteGateway.PasteFileAsync(
                request.Action.LaunchPath,
                request.TargetWindowHandle,
                request.Settings);

            return gatewayResult.Status switch
            {
                FilePasteGatewayStatus.Succeeded => ExecuteSnippetActionResult.Success(
                    shouldHideWindow: false,
                    statusMessage: $"{request.Action.Title} 파일 붙여넣기 요청됨"),
                FilePasteGatewayStatus.FileNotFound => ReportFilePasteFailure(
                    request.Action,
                    "붙여넣을 파일을 찾지 못했습니다."),
                _ => ReportFilePasteFailure(
                    request.Action,
                    "파일 붙여넣기를 요청하지 못했습니다.",
                    gatewayResult.Exception)
            };
        }
        catch (Exception ex)
        {
            return ReportFilePasteFailure(request.Action, "파일 붙여넣기를 요청하지 못했습니다.", ex);
        }
    }

    private async Task<ExecuteSnippetActionResult> PasteTextSnippetAsync(ExecuteSnippetActionRequest request)
    {
        var pasted = await clipboardPasteGateway.PasteActionAsync(
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
            return ReportLaunchFailure(action, "실행할 파일, 폴더 또는 바로 가기 경로가 없습니다.");

        try
        {
            return fileLaunchGateway.TryLaunch(action.LaunchPath)
                ? ExecuteSnippetActionResult.Success(settings.AutoHideAfterPaste, $"{action.Title} 실행됨")
                : ReportLaunchFailure(action, "실행할 파일, 폴더 또는 바로 가기를 찾지 못했습니다.");
        }
        catch (Exception ex)
        {
            return ReportLaunchFailure(action, "실행하지 못했습니다.", ex);
        }
    }

    private ExecuteSnippetActionResult LaunchUrlSnippet(ExecutableAction action, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(action.LaunchUrl))
            return ReportUrlLaunchFailure(action, "웹페이지 주소가 없습니다.");

        try
        {
            return urlLaunchGateway.TryLaunch(action.LaunchUrl)
                ? ExecuteSnippetActionResult.Success(settings.AutoHideAfterPaste, $"{action.Title} 웹페이지를 열었습니다.")
                : ReportUrlLaunchFailure(action, "웹페이지 주소 형식이 올바르지 않습니다.");
        }
        catch (Exception ex)
        {
            return ReportUrlLaunchFailure(action, "웹페이지를 열지 못했습니다.", ex);
        }
    }

    private Task<ExecuteSnippetActionResult> ExecuteMediaSnippetAsync(
        ExecutableAction action,
        AppSettings settings,
        CancellationToken cancellationToken) =>
        (action.MediaProvider ?? SnippetMediaProvider.System) switch
        {
            SnippetMediaProvider.Spotify => ExecuteSpotifyMediaSnippetAsync(action, settings, cancellationToken),
            SnippetMediaProvider.System => Task.FromResult(ExecuteSystemMediaSnippet(action, settings)),
            _ => Task.FromResult(ReportMediaActionFailure(action, "지원하지 않는 미디어 제공자입니다."))
        };

    private ExecuteSnippetActionResult ExecuteSystemMediaSnippet(ExecutableAction action, AppSettings settings)
    {
        try
        {
            return mediaActionGateway.TryExecute(action.MediaCommand ?? SnippetMediaCommand.PlayPause)
                ? ExecuteSnippetActionResult.Success(settings.AutoHideAfterPaste, $"{action.Title} 미디어 명령 실행됨")
                : ReportMediaActionFailure(action, "Windows 미디어 키 입력을 보내지 못했습니다.");
        }
        catch (Exception ex)
        {
            return ReportMediaActionFailure(action, "미디어 명령을 실행하지 못했습니다.", ex);
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
            var executed = await spotifyMediaActionGateway.TryExecuteAsync(command, cancellationToken);
            return executed.Succeeded
                ? ExecuteSnippetActionResult.Success(settings.AutoHideAfterPaste, $"{action.Title} Spotify 명령 실행됨")
                : ReportMediaActionFailure(action, executed.ErrorMessage ?? "Spotify 명령을 실행하지 못했습니다.");
        }
        catch (Exception ex)
        {
            return ReportMediaActionFailure(action, "Spotify 명령을 실행하지 못했습니다.", ex);
        }
    }

    private async Task<ExecuteSnippetActionResult> ExecuteTerminalCommandSnippetAsync(
        ExecutableAction action,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.TerminalCommand) && !action.OpenTerminalWindow)
            return ReportTerminalCommandFailure(action, "실행할 터미널 명령이 없습니다.");

        var commandTemplate = action.TerminalCommand ?? string.Empty;
        var isTurnOffDisplay = TerminalCommandParameterRules.IsTurnOffDisplayCommand(commandTemplate);
        if (isTurnOffDisplay && displayPowerGateway is not null)
            return ExecuteTurnOffDisplay(action);

        if (TerminalCommandParameterRules.TryParseBluetoothAudioCommand(commandTemplate, out var bluetoothCommand)
            && bluetoothDeviceControlGateway is not null)
        {
            var controlled = await bluetoothDeviceControlGateway.TryControlAsync(bluetoothCommand, cancellationToken);
            return controlled
                ? ExecuteSnippetActionResult.Success(
                    shouldHideWindow: settings.AutoHideAfterPaste,
                    statusMessage: string.IsNullOrWhiteSpace(action.Title)
                        ? (bluetoothCommand.Action == BluetoothAudioActionKind.Connect ? "블루투스 연결 실행됨" : "블루투스 연결 해제 실행됨")
                        : (bluetoothCommand.Action == BluetoothAudioActionKind.Connect ? $"{action.Title} 블루투스 연결 실행됨" : $"{action.Title} 블루투스 연결 해제 실행됨"))
                : ReportTerminalCommandFailure(action, "블루투스 기기 연결/해제에 실패했습니다. 기기 전원과 페어링 상태를 확인해 주세요.");
        }

        if (!TryResolveTerminalCommand(action, commandTemplate, out var command, out var isAdbPair, out var resolveFailure))
            return resolveFailure!;

        var shell = action.TerminalShell ?? SnippetTerminalShell.Cmd;
        var isAdbConnect = TerminalCommandParameterRules.IsAdbWirelessConnectCommand(commandTemplate);

        try
        {
            if (!terminalCommandGateway.TryExecute(
                    command,
                    shell,
                    action.RunAsAdministrator,
                    action.OpenTerminalWindow,
                    action.TerminalWorkingDirectory))
            {
                return ReportTerminalCommandFailure(
                    action,
                    isAdbPair ? "ADB 페어링 명령을 시작하지 못했습니다." :
                    isAdbConnect ? "ADB 연결 명령을 시작하지 못했습니다." : "터미널 명령을 시작하지 못했습니다.");
            }

            var statusMessage = (isAdbPair, isAdbConnect, isTurnOffDisplay) switch
            {
                (true, _, _) => $"{action.Title} ADB 페어링 실행됨",
                (_, true, _) => $"{action.Title} ADB 연결 실행됨",
                (_, _, true) => $"{action.Title} 실행됨",
                _ => $"{action.Title} 터미널 명령 실행됨"
            };

            return ExecuteSnippetActionResult.Success(
                shouldHideWindow: isTurnOffDisplay || settings.AutoHideAfterPaste,
                statusMessage: statusMessage);
        }
        catch (Exception ex)
        {
            return ReportTerminalCommandFailure(action, "터미널 명령을 실행하지 못했습니다.", ex);
        }
    }

    private ExecuteSnippetActionResult ExecuteTurnOffDisplay(ExecutableAction action)
    {
        try
        {
            return displayPowerGateway?.TryTurnOffDisplay() == true
                ? ExecuteSnippetActionResult.Success(
                    shouldHideWindow: true,
                    statusMessage: string.IsNullOrWhiteSpace(action.Title) ? "화면 끄기 실행됨" : $"{action.Title} 실행됨")
                : ReportTerminalCommandFailure(action, "화면을 끄지 못했습니다.");
        }
        catch (Exception ex)
        {
            return ReportTerminalCommandFailure(action, "화면 끄기를 실행하지 못했습니다.", ex);
        }
    }

    private bool TryResolveTerminalCommand(
        ExecutableAction action,
        string commandTemplate,
        out string command,
        out bool isAdbPair,
        out ExecuteSnippetActionResult? failure)
    {
        isAdbPair = false;
        if (TerminalCommandParameterRules.IsAdbWirelessConnectCommand(commandTemplate))
            return TryResolveAdbConnectCommand(action, commandTemplate, out command, out isAdbPair, out failure);

        var parameterNames = TerminalCommandParameterRules.ExtractParameterNames(commandTemplate);
        if (parameterNames.Count == 0)
        {
            command = commandTemplate;
            failure = null;
            return true;
        }

        if (!dialogAdapter.TryPromptTextInputs(
                title: action.Title,
                message: "명령에 넣을 값을 입력하세요.",
                fieldNames: parameterNames,
                out var values))
        {
            command = string.Empty;
            failure = ExecuteSnippetActionResult.Failure(statusMessage: $"{action.Title} 입력이 취소되었습니다.");
            return false;
        }

        command = TerminalCommandParameterRules.ApplyParameters(commandTemplate, values);
        failure = null;
        return true;
    }

    private bool TryResolveAdbConnectCommand(
        ExecutableAction action,
        string commandTemplate,
        out string command,
        out bool isAdbPair,
        out ExecuteSnippetActionResult? failure)
    {
        command = string.Empty;
        isAdbPair = false;

        if (!TerminalCommandParameterRules.TryNormalizeAdbIp(action.AdbDeviceIp, out var normalizedIp, out var ipError))
        {
            failure = ReportTerminalCommandFailure(action, ipError ?? TerminalCommandParameterRules.EmptyAdbIpMessage);
            return false;
        }

        if (!dialogAdapter.TryPromptAdbAction(action.Title, normalizedIp, out var promptResult))
        {
            failure = ExecuteSnippetActionResult.Failure(statusMessage: $"{action.Title} 입력이 취소되었습니다.");
            return false;
        }

        if (promptResult.Mode == AdbPromptMode.Pair)
        {
            if (!TerminalCommandParameterRules.TryNormalizeAdbPort(promptResult.Port, out var normalizedPort, out var portError))
            {
                failure = ReportTerminalCommandFailure(action, portError ?? TerminalCommandParameterRules.InvalidAdbPortMessage);
                return false;
            }

            if (!TerminalCommandParameterRules.TryNormalizeAdbPairCode(promptResult.PairCode, out var normalizedCode, out var codeError))
            {
                failure = ReportTerminalCommandFailure(action, codeError ?? TerminalCommandParameterRules.InvalidAdbPairCodeMessage);
                return false;
            }

            command = TerminalCommandParameterRules.BuildAdbPairCommand(commandTemplate, normalizedIp, normalizedPort, normalizedCode);
            isAdbPair = true;
            failure = null;
            return true;
        }
        else
        {
            if (!TerminalCommandParameterRules.TryNormalizeAdbPort(promptResult.Port, out var normalizedPort, out var portError))
            {
                failure = ReportTerminalCommandFailure(action, portError ?? TerminalCommandParameterRules.InvalidAdbPortMessage);
                return false;
            }

            command = TerminalCommandParameterRules.ApplyParameters(
                commandTemplate,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [TerminalCommandParameterRules.AdbIpParameterName] = normalizedIp,
                    [TerminalCommandParameterRules.AdbPortParameterName] = normalizedPort
                });
            isAdbPair = false;
            failure = null;
            return true;
        }
    }

    private static ExecuteSnippetActionResult ReportFilePasteFailure(
        ExecutableAction action, string message, Exception? exception = null) =>
        ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 파일 붙여넣기 실패: {message}",
            logMessage: $"File paste failed for action {action.Id}: {message}",
            exception: exception);

    private static ExecuteSnippetActionResult ReportLaunchFailure(
        ExecutableAction action, string message, Exception? exception = null) =>
        ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 실행 실패: {message}",
            logMessage: $"Launch failed for action {action.Id}: {message}",
            exception: exception);

    private static ExecuteSnippetActionResult ReportUrlLaunchFailure(
        ExecutableAction action, string message, Exception? exception = null) =>
        ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 웹 주소 열기 실패: {message}",
            logMessage: $"Launch URL failed for action {action.Id}: {message}",
            exception: exception);

    private static ExecuteSnippetActionResult ReportMediaActionFailure(
        ExecutableAction action, string message, Exception? exception = null) =>
        ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 미디어 제어 실패: {message}",
            logMessage: $"Media action failed for action {action.Id}: {message}",
            exception: exception);

    private static ExecuteSnippetActionResult ReportTerminalCommandFailure(
        ExecutableAction action, string message, Exception? exception = null) =>
        ExecuteSnippetActionResult.Failure(
            statusMessage: $"{action.Title} 터미널 명령 실행 실패: {message}",
            logMessage: $"Terminal command failed for action {action.Id}: {message}",
            exception: exception);
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
    public static ExecuteSnippetActionResult Noop() => new(true, ShouldHideWindow: false);

    public static ExecuteSnippetActionResult Success(bool shouldHideWindow, string statusMessage) =>
        new(true, shouldHideWindow, statusMessage);

    public static ExecuteSnippetActionResult Failure(
        string? logMessage = null,
        string? statusMessage = null,
        Exception? exception = null) =>
        new(false, ShouldHideWindow: false, StatusMessage: statusMessage, LogMessage: logMessage, Exception: exception);
}
