// 역할: 미디어 제어 슬롯에서 실행할 세부 명령(재생/일시정지, 볼륨 조절 등)의 종류를 정의하는 목록입니다.
namespace DeckDeckDeck.App.Models;

public enum SnippetMediaCommand
{
    PlayPause,
    PreviousTrack,
    NextTrack,
    Stop,
    Mute,
    VolumeUp,
    VolumeDown,
    ToggleShuffle,
    CycleRepeat,
    OpenSpotifyAndResume
}
