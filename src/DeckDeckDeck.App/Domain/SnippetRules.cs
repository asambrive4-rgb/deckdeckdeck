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
        bool runAsAdministrator = true)
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
            && string.IsNullOrWhiteSpace(terminalCommand))
        {
            return SnippetSaveValidationResult.Failure(TerminalCommandRequiredMessage);
        }

        var mediaProvider = actionType == SnippetActionType.MediaAction
            ? selectedMediaProvider
            : (SnippetMediaProvider?)null;
        var mediaCommand = actionType == SnippetActionType.MediaAction
            ? MediaCommandRules.GetValidCommandForProvider(selectedMediaProvider, selectedMediaCommand)
            : (SnippetMediaCommand?)null;
        var normalizedTerminalCommand = actionType == SnippetActionType.TerminalCommand
            ? terminalCommand?.Trim()
            : null;
        var terminalShell = actionType == SnippetActionType.TerminalCommand
            ? selectedTerminalShell
            : (SnippetTerminalShell?)null;
        var storedRunAsAdministrator = actionType == SnippetActionType.TerminalCommand
            && runAsAdministrator;

        return SnippetSaveValidationResult.Success(
            normalizedLaunchUrl,
            mediaProvider,
            mediaCommand,
            normalizedTerminalCommand,
            terminalShell,
            storedRunAsAdministrator);
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
    bool RunAsAdministrator)
{
    public static SnippetSaveValidationResult Success(
        string? normalizedLaunchUrl,
        SnippetMediaProvider? mediaProvider,
        SnippetMediaCommand? mediaCommand,
        string? normalizedTerminalCommand,
        SnippetTerminalShell? terminalShell,
        bool runAsAdministrator)
    {
        return new SnippetSaveValidationResult(
            true,
            null,
            normalizedLaunchUrl,
            mediaProvider,
            mediaCommand,
            normalizedTerminalCommand,
            terminalShell,
            runAsAdministrator);
    }

    public static SnippetSaveValidationResult Failure(string errorMessage)
    {
        return new SnippetSaveValidationResult(false, errorMessage, null, null, null, null, null, false);
    }
}
