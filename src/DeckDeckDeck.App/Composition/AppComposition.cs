using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Composition;

internal sealed record AppComposition(
    CategoryRepository CategoryRepository,
    BackupGateway? BackupGateway,
    DialogAdapter DialogAdapter,
    SettingsRepository SettingsRepository,
    SnippetRepository SnippetRepository,
    SnippetImageResolver? SnippetImageResolver,
    IClipboardPasteGateway ClipboardPasteGateway,
    IFileLaunchGateway FileLaunchGatewayAdapter,
    IUrlLaunchGateway UrlLaunchGatewayAdapter,
    IMediaActionGateway SystemMediaActionGatewayAdapter,
    ITerminalCommandGateway TerminalCommandGatewayAdapter,
    ISpotifyConnectionGateway SpotifyConnectionGatewayAdapter,
    ISpotifyMediaActionGateway SpotifyMediaActionGatewayAdapter,
    IStoredImagePathResolver StoredImagePathResolver,
    FileLogger? FileLogger,
    ImageFileRepository? ImageFileRepository,
    SlotGridViewModelFactory SlotGridViewModelFactory,
    ExecuteSnippetActionUseCase ExecuteSnippetActionUseCase,
    ResolveCategoryHotkeyUseCase ResolveCategoryHotkeyUseCase,
    ISpotifyConnectionUseCase SpotifyConnectionUseCase,
    IClipboardAdapter ClipboardAdapter)
{
    public static AppComposition CreateDefault()
    {
        var appStoragePaths = new AppStoragePaths();
        appStoragePaths.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(appStoragePaths.DatabasePath);
        dbContextFactory.EnsureCreated();
        new StoredPathMigration(dbContextFactory, appStoragePaths).NormalizeManagedPaths();

        var categoryRepository = new CategoryRepository(dbContextFactory);
        var settingsRepository = new SettingsRepository(dbContextFactory);
        var fileLogger = new FileLogger(appStoragePaths);
        var storedImagePathResolver = new StoredImagePathResolver(appStoragePaths);
        var backupGateway = new BackupGateway(appStoragePaths, settingsRepository, fileLogger);
        var imageFileRepository = new ImageFileRepository(appStoragePaths);
        var snippetRepository = new SnippetRepository(dbContextFactory);
        var fileIconCacheRepository = new FileIconCacheRepository(
            appStoragePaths,
            new ShellFileIconExtractor(),
            fileLogger);
        var snippetImageResolver = new SnippetImageResolver(fileIconCacheRepository, storedImagePathResolver);
        var urlLaunchGatewayAdapter = new UrlLaunchGatewayAdapter();
        var spotifyConnectionGatewayAdapter = new SpotifyConnectionGatewayAdapter(settingsRepository, urlLaunchGatewayAdapter);
        var spotifyMediaActionGatewayAdapter = new SpotifyMediaActionGatewayAdapter(settingsRepository);
        var clipboardAdapter = new WpfClipboardAdapter();
        var fileLaunchGatewayAdapter = new FileLaunchGatewayAdapter();
        var terminalCommandGatewayAdapter = new TerminalCommandGatewayAdapter(appStoragePaths.TempPath);
        var systemMediaActionGatewayAdapter = new SystemMediaActionGatewayAdapter();
        var clipboardPasteGateway = new ClipboardPasteGateway(
            clipboardAdapter,
            new Win32KeyboardInputAdapter(),
            new Win32WindowFocusAdapter());

        return new AppComposition(
            categoryRepository,
            backupGateway,
            new DialogAdapter(),
            settingsRepository,
            snippetRepository,
            snippetImageResolver,
            clipboardPasteGateway,
            fileLaunchGatewayAdapter,
            urlLaunchGatewayAdapter,
            systemMediaActionGatewayAdapter,
            terminalCommandGatewayAdapter,
            spotifyConnectionGatewayAdapter,
            spotifyMediaActionGatewayAdapter,
            storedImagePathResolver,
            fileLogger,
            imageFileRepository,
            new SlotGridViewModelFactory(storedImagePathResolver, snippetImageResolver),
            CreateExecuteSnippetActionUseCase(
                clipboardPasteGateway,
                fileLaunchGatewayAdapter,
                urlLaunchGatewayAdapter,
                systemMediaActionGatewayAdapter,
                spotifyMediaActionGatewayAdapter,
                terminalCommandGatewayAdapter),
            new ResolveCategoryHotkeyUseCase(categoryRepository, settingsRepository),
            new SpotifyConnectionUseCase(
                settingsRepository,
                spotifyConnectionGatewayAdapter,
                urlLaunchGatewayAdapter),
            clipboardAdapter);
    }

    public static AppComposition Create(
        CategoryRepository categoryRepository,
        BackupGateway? backupGateway,
        DialogAdapter dialogAdapter,
        SettingsRepository settingsRepository,
        SnippetRepository snippetRepository,
        SnippetImageResolver? snippetImageResolver,
        IClipboardPasteGateway? clipboardPasteGateway,
        IFileLaunchGateway? fileLaunchGateway,
        IUrlLaunchGateway? urlLaunchGateway,
        IMediaActionGateway? mediaActionGateway,
        ITerminalCommandGateway? terminalCommandGateway,
        ISpotifyConnectionGateway? spotifyConnectionGateway,
        ISpotifyMediaActionGateway? spotifyMediaActionGateway,
        IStoredImagePathResolver? storedImagePathResolver,
        FileLogger? fileLogger,
        ImageFileRepository? imageFileRepository,
        SlotGridViewModelFactory slotGridViewModelFactory,
        IClipboardAdapter? clipboardAdapter)
    {
        var effectiveClipboardPasteGateway = clipboardPasteGateway ?? new ClipboardPasteGateway();
        var effectiveFileLaunchGatewayAdapter = fileLaunchGateway ?? new FileLaunchGatewayAdapter();
        var effectiveUrlLaunchGatewayAdapter = urlLaunchGateway ?? new UrlLaunchGatewayAdapter();
        var effectiveSystemMediaActionGatewayAdapter = mediaActionGateway ?? new SystemMediaActionGatewayAdapter();
        var effectiveTerminalCommandGatewayAdapter = terminalCommandGateway ?? new TerminalCommandGatewayAdapter();
        var effectiveSpotifyConnectionGatewayAdapter = spotifyConnectionGateway
            ?? new SpotifyConnectionGatewayAdapter(settingsRepository, effectiveUrlLaunchGatewayAdapter);
        var effectiveSpotifyMediaActionGatewayAdapter = spotifyMediaActionGateway
            ?? new SpotifyMediaActionGatewayAdapter(settingsRepository);
        var effectiveStoredImagePathResolver = storedImagePathResolver
            ?? new StoredImagePathResolver(new AppStoragePaths());
        var effectiveClipboardAdapter = clipboardAdapter ?? new WpfClipboardAdapter();

        return new AppComposition(
            categoryRepository,
            backupGateway,
            dialogAdapter,
            settingsRepository,
            snippetRepository,
            snippetImageResolver,
            effectiveClipboardPasteGateway,
            effectiveFileLaunchGatewayAdapter,
            effectiveUrlLaunchGatewayAdapter,
            effectiveSystemMediaActionGatewayAdapter,
            effectiveTerminalCommandGatewayAdapter,
            effectiveSpotifyConnectionGatewayAdapter,
            effectiveSpotifyMediaActionGatewayAdapter,
            effectiveStoredImagePathResolver,
            fileLogger,
            imageFileRepository,
            new SlotGridViewModelFactory(effectiveStoredImagePathResolver, snippetImageResolver),
            CreateExecuteSnippetActionUseCase(
                effectiveClipboardPasteGateway,
                effectiveFileLaunchGatewayAdapter,
                effectiveUrlLaunchGatewayAdapter,
                effectiveSystemMediaActionGatewayAdapter,
                effectiveSpotifyMediaActionGatewayAdapter,
                effectiveTerminalCommandGatewayAdapter),
            new ResolveCategoryHotkeyUseCase(categoryRepository, settingsRepository),
            new SpotifyConnectionUseCase(
                settingsRepository,
                effectiveSpotifyConnectionGatewayAdapter,
                effectiveUrlLaunchGatewayAdapter),
            effectiveClipboardAdapter);
    }

    private static ExecuteSnippetActionUseCase CreateExecuteSnippetActionUseCase(
        IClipboardPasteGateway clipboardPasteGateway,
        IFileLaunchGateway fileLaunchGateway,
        IUrlLaunchGateway urlLaunchGateway,
        IMediaActionGateway mediaActionGateway,
        ISpotifyMediaActionGateway spotifyMediaActionGateway,
        ITerminalCommandGateway terminalCommandGateway)
    {
        return new ExecuteSnippetActionUseCase(
            clipboardPasteGateway,
            fileLaunchGateway,
            urlLaunchGateway,
            mediaActionGateway,
            spotifyMediaActionGateway,
            terminalCommandGateway);
    }
}
