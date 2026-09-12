// 역할: 여러 슬롯 중 사용자가 선택한 내용을 대상 프로그램에 안전하게 붙여넣는 작업을 관리합니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
namespace DeckDeckDeck.App.Composition;

internal sealed class PasteSelectionSession
{
    private int _currentSessionId;

    public void Start()
    {
        _currentSessionId++;
    }

    public Action CreateCompletion(Action completeSelection)
    {
        var sessionId = _currentSessionId;

        return () =>
        {
            if (sessionId == _currentSessionId)
            {
                completeSelection();
            }
        };
    }
}
