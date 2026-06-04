using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

internal sealed class PasteFlowService
{
    private readonly IClipboardPasteService _clipboardPasteService;
    private readonly Action _completePasteSelection;
    private readonly Func<IntPtr> _getPasteTargetWindowHandle;
    private readonly Action _hideWindowAfterPaste;
    private readonly LoggingService? _loggingService;
    private readonly SettingsService _settingsService;

    public PasteFlowService(
        IClipboardPasteService clipboardPasteService,
        SettingsService settingsService,
        Func<IntPtr> getPasteTargetWindowHandle,
        Action hideWindowAfterPaste,
        Action completePasteSelection,
        LoggingService? loggingService)
    {
        _clipboardPasteService = clipboardPasteService;
        _settingsService = settingsService;
        _getPasteTargetWindowHandle = getPasteTargetWindowHandle;
        _hideWindowAfterPaste = hideWindowAfterPaste;
        _completePasteSelection = completePasteSelection;
        _loggingService = loggingService;
    }

    public async Task PasteSnippetAsync(Snippet snippet)
    {
        var settings = _settingsService.Load();

        try
        {
            if (settings.AutoHideAfterPaste)
            {
                _hideWindowAfterPaste();
            }

            var pasted = await _clipboardPasteService.PasteSnippetAsync(
                snippet,
                _getPasteTargetWindowHandle(),
                settings);

            if (!pasted)
            {
                _loggingService?.Log($"Paste failed for snippet {snippet.Id}.");
            }
        }
        catch (Exception ex)
        {
            _loggingService?.Log($"Paste failed for snippet {snippet.Id}.", ex);
            throw;
        }
        finally
        {
            _completePasteSelection();
        }
    }
}
