using System.Windows;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public sealed class ClipboardPasteService : IClipboardPasteService
{
    private static readonly TimeSpan DefaultPasteDelay = TimeSpan.FromMilliseconds(80);

    private static readonly TimeSpan DefaultRestoreDelay = TimeSpan.FromMilliseconds(500);

    private readonly IClipboardService _clipboardService;
    private readonly IKeyboardInputService _keyboardInputService;
    private readonly IWindowFocusService _windowFocusService;
    private readonly TimeSpan _pasteDelay;
    private readonly TimeSpan _restoreDelay;

    public ClipboardPasteService()
        : this(new WpfClipboardService(), new KeyboardInputService(), new WindowFocusService(), DefaultRestoreDelay)
    {
    }

    public ClipboardPasteService(
        IClipboardService clipboardService,
        IKeyboardInputService keyboardInputService,
        IWindowFocusService windowFocusService,
        TimeSpan? restoreDelay = null,
        TimeSpan? pasteDelay = null)
    {
        _clipboardService = clipboardService;
        _keyboardInputService = keyboardInputService;
        _windowFocusService = windowFocusService;
        _pasteDelay = pasteDelay ?? DefaultPasteDelay;
        _restoreDelay = restoreDelay ?? DefaultRestoreDelay;
    }

    public async Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
    {
        if (targetWindowHandle == IntPtr.Zero)
        {
            return false;
        }

        IDataObject? backup = null;
        var shouldRestore = false;

        try
        {
            backup = _clipboardService.GetDataObject();
            _clipboardService.SetText(snippet.Content);
            shouldRestore = settings.RestoreClipboardAfterPaste && backup is not null;

            _windowFocusService.TryActivate(targetWindowHandle);
            await Task.Delay(_pasteDelay);

            return _keyboardInputService.SendCtrlV();
        }
        catch
        {
            return false;
        }
        finally
        {
            if (shouldRestore && backup is not null)
            {
                await Task.Delay(_restoreDelay);
                TryRestoreClipboard(backup);
            }
        }
    }

    private void TryRestoreClipboard(IDataObject backup)
    {
        try
        {
            _clipboardService.SetDataObject(backup);
        }
        catch
        {
            // Clipboard restore is best-effort; failures must not close the app.
        }
    }
}
