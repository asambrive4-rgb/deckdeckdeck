// 역할: 카테고리 및 실행 항목 편집 화면에서 공통으로 사용되는 이미지 조작과 슬롯 전이(복사·이동 및 덮어쓰기 확인) 흐름을 보조합니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal static class SlotEditCommon
{
    public static IReadOnlyList<SlotTransferTargetItem> BuildTransferTargetSlots(
        IReadOnlyList<TransferTargetState> transferTargets) =>
        transferTargets
            .Select(target => new SlotTransferTargetItem(
                target.SlotKey,
                SlotRules.FormatTransferTargetLabel(target.SlotKey, target.ExistingTitle)))
            .ToList();

    public static bool TryChooseImage(
        bool canStoreImages,
        IDialogAdapter dialogService,
        Action<string> onPathSelected,
        out string? errorMessage)
    {
        if (!canStoreImages)
        {
            errorMessage = "이미지 저장소가 준비되지 않았습니다.";
            return false;
        }

        var path = dialogService.SelectImageFile();
        if (path is null)
        {
            errorMessage = null;
            return false;
        }

        onPathSelected(path);
        errorMessage = null;
        return true;
    }

    public static bool TryDropSingleImage(
        IReadOnlyList<string> sourcePaths,
        Action<string> onPathSelected,
        out string? errorMessage)
    {
        if (sourcePaths.Count != 1)
        {
            errorMessage = "이미지는 하나만 드롭해 주세요.";
            return false;
        }

        onPathSelected(sourcePaths[0]);
        errorMessage = null;
        return true;
    }

    public static (string ActionText, string Particle) GetTransferText(bool isCopy) =>
        isCopy ? ("복사", "에") : ("이동", "로");

    public static void ExecuteTransferWithConfirmation<TResult>(
        IDialogAdapter dialogService,
        IAppLogger? logger,
        SlotKey sourceSlotKey,
        string itemTypeName,
        bool isCopy,
        Func<bool, TResult> execute,
        Func<TResult, (bool NeedsConfirm, string? ExistingTitle, bool Succeeded, string? ErrorMessage)> checkResult,
        Func<string?, string> getConfirmMessage,
        Action<TResult> onSuccess,
        Action<string> setErrorMessage)
    {
        var (actionText, _) = GetTransferText(isCopy);

        try
        {
            var result = execute(false);
            var (needsConfirm, existingTitle, succeeded, errorMessage) = checkResult(result);

            if (needsConfirm)
            {
                if (!dialogService.Confirm($"{itemTypeName} {actionText}", getConfirmMessage(existingTitle)))
                {
                    return;
                }

                result = execute(true);
                (_, _, succeeded, errorMessage) = checkResult(result);
            }

            if (!succeeded)
            {
                setErrorMessage(errorMessage ?? string.Empty);
                return;
            }

            onSuccess(result);
        }
        catch (Exception ex)
        {
            setErrorMessage($"{itemTypeName} {actionText}에 실패했습니다.");
            var logItemName = itemTypeName == "카테고리" ? "Category" : "Snippet";
            logger?.Log($"{logItemName} {actionText} failed for slot {sourceSlotKey}.", ex);
        }
    }
}
