using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal sealed record MainViewModelDependencies(
    MainViewModelNavigatorDependencies NavigatorDependencies,
    ILoadSettingsUseCase LoadSettingsUseCase,
    SaveWindowPlacementUseCase SaveWindowPlacementUseCase,
    ResolveCategoryHotkeyUseCase ResolveCategoryHotkeyUseCase,
    LoadHotkeyActionsUseCase LoadHotkeyActionsUseCase,
    GetHotkeyActionByIdUseCase GetHotkeyActionByIdUseCase,
    PrepareSnippetActionUseCase PrepareSnippetActionUseCase,
    ExecuteSnippetActionUseCase ExecuteSnippetActionUseCase,
    IAppLogger? Logger,
    IAutoBackupCoordinator? AutoBackupCoordinator);

internal sealed record MainViewModelCallbacks(
    Func<IntPtr> GetPasteTargetWindowHandle,
    Action HideWindowAfterPaste,
    Action EnterEditMode,
    Func<Action> CreatePasteSelectionCompletion)
{
    public static MainViewModelCallbacks Empty { get; } = new(
        () => IntPtr.Zero,
        () => { },
        () => { },
        () => () => { });
}
