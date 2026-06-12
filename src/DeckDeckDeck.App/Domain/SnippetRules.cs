using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public static class SnippetRules
{
    public const string TitleRequiredMessage = "슬롯 이름을 입력해 주세요.";
    public const string PasteContentRequiredMessage = "붙여넣을 문구를 입력해 주세요.";
    public const string LaunchPathRequiredMessage = "실행할 파일, 폴더 또는 바로 가기를 선택해 주세요.";
    public const string LaunchUrlRequiredMessage = "열 웹페이지 주소를 http 또는 https 주소로 입력해 주세요.";

    public static SnippetSaveValidationResult ValidateForSave(
        string? title,
        string? content,
        SnippetActionType actionType,
        string? launchPath,
        string? launchUrl,
        SnippetMediaProvider selectedMediaProvider,
        SnippetMediaCommand selectedMediaCommand)
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

        var mediaProvider = actionType == SnippetActionType.MediaAction
            ? selectedMediaProvider
            : (SnippetMediaProvider?)null;
        var mediaCommand = actionType == SnippetActionType.MediaAction
            ? MediaCommandRules.GetValidCommandForProvider(selectedMediaProvider, selectedMediaCommand)
            : (SnippetMediaCommand?)null;

        return SnippetSaveValidationResult.Success(normalizedLaunchUrl, mediaProvider, mediaCommand);
    }
}

public sealed record SnippetSaveValidationResult(
    bool Succeeded,
    string? ErrorMessage,
    string? NormalizedLaunchUrl,
    SnippetMediaProvider? MediaProvider,
    SnippetMediaCommand? MediaCommand)
{
    public static SnippetSaveValidationResult Success(
        string? normalizedLaunchUrl,
        SnippetMediaProvider? mediaProvider,
        SnippetMediaCommand? mediaCommand)
    {
        return new SnippetSaveValidationResult(
            true,
            null,
            normalizedLaunchUrl,
            mediaProvider,
            mediaCommand);
    }

    public static SnippetSaveValidationResult Failure(string errorMessage)
    {
        return new SnippetSaveValidationResult(false, errorMessage, null, null, null);
    }
}
