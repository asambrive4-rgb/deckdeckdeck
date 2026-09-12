// 역할: 웹 브라우저를 통한 인터넷 주소 열기 연동이 올바르게 수행되는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using System.Diagnostics;

namespace DeckDeckDeck.App.Tests;

public sealed class UrlLaunchGatewayAdapterTests
{
    [Fact]
    public void ShellLaunchReturningNoProcessStillReturnsTrue()
    {
        ProcessStartInfo? startInfo = null;
        var service = new UrlLaunchGatewayAdapter(info =>
        {
            startInfo = info;
            return null;
        });

        var launched = service.TryLaunch("https://example.com");

        Assert.True(launched);
        Assert.NotNull(startInfo);
        Assert.Equal("https://example.com", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }
}
