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
    SaveSnippetUseCase SaveSnippetUseCase,
    DeleteSnippetUseCase DeleteSnippetUseCase,
    TransferSnippetUseCase TransferSnippetUseCase,
    SaveHotkeyActionUseCase SaveHotkeyActionUseCase,
    SetHotkeyActionEnabledUseCase SetHotkeyActionEnabledUseCase,
    DeleteHotkeyActionUseCase DeleteHotkeyActionUseCase,
    ILoadSettingsUseCase LoadSettingsUseCase,
    ISaveSettingsUseCase SaveSettingsUseCase,
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
    IClipboardTextWriter ClipboardTextWriter);
