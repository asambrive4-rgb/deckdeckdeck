// 역할: 윈도우 키보드 입력을 시뮬레이션하는 기능의 인터페이스를 정의합니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
namespace DeckDeckDeck.App.Infrastructure.Platform;

public interface IWin32KeyboardInputAdapter
{
    bool SendCtrlV();

    bool SendCtrlShiftV();
}
