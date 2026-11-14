// 역할: 오디오 3종(헤드폰, 이어폰, 스피커) 전용 벡터 아이콘 로드 및 자동 매핑 규칙을 검증합니다.
using DeckDeckDeck.App.Infrastructure.Storage;

namespace DeckDeckDeck.App.Tests;

public sealed class AudioPresetIconResolverTests
{
    [Theory]
    [InlineData("bt-audio connect AC:80:0A:20:CB:AF", "XM5 on", AudioVectorIconResources.HeadphonesOn)]
    [InlineData("bt-audio disconnect AC:80:0A:20:CB:AF", "XM5 off", AudioVectorIconResources.HeadphonesOff)]
    [InlineData("bt-audio connect E4:92:82:82:D5:9B", "Buds on", AudioVectorIconResources.EarbudsOn)]
    [InlineData("bt-audio disconnect E4:92:82:82:D5:9B", "Buds off", AudioVectorIconResources.EarbudsOff)]
    [InlineData("bt-audio connect 04:FE:A1:28:32:33", "JBL Flip 4 on", AudioVectorIconResources.SpeakerOn)]
    [InlineData("bt-audio disconnect 04:FE:A1:28:32:33", "JBL Flip 4 off", AudioVectorIconResources.SpeakerOff)]
    public void GetAutoIconResourcePath_MatchesExpectedAudioResource(string? cmd, string? title, string expectedPath)
    {
        var actual = AudioPresetIconResolver.GetAutoIconResourcePath(cmd, title);
        Assert.Equal(expectedPath, actual);
    }

    [Theory]
    [InlineData(null, "모니터 절전")]
    [InlineData(null, "화면 끄기")]
    [InlineData(null, "모바일 핫스팟 설정")]
    [InlineData("git status", "터미널 실행")]
    public void GetAutoIconResourcePath_NonAudioActions_ReturnNullToKeepOriginalLayout(string? cmd, string? title)
    {
        var actual = AudioPresetIconResolver.GetAutoIconResourcePath(cmd, title);
        Assert.Null(actual);
    }

    [Fact]
    public void TryGetImage_LoadsNavyHeadphonesVector()
    {
        var success = AudioPresetIconResolver.TryGetImage(AudioVectorIconResources.HeadphonesOn, out var image);
        Assert.True(success);
        Assert.NotNull(image);
    }

    [Fact]
    public void TryGetImage_LoadsSilverEarbudsVector()
    {
        var success = AudioPresetIconResolver.TryGetImage(AudioVectorIconResources.EarbudsOn, out var image);
        Assert.True(success);
        Assert.NotNull(image);
    }

    [Fact]
    public void TryGetImage_LoadsGraySpeakerVector()
    {
        var success = AudioPresetIconResolver.TryGetImage(AudioVectorIconResources.SpeakerOn, out var image);
        Assert.True(success);
        Assert.NotNull(image);
    }
}
