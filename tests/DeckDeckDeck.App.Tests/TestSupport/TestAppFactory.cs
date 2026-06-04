using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace DeckDeckDeck.App.Tests;

internal static class TestAppFactory
{
    public static TestServices CreateServices(string? appDataPath = null)
    {
        var storage = new FileStorageService(appDataPath ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(storage.DatabasePath);
        dbContextFactory.EnsureCreated();

        var settingsService = new SettingsService(dbContextFactory);
        settingsService.EnsureDefaults();

        return new TestServices(
            storage,
            new CategoryService(dbContextFactory),
            new SnippetService(dbContextFactory),
            settingsService,
            new LoggingService(storage),
            new ThumbnailService(storage));
    }

    public static MainViewModel CreateMainViewModel(
        TestServices services,
        IClipboardPasteService? clipboardPasteService = null,
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null,
        IFileLaunchService? fileLaunchService = null)
    {
        return new MainViewModel(
            services.CategoryService,
            new DialogService(),
            services.SettingsService,
            new SlotService(),
            services.SnippetService,
            clipboardPasteService,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            loggingService: services.LoggingService,
            thumbnailService: services.ThumbnailService,
            fileLaunchService: fileLaunchService);
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
    FileStorageService Storage,
    CategoryService CategoryService,
    SnippetService SnippetService,
    SettingsService SettingsService,
    LoggingService LoggingService,
    ThumbnailService ThumbnailService);

internal sealed class FakeClipboardService : IClipboardService
{
    public FakeClipboardService(IDataObject? backup)
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

internal sealed class FakeKeyboardInputService : IKeyboardInputService
{
    public bool SentCtrlV { get; private set; }

    public bool SendCtrlV()
    {
        SentCtrlV = true;
        return true;
    }
}

internal sealed class FakeWindowFocusService : IWindowFocusService
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

internal sealed class RecordingClipboardPasteService : IClipboardPasteService
{
    public List<PasteCall> Calls { get; } = [];

    public Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
    {
        Calls.Add(new PasteCall(snippet, targetWindowHandle, settings));
        return Task.FromResult(true);
    }
}

internal sealed record PasteCall(Snippet Snippet, IntPtr TargetWindowHandle, AppSettings Settings);

internal sealed class ThrowingClipboardPasteService : IClipboardPasteService
{
    public Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
    {
        return Task.FromException<bool>(new InvalidOperationException("Paste failed."));
    }
}

internal sealed class RecordingFileLaunchService : IFileLaunchService
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
