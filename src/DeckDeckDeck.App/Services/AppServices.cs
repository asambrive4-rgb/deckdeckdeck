using DeckDeckDeck.App.Data;

namespace DeckDeckDeck.App.Services;

internal sealed record AppServices(
    CategoryService CategoryService,
    CategoryTransferService CategoryTransferService,
    DialogService DialogService,
    SettingsService SettingsService,
    SlotService SlotService,
    SnippetService SnippetService,
    IClipboardPasteService ClipboardPasteService,
    IFileLaunchService FileLaunchService,
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

        return new AppServices(
            categoryService,
            new CategoryTransferService(categoryService, settingsService, thumbnailService, loggingService),
            new DialogService(),
            settingsService,
            new SlotService(),
            new SnippetService(dbContextFactory),
            new ClipboardPasteService(),
            new FileLaunchService(),
            loggingService,
            thumbnailService);
    }
}
