using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Services;

public interface IClipboardPasteService
{
    Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings);
}
