using DeckDeckDeck.App.Data;

namespace DeckDeckDeck.App.Services;

internal sealed record AppServices(
    CategoryService CategoryService,
    CategoryTransferService CategoryTransferService,
    BackupService? BackupService,
    DialogService DialogService,
    SettingsService SettingsService,
    SlotService SlotService,
    SnippetService SnippetService,
    SnippetTransferService SnippetTransferService,
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
        var backupService = new BackupService(fileStorageService, settingsService, loggingService);
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
            backupService,
            new DialogService(),
            settingsService,
            new SlotService(snippetImageService),
            snippetService,
            new SnippetTransferService(snippetService, settingsService, thumbnailService, loggingService),
            snippetImageService,
            new ClipboardPasteService(),
            new FileLaunchService(),
            new UrlLaunchService(),
            loggingService,
            thumbnailService);
    }
}
