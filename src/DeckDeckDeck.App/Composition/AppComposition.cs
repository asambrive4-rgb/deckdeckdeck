using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Diagnostics;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;

namespace DeckDeckDeck.App.Composition;

/// <summary>
/// Storage/DB bootstrap completed before (or off) the UI thread so the shell can show earlier.
/// </summary>
internal sealed class AppCompositionBootstrap
{
    public AppCompositionBootstrap(
        AppStoragePaths storagePaths,
        AppDbContextFactory dbContextFactory,
        FileLogger fileLogger)
    {
        StoragePaths = storagePaths;
        DbContextFactory = dbContextFactory;
        FileLogger = fileLogger;
    }

    public AppStoragePaths StoragePaths { get; }

    public AppDbContextFactory DbContextFactory { get; }

    public FileLogger FileLogger { get; }
}

internal sealed record AppComposition(
    CategoryRepository CategoryRepository,
    BackupGateway? BackupGateway,
    DialogAdapter DialogAdapter,
    SettingsRepository SettingsRepository,
    SnippetRepository SnippetRepository,
    HotkeyActionRepository HotkeyActionRepository,
    SnippetImageResolver? SnippetImageResolver,
    IClipboardPasteGateway ClipboardPasteGateway,
    IFilePasteGateway FilePasteGateway,
    IFileLaunchGateway FileLaunchGatewayAdapter,
    IUrlLaunchGateway UrlLaunchGatewayAdapter,
    IMediaActionGateway SystemMediaActionGatewayAdapter,
    ITerminalCommandGateway TerminalCommandGatewayAdapter,
    ISpotifyConnectionGateway SpotifyConnectionGatewayAdapter,
    ISpotifyMediaActionGateway SpotifyMediaActionGatewayAdapter,
    IStartupRegistrationGateway StartupRegistrationGateway,
    IStoredImagePathResolver StoredImagePathResolver,
    FileLogger? FileLogger,
    ImageFileRepository? ImageFileRepository,
    SlotGridViewModelFactory SlotGridViewModelFactory,
    ExecuteSnippetActionUseCase ExecuteSnippetActionUseCase,
    ResolveCategoryHotkeyUseCase ResolveCategoryHotkeyUseCase,
    ISpotifyConnectionUseCase SpotifyConnectionUseCase,
    IClipboardAdapter ClipboardAdapter)
{
    public static AppComposition CreateDefault(StartupTimingLog? startupTiming = null)
    {
        var bootstrap = PrepareStorageAndDatabase(startupTiming);
        return CreateFromBootstrap(bootstrap, startupTiming);
    }

    /// <summary>
    /// Heavy I/O: directories, SQLite ensure, one-time path migration.
    /// Safe to run on a background thread before UI composition.
    /// </summary>
    public static AppCompositionBootstrap PrepareStorageAndDatabase(StartupTimingLog? startupTiming = null)
    {
        AppStoragePaths appStoragePaths;
        using (startupTiming?.Measure("storage prepare"))
        {
            appStoragePaths = new AppStoragePaths();
            appStoragePaths.EnsureCreated();
        }

        AppDbContextFactory dbContextFactory;
        using (startupTiming?.Measure("database prepare"))
        {
            dbContextFactory = new AppDbContextFactory(appStoragePaths.DatabasePath);
            dbContextFactory.EnsureCreated();
        }

        FileLogger fileLogger;
        using (startupTiming?.Measure("startup maintenance"))
        {
            fileLogger = new FileLogger(appStoragePaths);
            new StartupMaintenanceUseCase(
                new StartupMaintenanceStateRepository(dbContextFactory),
                new StoredPathMigration(dbContextFactory, appStoragePaths))
                .Execute();
        }

        return new AppCompositionBootstrap(appStoragePaths, dbContextFactory, fileLogger);
    }

    /// <summary>
    /// Builds service graph from a prepared bootstrap. Prefer UI thread for WPF adapters.
    /// </summary>
    public static AppComposition CreateFromBootstrap(
        AppCompositionBootstrap bootstrap,
        StartupTimingLog? startupTiming = null)
    {
        var appStoragePaths = bootstrap.StoragePaths;
        var dbContextFactory = bootstrap.DbContextFactory;
        var fileLogger = bootstrap.FileLogger;

        CategoryRepository categoryRepository;
        SettingsRepository settingsRepository;
        SnippetRepository snippetRepository;
        HotkeyActionRepository hotkeyActionRepository;
        IStoredImagePathResolver storedImagePathResolver;
        SlotGridViewModelFactory slotGridViewModelFactory;
        SnippetImageResolver snippetImageResolver;
        ClipboardPasteGateway clipboardPasteGateway;
        IFileLaunchGateway fileLaunchGatewayAdapter;
        IUrlLaunchGateway urlLaunchGatewayAdapter;
        IMediaActionGateway systemMediaActionGatewayAdapter;
        ITerminalCommandGateway terminalCommandGatewayAdapter;
        IClipboardAdapter clipboardAdapter;
        ISpotifyMediaActionGateway spotifyMediaActionGatewayAdapter;
        ExecuteSnippetActionUseCase executeSnippetActionUseCase;
        ResolveCategoryHotkeyUseCase resolveCategoryHotkeyUseCase;

        using (startupTiming?.Measure("core composition"))
        {
            categoryRepository = new CategoryRepository(dbContextFactory);
            settingsRepository = new SettingsRepository(dbContextFactory);
            snippetRepository = new SnippetRepository(dbContextFactory);
            hotkeyActionRepository = new HotkeyActionRepository(dbContextFactory);
            storedImagePathResolver = new StoredImagePathResolver(appStoragePaths);

            var fileIconCacheRepository = new FileIconCacheRepository(
                appStoragePaths,
                new ShellFileIconExtractor(),
                fileLogger);
            snippetImageResolver = new SnippetImageResolver(fileIconCacheRepository, storedImagePathResolver);
            slotGridViewModelFactory = new SlotGridViewModelFactory(storedImagePathResolver, snippetImageResolver);

            clipboardAdapter = new WpfClipboardAdapter();
            fileLaunchGatewayAdapter = new FileLaunchGatewayAdapter();
            urlLaunchGatewayAdapter = new UrlLaunchGatewayAdapter();
            terminalCommandGatewayAdapter = new TerminalCommandGatewayAdapter(appStoragePaths.TempPath);
            systemMediaActionGatewayAdapter = new SystemMediaActionGatewayAdapter();
            clipboardPasteGateway = new ClipboardPasteGateway(
                clipboardAdapter,
                new Win32KeyboardInputAdapter(),
                new Win32WindowFocusAdapter());

            // Required by action execution on home; keep with core.
            spotifyMediaActionGatewayAdapter = new SpotifyMediaActionGatewayAdapter(settingsRepository);
            resolveCategoryHotkeyUseCase = new ResolveCategoryHotkeyUseCase(
                categoryRepository,
                settingsRepository);
        }

        BackupGateway backupGateway;
        ImageFileRepository imageFileRepository;
        ISpotifyConnectionGateway spotifyConnectionGatewayAdapter;
        IStartupRegistrationGateway startupRegistrationGateway;
        ISpotifyConnectionUseCase spotifyConnectionUseCase;
        DialogAdapter dialogAdapter;

        // Secondary services timed separately for L1; still assembled before first home for a complete graph.
        using (startupTiming?.Measure("deferred composition"))
        {
            backupGateway = new BackupGateway(appStoragePaths, settingsRepository, fileLogger);
            imageFileRepository = new ImageFileRepository(appStoragePaths);
            spotifyConnectionGatewayAdapter = new SpotifyConnectionGatewayAdapter(urlLaunchGatewayAdapter);
            startupRegistrationGateway = new WindowsStartupRegistrationGateway();
            spotifyConnectionUseCase = new SpotifyConnectionUseCase(
                settingsRepository,
                spotifyConnectionGatewayAdapter,
                urlLaunchGatewayAdapter);
            dialogAdapter = new DialogAdapter();
            executeSnippetActionUseCase = CreateExecuteSnippetActionUseCase(
                clipboardPasteGateway,
                fileLaunchGatewayAdapter,
                urlLaunchGatewayAdapter,
                systemMediaActionGatewayAdapter,
                spotifyMediaActionGatewayAdapter,
                terminalCommandGatewayAdapter,
                clipboardPasteGateway,
                dialogAdapter);
        }

        return new AppComposition(
            categoryRepository,
            backupGateway,
            dialogAdapter,
            settingsRepository,
            snippetRepository,
            hotkeyActionRepository,
            snippetImageResolver,
            clipboardPasteGateway,
            clipboardPasteGateway,
            fileLaunchGatewayAdapter,
            urlLaunchGatewayAdapter,
            systemMediaActionGatewayAdapter,
            terminalCommandGatewayAdapter,
            spotifyConnectionGatewayAdapter,
            spotifyMediaActionGatewayAdapter,
            startupRegistrationGateway,
            storedImagePathResolver,
            fileLogger,
            imageFileRepository,
            slotGridViewModelFactory,
            executeSnippetActionUseCase,
            resolveCategoryHotkeyUseCase,
            spotifyConnectionUseCase,
            clipboardAdapter);
    }

    public static AppComposition Create(
        CategoryRepository categoryRepository,
        BackupGateway? backupGateway,
        DialogAdapter dialogAdapter,
        SettingsRepository settingsRepository,
        SnippetRepository snippetRepository,
        HotkeyActionRepository hotkeyActionRepository,
        SnippetImageResolver? snippetImageResolver,
        IClipboardPasteGateway? clipboardPasteGateway,
        IFileLaunchGateway? fileLaunchGateway,
        IUrlLaunchGateway? urlLaunchGateway,
        IMediaActionGateway? mediaActionGateway,
        ITerminalCommandGateway? terminalCommandGateway,
        ISpotifyConnectionGateway? spotifyConnectionGateway,
        ISpotifyMediaActionGateway? spotifyMediaActionGateway,
        IStartupRegistrationGateway? startupRegistrationGateway,
        IStoredImagePathResolver? storedImagePathResolver,
        FileLogger? fileLogger,
        ImageFileRepository? imageFileRepository,
        SlotGridViewModelFactory slotGridViewModelFactory,
        IClipboardAdapter? clipboardAdapter,
        IFilePasteGateway? filePasteGateway = null)
    {
        var effectiveClipboardPasteGateway = clipboardPasteGateway ?? new ClipboardPasteGateway();
        var effectiveFileLaunchGatewayAdapter = fileLaunchGateway ?? new FileLaunchGatewayAdapter();
        var effectiveUrlLaunchGatewayAdapter = urlLaunchGateway ?? new UrlLaunchGatewayAdapter();
        var effectiveSystemMediaActionGatewayAdapter = mediaActionGateway ?? new SystemMediaActionGatewayAdapter();
        var effectiveTerminalCommandGatewayAdapter = terminalCommandGateway ?? new TerminalCommandGatewayAdapter();
        var effectiveSpotifyConnectionGatewayAdapter = spotifyConnectionGateway
            ?? new SpotifyConnectionGatewayAdapter(effectiveUrlLaunchGatewayAdapter);
        var effectiveSpotifyMediaActionGatewayAdapter = spotifyMediaActionGateway
            ?? new SpotifyMediaActionGatewayAdapter(settingsRepository);
        var effectiveStartupRegistrationGateway = startupRegistrationGateway
            ?? new WindowsStartupRegistrationGateway();
        var effectiveStoredImagePathResolver = storedImagePathResolver
            ?? new StoredImagePathResolver(new AppStoragePaths());
        var effectiveClipboardAdapter = clipboardAdapter ?? new WpfClipboardAdapter();
        var effectiveFilePasteGateway = filePasteGateway
            ?? effectiveClipboardPasteGateway as IFilePasteGateway
            ?? new ClipboardPasteGateway(
                effectiveClipboardAdapter,
                new Win32KeyboardInputAdapter(),
                new Win32WindowFocusAdapter());

        return new AppComposition(
            categoryRepository,
            backupGateway,
            dialogAdapter,
            settingsRepository,
            snippetRepository,
            hotkeyActionRepository,
            snippetImageResolver,
            effectiveClipboardPasteGateway,
            effectiveFilePasteGateway,
            effectiveFileLaunchGatewayAdapter,
            effectiveUrlLaunchGatewayAdapter,
            effectiveSystemMediaActionGatewayAdapter,
            effectiveTerminalCommandGatewayAdapter,
            effectiveSpotifyConnectionGatewayAdapter,
            effectiveSpotifyMediaActionGatewayAdapter,
            effectiveStartupRegistrationGateway,
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
                effectiveTerminalCommandGatewayAdapter,
                effectiveFilePasteGateway,
                dialogAdapter),
            new ResolveCategoryHotkeyUseCase(categoryRepository, settingsRepository),
            new SpotifyConnectionUseCase(
                settingsRepository,
                effectiveSpotifyConnectionGatewayAdapter,
                effectiveUrlLaunchGatewayAdapter),
            effectiveClipboardAdapter);
    }

    public MainViewModelDependencies CreateMainViewModelDependencies(
        IAutoBackupCoordinator? autoBackupCoordinator,
        Func<ExecutableAction, Task> executeActionAsync,
        IBluetoothAudioStatusGateway? bluetoothAudioStatusGateway = null)
    {
        var loadSettingsUseCase = new LoadSettingsUseCase(SettingsRepository);
        var saveCategoryUseCase = new SaveCategoryUseCase(
            CategoryRepository,
            SettingsRepository,
            autoBackupCoordinator);
        var saveSnippetUseCase = new SaveSnippetUseCase(
            SnippetRepository,
            SettingsRepository,
            autoBackupCoordinator);
        var saveHotkeyActionUseCase = new SaveHotkeyActionUseCase(
            HotkeyActionRepository,
            autoBackupCoordinator);
        var setHotkeyActionEnabledUseCase = new SetHotkeyActionEnabledUseCase(
            HotkeyActionRepository,
            autoBackupCoordinator);
        var startupRegistrationUseCase = new StartupRegistrationUseCase(StartupRegistrationGateway);
        var saveAppPreferencesUseCase = new SaveAppPreferencesUseCase(
            new SaveSettingsUseCase(SettingsRepository, BackupGateway, autoBackupCoordinator),
            startupRegistrationUseCase);
        var navigatorDependencies = new MainViewModelNavigatorDependencies(
            new LoadHomeGridUseCase(CategoryRepository, SettingsRepository),
            new LoadCategoryGridUseCase(SnippetRepository, SettingsRepository),
            new GetCategoryByIdUseCase(CategoryRepository),
            new LoadCategoryEditorStateUseCase(CategoryRepository, SettingsRepository),
            new LoadSnippetEditorStateUseCase(SnippetRepository, SettingsRepository),
            new LoadHotkeyActionsUseCase(HotkeyActionRepository),
            new GetHotkeyActionByIdUseCase(HotkeyActionRepository),
            new LoadHotkeyActionEditorStateUseCase(SettingsRepository),
            saveCategoryUseCase,
            new DeleteCategoryUseCase(
                CategoryRepository,
                ImageFileRepository,
                autoBackupCoordinator),
            new TransferCategoryUseCase(
                CategoryRepository,
                SettingsRepository,
                saveCategoryUseCase,
                ImageFileRepository,
                autoBackupCoordinator),
            new MoveCategorySlotUseCase(
                CategoryRepository,
                SettingsRepository,
                ImageFileRepository,
                autoBackupCoordinator),
            saveSnippetUseCase,
            new DeleteSnippetUseCase(
                SnippetRepository,
                ImageFileRepository,
                autoBackupCoordinator),
            new TransferSnippetUseCase(
                SnippetRepository,
                SettingsRepository,
                saveSnippetUseCase,
                ImageFileRepository,
                autoBackupCoordinator),
            new MoveSnippetSlotUseCase(
                SnippetRepository,
                SettingsRepository,
                ImageFileRepository,
                autoBackupCoordinator),
            saveHotkeyActionUseCase,
            setHotkeyActionEnabledUseCase,
            new DeleteHotkeyActionUseCase(
                HotkeyActionRepository,
                ImageFileRepository,
                autoBackupCoordinator),
            loadSettingsUseCase,
            saveAppPreferencesUseCase,
            new CreateManualBackupUseCase(BackupGateway),
            new RestoreBackupUseCase(BackupGateway),
            startupRegistrationUseCase,
            DialogAdapter,
            SlotGridViewModelFactory,
            ImageFileRepository,
            FileLogger,
            SnippetImageResolver,
            StoredImagePathResolver,
            SpotifyConnectionUseCase,
            ClipboardAdapter);

        return new MainViewModelDependencies(
            navigatorDependencies,
            loadSettingsUseCase,
            new SaveWindowPlacementUseCase(SettingsRepository, FileLogger),
            ResolveCategoryHotkeyUseCase,
            new LoadDirectHotkeyRegistrationsUseCase(HotkeyActionRepository),
            new ResolveExecutableHotkeyActionUseCase(HotkeyActionRepository),
            executeActionAsync,
            FileLogger,
            autoBackupCoordinator,
            bluetoothAudioStatusGateway ?? new WindowsBluetoothAudioStatusGateway(FileLogger));
    }

    private static ExecuteSnippetActionUseCase CreateExecuteSnippetActionUseCase(
        IClipboardPasteGateway clipboardPasteGateway,
        IFileLaunchGateway fileLaunchGateway,
        IUrlLaunchGateway urlLaunchGateway,
        IMediaActionGateway mediaActionGateway,
        ISpotifyMediaActionGateway spotifyMediaActionGateway,
        ITerminalCommandGateway terminalCommandGateway,
        IFilePasteGateway filePasteGateway,
        IDialogAdapter dialogAdapter)
    {
        return new ExecuteSnippetActionUseCase(
            clipboardPasteGateway,
            fileLaunchGateway,
            urlLaunchGateway,
            mediaActionGateway,
            spotifyMediaActionGateway,
            terminalCommandGateway,
            filePasteGateway,
            dialogAdapter);
    }
}
