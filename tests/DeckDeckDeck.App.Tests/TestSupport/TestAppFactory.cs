using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace DeckDeckDeck.App.Tests;

internal static class TestAppFactory
{
    public static TestServices CreateServices(string? appDataPath = null)
    {
        var storage = new AppStoragePaths(appDataPath ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(storage.DatabasePath);
        dbContextFactory.EnsureCreated();
        new StoredPathMigration(dbContextFactory, storage).NormalizeManagedPaths();

        var settingsService = new SettingsRepository(dbContextFactory);
        settingsService.EnsureDefaults();
        var snippetService = new SnippetRepository(dbContextFactory);
        var loggingService = new FileLogger(storage);
        var storedImagePathResolver = new StoredImagePathResolver(storage);
        var backupService = new BackupGateway(storage, settingsService, loggingService);
        var fileIconCacheService = new FileIconCacheRepository(
            storage,
            new StubFileIconExtractor(),
            loggingService);
        var snippetImageService = new SnippetImageResolver(fileIconCacheService, storedImagePathResolver);

        return new TestServices(
            storage,
            new CategoryRepository(dbContextFactory),
            snippetService,
            settingsService,
            loggingService,
            backupService,
            new ImageFileRepository(storage),
            fileIconCacheService,
            snippetImageService,
            storedImagePathResolver);
    }

    public static MainViewModel CreateMainViewModel(
        TestServices services,
        IClipboardPasteGateway? clipboardPasteService = null,
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null,
        Func<Action>? createPasteSelectionCompletion = null,
        IFileLaunchGateway? fileLaunchService = null,
        IUrlLaunchGateway? urlLaunchService = null,
        IMediaActionGateway? mediaActionService = null,
        ITerminalCommandGateway? terminalCommandService = null,
        ISpotifyConnectionGateway? spotifyConnectionService = null,
        ISpotifyMediaActionGateway? spotifyMediaActionGatewayAdapter = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        return new MainViewModel(
            services.CategoryRepository,
            new DialogAdapter(),
            services.SettingsRepository,
            new SlotGridViewModelFactory(services.StoredImagePathResolver, services.SnippetImageResolver),
            services.SnippetRepository,
            clipboardPasteService ?? new RecordingClipboardPasteGateway(),
            fileLaunchService ?? new RecordingFileLaunchGatewayAdapter(),
            urlLaunchService ?? new RecordingUrlLaunchGatewayAdapter(),
            mediaActionService ?? new RecordingSystemMediaActionGatewayAdapter(),
            spotifyMediaActionGatewayAdapter ?? new RecordingSpotifyMediaActionGatewayAdapter(),
            terminalCommandService ?? new RecordingTerminalCommandGatewayAdapter(),
            new SpotifyConnectionUseCase(
                services.SettingsRepository,
                spotifyConnectionService
                    ?? new SpotifyConnectionGatewayAdapter(
                        services.SettingsRepository,
                        urlLaunchService ?? new RecordingUrlLaunchGatewayAdapter()),
                urlLaunchService ?? new RecordingUrlLaunchGatewayAdapter()),
            new FakeClipboardAdapter(null),
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            createPasteSelectionCompletion,
            loggingService: services.FileLogger,
            imageFileRepository: services.ImageFileRepository,
            snippetImageResolver: services.SnippetImageResolver,
            backupGateway: services.BackupGateway,
            autoBackupCoordinator: autoBackupCoordinator,
            storedImagePathResolver: services.StoredImagePathResolver);
    }

    public static string CreateTinyBmp(string directory)
    {
        var path = Path.Combine(directory, "source.bmp");
        byte[] bytes =
        [
            0x42, 0x4D, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00,
            0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF,
            0x00, 0xFF, 0xFF, 0xFF, 0x00, 0x00
        ];

        File.WriteAllBytes(path, bytes);

        return path;
    }

    public static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }

        return result!;
    }
}

internal sealed record TestServices(
    AppStoragePaths Storage,
    CategoryRepository CategoryRepository,
    SnippetRepository SnippetRepository,
    SettingsRepository SettingsRepository,
    FileLogger FileLogger,
    BackupGateway BackupGateway,
    ImageFileRepository ImageFileRepository,
    FileIconCacheRepository FileIconCacheRepository,
    SnippetImageResolver SnippetImageResolver,
    IStoredImagePathResolver StoredImagePathResolver);

internal sealed class StubFileIconExtractor : IFileIconExtractor
{
    public int CallCount { get; private set; }

    public bool ShouldSucceed { get; set; } = true;

    public bool TryExtractIcon(string sourcePath, string destinationPngPath)
    {
        CallCount++;
        if (!ShouldSucceed)
        {
            return false;
        }

        File.WriteAllBytes(destinationPngPath, TinyPng);
        return true;
    }

    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];
}

internal sealed class FakeClipboardAdapter : IClipboardAdapter
{
    public FakeClipboardAdapter(IDataObject? backup)
    {
        Backup = backup;
    }

    public IDataObject? Backup { get; }

    public IDataObject? Restored { get; private set; }

    public List<string> SetTexts { get; } = [];

    public IDataObject? GetDataObject()
    {
        return Backup;
    }

    public void SetText(string text)
    {
        SetTexts.Add(text);
    }

    public void SetDataObject(IDataObject dataObject)
    {
        Restored = dataObject;
    }
}

internal sealed class FakeWin32KeyboardInputAdapter : IWin32KeyboardInputAdapter
{
    public bool SentCtrlV { get; private set; }

    public bool SentCtrlShiftV { get; private set; }

    public bool SendCtrlV()
    {
        SentCtrlV = true;
        return true;
    }

    public bool SendCtrlShiftV()
    {
        SentCtrlShiftV = true;
        return true;
    }
}

internal sealed class FakeWin32WindowFocusAdapter : IWin32WindowFocusAdapter
{
    public bool CanActivate { get; set; } = true;

    public IntPtr ActivatedHandle { get; private set; }

    public IntPtr GetForegroundWindow()
    {
        return IntPtr.Zero;
    }

    public bool TryActivate(IntPtr windowHandle)
    {
        ActivatedHandle = windowHandle;
        return CanActivate;
    }
}

internal sealed class RecordingClipboardPasteGateway : IClipboardPasteGateway
{
    public List<PasteCall> Calls { get; } = [];

    public Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
    {
        Calls.Add(new PasteCall(snippet, targetWindowHandle, settings));
        return Task.FromResult(true);
    }
}

internal sealed record PasteCall(Snippet Snippet, IntPtr TargetWindowHandle, AppSettings Settings);

internal sealed class ThrowingClipboardPasteGateway : IClipboardPasteGateway
{
    public Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
    {
        return Task.FromException<bool>(new InvalidOperationException("Paste failed."));
    }
}

internal sealed class RecordingFileLaunchGatewayAdapter : IFileLaunchGateway
{
    public bool Result { get; set; } = true;

    public Exception? Exception { get; set; }

    public List<string> Paths { get; } = [];

    public bool TryLaunch(string path)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        Paths.Add(path);
        return Result;
    }
}

internal sealed class RecordingUrlLaunchGatewayAdapter : IUrlLaunchGateway
{
    public bool Result { get; set; } = true;

    public Exception? Exception { get; set; }

    public List<string> Urls { get; } = [];

    public bool TryLaunch(string url)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        Urls.Add(url);
        return Result;
    }
}

internal sealed class RecordingSystemMediaActionGatewayAdapter : IMediaActionGateway
{
    public bool Result { get; set; } = true;

    public Exception? Exception { get; set; }

    public List<SnippetMediaCommand> Commands { get; } = [];

    public bool TryExecute(SnippetMediaCommand command)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        Commands.Add(command);
        return Result;
    }
}

internal sealed class RecordingSpotifyMediaActionGatewayAdapter : ISpotifyMediaActionGateway
{
    public SpotifyMediaActionGatewayResult Result { get; set; } = new(true);

    public Exception? Exception { get; set; }

    public List<SnippetMediaCommand> Commands { get; } = [];

    public Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(
        SnippetMediaCommand command,
        CancellationToken cancellationToken = default)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        Commands.Add(command);
        return Task.FromResult(Result);
    }
}

internal sealed class RecordingTerminalCommandGatewayAdapter : ITerminalCommandGateway
{
    public bool Result { get; set; } = true;

    public Exception? Exception { get; set; }

    public List<TerminalCommandCall> Calls { get; } = [];

    public bool TryExecute(
        string command,
        SnippetTerminalShell shell,
        bool runAsAdministrator)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        Calls.Add(new TerminalCommandCall(command, shell, runAsAdministrator));
        return Result;
    }
}

internal sealed record TerminalCommandCall(
    string Command,
    SnippetTerminalShell Shell,
    bool RunAsAdministrator);

internal sealed class RecordingAutoBackupCoordinator : IAutoBackupCoordinator
{
    public int RequestCount { get; private set; }

    public void RequestAutoBackup()
    {
        RequestCount++;
    }
}

internal sealed class TestDataObject : IDataObject
{
    public object GetData(string format)
    {
        return string.Empty;
    }

    public object GetData(Type format)
    {
        return string.Empty;
    }

    public object GetData(string format, bool autoConvert)
    {
        return string.Empty;
    }

    public bool GetDataPresent(string format)
    {
        return false;
    }

    public bool GetDataPresent(Type format)
    {
        return false;
    }

    public bool GetDataPresent(string format, bool autoConvert)
    {
        return false;
    }

    public string[] GetFormats()
    {
        return [];
    }

    public string[] GetFormats(bool autoConvert)
    {
        return [];
    }

    public void SetData(object data)
    {
    }

    public void SetData(string format, object data)
    {
    }

    public void SetData(Type format, object data)
    {
    }

    public void SetData(string format, object data, bool autoConvert)
    {
    }
}

