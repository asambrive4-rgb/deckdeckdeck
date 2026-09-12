// 역할: 메인 화면 모델이 동작하는 데 필요한 유스케이스 및 보조 컴포넌트들을 한곳에 묶어 전달하는 데이터 구조입니다.
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal sealed record MainViewModelDependencies(
    MainViewModelNavigatorDependencies NavigatorDependencies,
    ILoadSettingsUseCase LoadSettingsUseCase,
    SaveWindowPlacementUseCase SaveWindowPlacementUseCase,
    ResolveCategoryHotkeyUseCase ResolveCategoryHotkeyUseCase,
    LoadDirectHotkeyRegistrationsUseCase LoadDirectHotkeyRegistrationsUseCase,
    ResolveExecutableHotkeyActionUseCase ResolveExecutableHotkeyActionUseCase,
    Func<ExecutableAction, Task> ExecuteActionAsync,
    IAppLogger? Logger,
    IAutoBackupCoordinator? AutoBackupCoordinator,
    IBluetoothAudioStatusGateway BluetoothAudioStatusGateway);

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
