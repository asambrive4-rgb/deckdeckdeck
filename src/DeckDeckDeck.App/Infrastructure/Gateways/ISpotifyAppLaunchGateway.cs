// 역할: 스포티파이(Spotify) 데스크톱 앱을 실행하기 위한 외부 연동 인터페이스를 정의합니다.
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
namespace DeckDeckDeck.App.Infrastructure.Gateways;

public interface ISpotifyAppLaunchGateway
{
    bool TryLaunch();
}
