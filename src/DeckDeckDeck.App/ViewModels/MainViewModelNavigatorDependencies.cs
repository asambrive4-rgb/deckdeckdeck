using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

internal sealed record MainViewModelNavigatorDependencies(
    ICategoryRepository CategoryRepository,
    ISettingsRepository SettingsRepository,
    ISnippetRepository SnippetRepository,
    IDialogAdapter DialogAdapter,
    SlotGridViewModelFactory SlotGridViewModelFactory,
    IImageFileRepository? ImageFileRepository,
    IAppLogger? Logger,
    ISnippetImageResolver? SnippetImageResolver,
    IStoredImagePathResolver? StoredImagePathResolver,
    IBackupGateway? BackupGateway,
    ISpotifyConnectionUseCase SpotifyConnectionUseCase,
    IClipboardTextWriter ClipboardTextWriter);
