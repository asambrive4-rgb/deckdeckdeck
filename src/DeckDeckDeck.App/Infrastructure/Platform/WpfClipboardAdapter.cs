using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Collections.Specialized;
using System.IO;
using System.Windows;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class WpfClipboardAdapter : IClipboardAdapter
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

    public void SetFileDropList(string filePath)
    {
        Clipboard.SetDataObject(CreateFileDropDataObject(filePath), copy: true);
    }

    internal static DataObject CreateFileDropDataObject(string filePath)
    {
        var files = new StringCollection
        {
            filePath
        };
        var dataObject = new DataObject();
        dataObject.SetFileDropList(files);
        dataObject.SetData(
            "Preferred DropEffect",
            new MemoryStream([5, 0, 0, 0]));

        return dataObject;
    }
}
