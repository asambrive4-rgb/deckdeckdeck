// 역할: 미디어 제어 버튼(재생, 정지 등)에 사용되는 기본 아이콘 이미지 리소스 경로를 정의합니다.
namespace DeckDeckDeck.App.Models;

public static class MediaIconResourcePaths
{
    private const string ResourcePrefix = "resource:";
    private const string PlayPauseIcon = ResourcePrefix + "Deck.Icon.Media.PlayPause";
    private const string PreviousTrackIcon = ResourcePrefix + "Deck.Icon.Media.PreviousTrack";
    private const string NextTrackIcon = ResourcePrefix + "Deck.Icon.Media.NextTrack";
    private const string StopIcon = ResourcePrefix + "Deck.Icon.Media.Stop";
    private const string MuteIcon = ResourcePrefix + "Deck.Icon.Media.Mute";
    private const string VolumeUpIcon = ResourcePrefix + "Deck.Icon.Media.VolumeUp";
    private const string VolumeDownIcon = ResourcePrefix + "Deck.Icon.Media.VolumeDown";
    private const string ShuffleIcon = ResourcePrefix + "Deck.Icon.Media.Shuffle";
    private const string RepeatIcon = ResourcePrefix + "Deck.Icon.Media.Repeat";

    public static string GetIconResourcePath(SnippetMediaCommand? command)
    {
        return command switch
        {
            SnippetMediaCommand.PreviousTrack => PreviousTrackIcon,
            SnippetMediaCommand.NextTrack => NextTrackIcon,
            SnippetMediaCommand.Stop => StopIcon,
            SnippetMediaCommand.Mute => MuteIcon,
            SnippetMediaCommand.VolumeUp => VolumeUpIcon,
            SnippetMediaCommand.VolumeDown => VolumeDownIcon,
            SnippetMediaCommand.ToggleShuffle => ShuffleIcon,
            SnippetMediaCommand.CycleRepeat => RepeatIcon,
            SnippetMediaCommand.OpenSpotifyAndResume => PlayPauseIcon,
            _ => PlayPauseIcon
        };
    }
}
