using System.Windows;

namespace DeckDeckDeck.App.Services;

public sealed class WpfClipboardService : IClipboardService
{
    public IDataObject? GetDataObject()
    {
        return Clipboard.GetDataObject();
    }

    public void SetText(string text)
    {
        Clipboard.SetText(text, TextDataFormat.UnicodeText);
    }

    public void SetDataObject(IDataObject dataObject)
    {
        Clipboard.SetDataObject(dataObject, copy: true);
    }
}
