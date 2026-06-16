using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal sealed record MainViewModelNavigatorDependencies(
    LoadHomeGridUseCase LoadHomeGridUseCase,
    LoadCategoryGridUseCase LoadCategoryGridUseCase,
    GetCategoryByIdUseCase GetCategoryByIdUseCase,
    LoadCategoryEditorStateUseCase LoadCategoryEditorStateUseCase,
    LoadSnippetEditorStateUseCase LoadSnippetEditorStateUseCase,
    SaveCategoryUseCase SaveCategoryUseCase,
    DeleteCategoryUseCase DeleteCategoryUseCase,
    TransferCategoryUseCase TransferCategoryUseCase,
    SaveSnippetUseCase SaveSnippetUseCase,
    DeleteSnippetUseCase DeleteSnippetUseCase,
    TransferSnippetUseCase TransferSnippetUseCase,
    ILoadSettingsUseCase LoadSettingsUseCase,
    ISaveSettingsUseCase SaveSettingsUseCase,
    ICreateManualBackupUseCase CreateManualBackupUseCase,
    IRestoreBackupUseCase RestoreBackupUseCase,
    IDialogAdapter DialogAdapter,
    SlotGridViewModelFactory SlotGridViewModelFactory,
    IImageFileRepository? ImageFileRepository,
    IAppLogger? Logger,
    ISnippetImageResolver? SnippetImageResolver,
    IStoredImagePathResolver? StoredImagePathResolver,
    ISpotifyConnectionUseCase SpotifyConnectionUseCase,
    IClipboardTextWriter ClipboardTextWriter);
