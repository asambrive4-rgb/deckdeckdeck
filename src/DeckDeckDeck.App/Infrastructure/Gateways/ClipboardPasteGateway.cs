using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.IO;
using System.Windows;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class ClipboardPasteGateway : IClipboardPasteGateway, IFilePasteGateway
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

    public Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
    {
        return PasteActionAsync(ExecutableAction.FromSnippet(snippet), targetWindowHandle, settings);
    }

    public async Task<bool> PasteActionAsync(ExecutableAction action, IntPtr targetWindowHandle, AppSettings settings)
    {
        var result = await PasteClipboardAsync(
            () => _clipboardAdapter.SetText(action.Content),
            targetWindowHandle,
            settings,
            action.PasteShortcutMode);

        return result.Succeeded;
    }

    public async Task<FilePasteGatewayResult> PasteFileAsync(
        string filePath,
        IntPtr targetWindowHandle,
        AppSettings settings)
    {
        if (!File.Exists(filePath))
        {
            return FilePasteGatewayResult.FileNotFound();
        }

        var result = await PasteClipboardAsync(
            () => _clipboardAdapter.SetFileDropList(filePath),
            targetWindowHandle,
            settings,
            PasteShortcutMode.CtrlV);

        return result.Succeeded
            ? FilePasteGatewayResult.Success()
            : FilePasteGatewayResult.Failure(result.Exception);
    }

    private async Task<ClipboardPasteAttempt> PasteClipboardAsync(
        Action setClipboard,
        IntPtr targetWindowHandle,
        AppSettings settings,
        PasteShortcutMode pasteShortcutMode)
    {
        if (targetWindowHandle == IntPtr.Zero)
        {
            return ClipboardPasteAttempt.Failure();
        }

        IDataObject? backup = null;
        var shouldRestore = false;

        try
        {
            backup = _clipboardAdapter.GetDataObject();
            setClipboard();
            shouldRestore = settings.RestoreClipboardAfterPaste && backup is not null;

            _windowFocusAdapter.TryActivate(targetWindowHandle);
            await Task.Delay(_pasteDelay);

            var pasted = pasteShortcutMode == PasteShortcutMode.CtrlShiftV
                ? _keyboardInputAdapter.SendCtrlShiftV()
                : _keyboardInputAdapter.SendCtrlV();

            return pasted
                ? ClipboardPasteAttempt.Success()
                : ClipboardPasteAttempt.Failure();
        }
        catch (Exception ex)
        {
            return ClipboardPasteAttempt.Failure(ex);
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

    private sealed record ClipboardPasteAttempt(bool Succeeded, Exception? Exception = null)
    {
        public static ClipboardPasteAttempt Success()
        {
            return new ClipboardPasteAttempt(true);
        }

        public static ClipboardPasteAttempt Failure(Exception? exception = null)
        {
            return new ClipboardPasteAttempt(false, exception);
        }
    }
}
