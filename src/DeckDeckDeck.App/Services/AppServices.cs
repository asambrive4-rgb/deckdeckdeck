using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.UseCases;
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
    IStoredImagePathResolver StoredImagePathResolver,
    LoggingService? LoggingService,
    ThumbnailService? ThumbnailService,
    SlotGridViewModelFactory SlotGridViewModelFactory,
    ExecuteSnippetActionUseCase ExecuteSnippetActionUseCase,
    ResolveCategoryHotkeyUseCase ResolveCategoryHotkeyUseCase,
    ISpotifyConnectionUseCase SpotifyConnectionUseCase,
    IClipboardService ClipboardService)
{
    public static AppServices CreateDefault()
    {
        var fileStorageService = new FileStorageService();
        fileStorageService.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(fileStorageService.DatabasePath);
        dbContextFactory.EnsureCreated();
        new StoredPathMigrationService(dbContextFactory, fileStorageService).NormalizeManagedPaths();

        var categoryService = new CategoryService(dbContextFactory);
        var settingsService = new SettingsService(dbContextFactory);
        var loggingService = new LoggingService(fileStorageService);
        var storedImagePathResolver = new StoredImagePathResolver(fileStorageService);
        var backupService = new BackupService(fileStorageService, settingsService, loggingService);
        var thumbnailService = new ThumbnailService(fileStorageService);
        var snippetService = new SnippetService(dbContextFactory);
        var fileIconCacheService = new FileIconCacheService(
            fileStorageService,
            new ShellFileIconExtractor(),
            loggingService);
        var snippetImageService = new SnippetImageService(fileIconCacheService, storedImagePathResolver);
        var urlLaunchService = new UrlLaunchService();
        var spotifyConnectionService = new SpotifyConnectionService(settingsService, urlLaunchService);
        var spotifyMediaActionService = new SpotifyMediaActionService(settingsService);
        var clipboardService = new WpfClipboardService();
        var fileLaunchService = new FileLaunchService();
        var mediaActionService = new MediaActionService();
        var clipboardPasteService = new ClipboardPasteService(
            clipboardService,
            new KeyboardInputService(),
            new WindowFocusService());

        return new AppServices(
            categoryService,
            backupService,
            new DialogService(),
            settingsService,
            snippetService,
            snippetImageService,
            clipboardPasteService,
            fileLaunchService,
            urlLaunchService,
            mediaActionService,
            spotifyConnectionService,
            spotifyMediaActionService,
            storedImagePathResolver,
            loggingService,
            thumbnailService,
            new SlotGridViewModelFactory(storedImagePathResolver),
            CreateExecuteSnippetActionUseCase(
                clipboardPasteService,
                fileLaunchService,
                urlLaunchService,
                mediaActionService,
                spotifyMediaActionService),
            new ResolveCategoryHotkeyUseCase(categoryService, settingsService),
            new SpotifyConnectionUseCase(
                settingsService,
                new SpotifyConnectionGatewayAdapter(spotifyConnectionService),
                urlLaunchService),
            clipboardService);
    }

    public static AppServices Create(
        CategoryService categoryService,
        BackupService? backupService,
        DialogService dialogService,
        SettingsService settingsService,
        SnippetService snippetService,
        SnippetImageService? snippetImageService,
        IClipboardPasteService? clipboardPasteService,
        IFileLaunchService? fileLaunchService,
        IUrlLaunchService? urlLaunchService,
        IMediaActionService? mediaActionService,
        ISpotifyConnectionService? spotifyConnectionService,
        ISpotifyMediaActionService? spotifyMediaActionService,
        IStoredImagePathResolver? storedImagePathResolver,
        LoggingService? loggingService,
        ThumbnailService? thumbnailService,
        SlotGridViewModelFactory slotGridViewModelFactory,
        IClipboardService? clipboardService)
    {
        var effectiveClipboardPasteService = clipboardPasteService ?? new ClipboardPasteService();
        var effectiveFileLaunchService = fileLaunchService ?? new FileLaunchService();
        var effectiveUrlLaunchService = urlLaunchService ?? new UrlLaunchService();
        var effectiveMediaActionService = mediaActionService ?? new MediaActionService();
        var effectiveSpotifyConnectionService = spotifyConnectionService
            ?? new SpotifyConnectionService(settingsService, effectiveUrlLaunchService);
        var effectiveSpotifyMediaActionService = spotifyMediaActionService
            ?? new SpotifyMediaActionService(settingsService);
        var effectiveStoredImagePathResolver = storedImagePathResolver
            ?? new StoredImagePathResolver(new FileStorageService());
        var effectiveClipboardService = clipboardService ?? new WpfClipboardService();

        return new AppServices(
            categoryService,
            backupService,
            dialogService,
            settingsService,
            snippetService,
            snippetImageService,
            effectiveClipboardPasteService,
            effectiveFileLaunchService,
            effectiveUrlLaunchService,
            effectiveMediaActionService,
            effectiveSpotifyConnectionService,
            effectiveSpotifyMediaActionService,
            effectiveStoredImagePathResolver,
            loggingService,
            thumbnailService,
            slotGridViewModelFactory,
            CreateExecuteSnippetActionUseCase(
                effectiveClipboardPasteService,
                effectiveFileLaunchService,
                effectiveUrlLaunchService,
                effectiveMediaActionService,
                effectiveSpotifyMediaActionService),
            new ResolveCategoryHotkeyUseCase(categoryService, settingsService),
            new SpotifyConnectionUseCase(
                settingsService,
                new SpotifyConnectionGatewayAdapter(effectiveSpotifyConnectionService),
                effectiveUrlLaunchService),
            effectiveClipboardService);
    }

    private static ExecuteSnippetActionUseCase CreateExecuteSnippetActionUseCase(
        IClipboardPasteService clipboardPasteService,
        IFileLaunchService fileLaunchService,
        IUrlLaunchService urlLaunchService,
        IMediaActionService mediaActionService,
        ISpotifyMediaActionService spotifyMediaActionService)
    {
        return new ExecuteSnippetActionUseCase(
            clipboardPasteService,
            fileLaunchService,
            urlLaunchService,
            mediaActionService,
            new SpotifyMediaActionGatewayAdapter(spotifyMediaActionService));
    }
}
