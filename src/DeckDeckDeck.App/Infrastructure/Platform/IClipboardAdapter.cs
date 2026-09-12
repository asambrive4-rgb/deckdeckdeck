// 역할: 윈도우 클립보드에 접근하여 데이터를 읽거나 쓰는 기능을 추상화한 인터페이스입니다.
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
