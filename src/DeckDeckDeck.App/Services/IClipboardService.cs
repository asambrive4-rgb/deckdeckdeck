using System.Windows;

namespace DeckDeckDeck.App.Services;

public interface IClipboardService
{
    IDataObject? GetDataObject();

    void SetText(string text);

    void SetDataObject(IDataObject dataObject);
}
