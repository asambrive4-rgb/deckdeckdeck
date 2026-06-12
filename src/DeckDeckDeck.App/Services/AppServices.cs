using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Services;

internal sealed record AppServices(
    CategoryService CategoryService,
    BackupService? BackupService,
    DialogService DialogService,
    SettingsService SettingsService,
    SnippetService SnippetService,
    SnippetImageService? SnippetImageService,
    IClipboardPasteService ClipboardPasteService,
    IFileLaunchService FileLaunchService,
    IUrlLaunchService UrlLaunchService,
    IMediaActionService MediaActionService,
    ISpotifyConnectionService SpotifyConnectionService,
    ISpotifyMediaActionService SpotifyMediaActionService,
    LoggingService? LoggingService,
    ThumbnailService? ThumbnailService,
    SlotGridViewModelFactory SlotGridViewModelFactory)
{
    public static AppServices CreateDefault()
    {
        var fileStorageService = new FileStorageService();
        fileStorageService.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(fileStorageService.DatabasePath);
        dbContextFactory.EnsureCreated();

        var categoryService = new CategoryService(dbContextFactory);
        var settingsService = new SettingsService(dbContextFactory);
        var loggingService = new LoggingService(fileStorageService);
        var backupService = new BackupService(fileStorageService, settingsService, loggingService);
        var thumbnailService = new ThumbnailService(fileStorageService);
        var snippetService = new SnippetService(dbContextFactory);
        var fileIconCacheService = new FileIconCacheService(
            fileStorageService,
            new ShellFileIconExtractor(),
            loggingService);
        var snippetImageService = new SnippetImageService(fileIconCacheService);
        var urlLaunchService = new UrlLaunchService();
        var spotifyConnectionService = new SpotifyConnectionService(settingsService, urlLaunchService);

        return new AppServices(
            categoryService,
            backupService,
            new DialogService(),
            settingsService,
            snippetService,
            snippetImageService,
            new ClipboardPasteService(),
            new FileLaunchService(),
            urlLaunchService,
            new MediaActionService(),
            spotifyConnectionService,
            new SpotifyMediaActionService(settingsService),
            loggingService,
            thumbnailService,
            new SlotGridViewModelFactory());
    }
}
