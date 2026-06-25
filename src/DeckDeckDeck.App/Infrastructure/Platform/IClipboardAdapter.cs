using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Windows;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public interface IClipboardAdapter : IClipboardTextWriter
{
    IDataObject? GetDataObject();

    void SetDataObject(IDataObject dataObject);

    void SetFileDropList(string filePath);
}
