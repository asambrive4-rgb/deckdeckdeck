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
}
