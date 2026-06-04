using DeckDeckDeck.App.Data;

namespace DeckDeckDeck.App.Services;

internal sealed record AppServices(
    CategoryService CategoryService,
    DialogService DialogService,
    SettingsService SettingsService,
    SlotService SlotService,
    SnippetService SnippetService,
    IClipboardPasteService ClipboardPasteService,
    LoggingService? LoggingService,
    ThumbnailService? ThumbnailService)
{
    public static AppServices CreateDefault()
    {
        var fileStorageService = new FileStorageService();
        fileStorageService.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(fileStorageService.DatabasePath);
        dbContextFactory.EnsureCreated();

        return new AppServices(
            new CategoryService(dbContextFactory),
            new DialogService(),
            new SettingsService(dbContextFactory),
            new SlotService(),
            new SnippetService(dbContextFactory),
            new ClipboardPasteService(),
            new LoggingService(fileStorageService),
            new ThumbnailService(fileStorageService));
    }
}
