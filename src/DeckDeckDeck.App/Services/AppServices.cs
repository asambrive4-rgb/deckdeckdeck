using DeckDeckDeck.App.Data;

namespace DeckDeckDeck.App.Services;

internal sealed record AppServices(
    CategoryService CategoryService,
    CategoryTransferService CategoryTransferService,
    DialogService DialogService,
    SettingsService SettingsService,
    SlotService SlotService,
    SnippetService SnippetService,
    SnippetImageService? SnippetImageService,
    IClipboardPasteService ClipboardPasteService,
    IFileLaunchService FileLaunchService,
    IUrlLaunchService UrlLaunchService,
    LoggingService? LoggingService,
    ThumbnailService? ThumbnailService)
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
        var thumbnailService = new ThumbnailService(fileStorageService);
        var snippetService = new SnippetService(dbContextFactory);
        var fileIconCacheService = new FileIconCacheService(
            fileStorageService,
            new ShellFileIconExtractor(),
            loggingService);
        var snippetImageService = new SnippetImageService(snippetService, fileIconCacheService);

        return new AppServices(
            categoryService,
            new CategoryTransferService(categoryService, settingsService, thumbnailService, loggingService),
            new DialogService(),
            settingsService,
            new SlotService(snippetImageService),
            snippetService,
            snippetImageService,
            new ClipboardPasteService(),
            new FileLaunchService(),
            new UrlLaunchService(),
            loggingService,
            thumbnailService);
    }
}
