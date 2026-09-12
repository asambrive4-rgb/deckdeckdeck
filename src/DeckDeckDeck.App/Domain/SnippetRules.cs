// 역할: 저장할 텍스트 스니펫의 내용과 형식이 올바른지 검사하는 규칙을 정의합니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public static class SnippetRules
{
    public const string TitleRequiredMessage = "슬롯 이름을 입력해 주세요.";
    public const string PasteContentRequiredMessage = "붙여넣을 문구를 입력해 주세요.";
    public const string LaunchPathRequiredMessage = "실행할 파일, 폴더 또는 바로 가기를 선택해 주세요.";
    public const string LaunchUrlRequiredMessage = "열 웹페이지 주소를 http 또는 https 주소로 입력해 주세요.";
    public const string TerminalCommandRequiredMessage = "실행할 터미널 명령을 입력해 주세요.";

    public static SnippetSaveValidationResult ValidateForSave(
        string? title,
        string? content,
        SnippetActionType actionType,
        string? launchPath,
        string? launchUrl,
        SnippetMediaProvider selectedMediaProvider,
        SnippetMediaCommand selectedMediaCommand,
        string? terminalCommand = null,
        SnippetTerminalShell selectedTerminalShell = SnippetTerminalShell.Cmd,
        bool runAsAdministrator = true,
        bool openTerminalWindow = false,
        string? terminalWorkingDirectory = null,
        string? adbDeviceIp = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return SnippetSaveValidationResult.Failure(TitleRequiredMessage);
        }

        if (actionType == SnippetActionType.PasteText && string.IsNullOrWhiteSpace(content))
        {
            return SnippetSaveValidationResult.Failure(PasteContentRequiredMessage);
        }

        if (actionType == SnippetActionType.LaunchFile && string.IsNullOrWhiteSpace(launchPath))
        {
            return SnippetSaveValidationResult.Failure(LaunchPathRequiredMessage);
        }

        var normalizedLaunchUrl = launchUrl;
        if (actionType == SnippetActionType.LaunchUrl
            && !UrlRules.TryNormalize(launchUrl, out normalizedLaunchUrl))
        {
            return SnippetSaveValidationResult.Failure(LaunchUrlRequiredMessage);
        }

        if (actionType == SnippetActionType.TerminalCommand
            && !openTerminalWindow
            && string.IsNullOrWhiteSpace(terminalCommand))
        {
            return SnippetSaveValidationResult.Failure(TerminalCommandRequiredMessage);
        }

        var isMedia = actionType == SnippetActionType.MediaAction;
        var mediaProvider = isMedia ? selectedMediaProvider : (SnippetMediaProvider?)null;
        var mediaCommand = isMedia
            ? (SnippetMediaCommand?)MediaCommandRules.GetValidCommandForProvider(selectedMediaProvider, selectedMediaCommand)
            : null;

        var isTerminal = actionType == SnippetActionType.TerminalCommand;
        var normalizedTerminalCommand = isTerminal && !string.IsNullOrWhiteSpace(terminalCommand)
            ? terminalCommand.Trim()
            : null;
        var terminalShell = isTerminal ? selectedTerminalShell : (SnippetTerminalShell?)null;
        var normalizedTerminalWorkingDirectory = isTerminal && !string.IsNullOrWhiteSpace(terminalWorkingDirectory)
            ? terminalWorkingDirectory.Trim()
            : null;

        string? normalizedAdbDeviceIp = null;
        if (isTerminal && TerminalCommandParameterRules.IsAdbWirelessConnectCommand(normalizedTerminalCommand))
        {
            if (!TerminalCommandParameterRules.TryNormalizeAdbIp(adbDeviceIp, out var adbIp, out var adbError))
            {
                return SnippetSaveValidationResult.Failure(adbError ?? TerminalCommandParameterRules.EmptyAdbIpMessage);
            }

            normalizedAdbDeviceIp = adbIp;
        }
        else if (isTerminal && !string.IsNullOrWhiteSpace(adbDeviceIp))
        {
            normalizedAdbDeviceIp = adbDeviceIp.Trim();
        }

        return SnippetSaveValidationResult.Success(
            normalizedLaunchUrl,
            mediaProvider,
            mediaCommand,
            normalizedTerminalCommand,
            terminalShell,
            isTerminal && runAsAdministrator,
            isTerminal && openTerminalWindow,
            normalizedTerminalWorkingDirectory,
            normalizedAdbDeviceIp);
    }
}

public sealed record SnippetSaveValidationResult(
    bool Succeeded,
    string? ErrorMessage,
    string? NormalizedLaunchUrl,
    SnippetMediaProvider? MediaProvider,
    SnippetMediaCommand? MediaCommand,
    string? NormalizedTerminalCommand,
    SnippetTerminalShell? TerminalShell,
    bool RunAsAdministrator,
    bool OpenTerminalWindow,
    string? TerminalWorkingDirectory,
    string? AdbDeviceIp)
{
    public static SnippetSaveValidationResult Success(
        string? normalizedLaunchUrl,
        SnippetMediaProvider? mediaProvider,
        SnippetMediaCommand? mediaCommand,
        string? normalizedTerminalCommand,
        SnippetTerminalShell? terminalShell,
        bool runAsAdministrator,
        bool openTerminalWindow = false,
        string? terminalWorkingDirectory = null,
        string? adbDeviceIp = null) =>
        new(true, null, normalizedLaunchUrl, mediaProvider, mediaCommand,
            normalizedTerminalCommand, terminalShell, runAsAdministrator,
            openTerminalWindow, terminalWorkingDirectory, adbDeviceIp);

    public static SnippetSaveValidationResult Failure(string errorMessage) =>
        new(false, errorMessage, null, null, null, null, null, false, false, null, null);
}
