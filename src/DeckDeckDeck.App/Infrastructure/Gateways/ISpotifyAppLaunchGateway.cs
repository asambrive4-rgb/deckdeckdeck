// 역할: 스포티파이(Spotify) 데스크톱 앱을 실행하기 위한 외부 연동 인터페이스를 정의합니다.
namespace DeckDeckDeck.App.Infrastructure.Gateways;

public interface ISpotifyAppLaunchGateway
{
    bool TryLaunch();
}
