// 역할: 화면 이동 제어기(Navigator)가 필요로 하는 화면 생성 팩토리 및 의존성들을 묶어둔 데이터 구조입니다.
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal sealed record MainViewModelNavigatorDependencies(
    LoadHomeGridUseCase LoadHomeGridUseCase,
    LoadCategoryGridUseCase LoadCategoryGridUseCase,
    GetCategoryByIdUseCase GetCategoryByIdUseCase,
    LoadCategoryEditorStateUseCase LoadCategoryEditorStateUseCase,
    LoadSnippetEditorStateUseCase LoadSnippetEditorStateUseCase,
    LoadHotkeyActionsUseCase LoadHotkeyActionsUseCase,
    GetHotkeyActionByIdUseCase GetHotkeyActionByIdUseCase,
    LoadHotkeyActionEditorStateUseCase LoadHotkeyActionEditorStateUseCase,
    SaveCategoryUseCase SaveCategoryUseCase,
    DeleteCategoryUseCase DeleteCategoryUseCase,
    TransferCategoryUseCase TransferCategoryUseCase,
    MoveCategorySlotUseCase MoveCategorySlotUseCase,
    SaveSnippetUseCase SaveSnippetUseCase,
    DeleteSnippetUseCase DeleteSnippetUseCase,
    TransferSnippetUseCase TransferSnippetUseCase,
    MoveSnippetSlotUseCase MoveSnippetSlotUseCase,
    SaveHotkeyActionUseCase SaveHotkeyActionUseCase,
    SetHotkeyActionEnabledUseCase SetHotkeyActionEnabledUseCase,
    DeleteHotkeyActionUseCase DeleteHotkeyActionUseCase,
    ILoadSettingsUseCase LoadSettingsUseCase,
    ISaveAppPreferencesUseCase SaveAppPreferencesUseCase,
    ICreateManualBackupUseCase CreateManualBackupUseCase,
    IRestoreBackupUseCase RestoreBackupUseCase,
    IStartupRegistrationUseCase StartupRegistrationUseCase,
    IDialogAdapter DialogAdapter,
    SlotGridViewModelFactory SlotGridViewModelFactory,
    IImageFileRepository? ImageFileRepository,
    IAppLogger? Logger,
    ISnippetImageResolver? SnippetImageResolver,
    IStoredImagePathResolver? StoredImagePathResolver,
    ISpotifyConnectionUseCase SpotifyConnectionUseCase,
    IClipboardTextWriter ClipboardTextWriter,
    ISlotTimerCoordinator? SlotTimerCoordinator = null);
