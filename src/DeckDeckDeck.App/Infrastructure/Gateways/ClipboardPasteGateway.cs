using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.Windows;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class ClipboardPasteGateway : IClipboardPasteGateway
{
    private static readonly TimeSpan DefaultPasteDelay = TimeSpan.FromMilliseconds(80);

    private static readonly TimeSpan DefaultRestoreDelay = TimeSpan.FromMilliseconds(500);

    private readonly IClipboardAdapter _clipboardAdapter;
    private readonly IWin32KeyboardInputAdapter _keyboardInputAdapter;
    private readonly IWin32WindowFocusAdapter _windowFocusAdapter;
    private readonly TimeSpan _pasteDelay;
    private readonly TimeSpan _restoreDelay;

    public ClipboardPasteGateway()
        : this(new WpfClipboardAdapter(), new Win32KeyboardInputAdapter(), new Win32WindowFocusAdapter(), DefaultRestoreDelay)
    {
    }

    public ClipboardPasteGateway(
        IClipboardAdapter clipboardAdapter,
        IWin32KeyboardInputAdapter keyboardInputAdapter,
        IWin32WindowFocusAdapter windowFocusAdapter,
        TimeSpan? restoreDelay = null,
        TimeSpan? pasteDelay = null)
    {
        _clipboardAdapter = clipboardAdapter;
        _keyboardInputAdapter = keyboardInputAdapter;
        _windowFocusAdapter = windowFocusAdapter;
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
            backup = _clipboardAdapter.GetDataObject();
            _clipboardAdapter.SetText(snippet.Content);
            shouldRestore = settings.RestoreClipboardAfterPaste && backup is not null;

            _windowFocusAdapter.TryActivate(targetWindowHandle);
            await Task.Delay(_pasteDelay);

            return snippet.PasteShortcutMode == PasteShortcutMode.CtrlShiftV
                ? _keyboardInputAdapter.SendCtrlShiftV()
                : _keyboardInputAdapter.SendCtrlV();
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
            _clipboardAdapter.SetDataObject(backup);
        }
        catch
        {
            // Clipboard restore is best-effort; failures must not close the app.
        }
    }
}
