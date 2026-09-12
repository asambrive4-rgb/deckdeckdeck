// 역할: 윈도우 멀티미디어 키(재생/정지/볼륨) 시뮬레이션 기능이 올바르게 동작하는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class SystemMediaActionGatewayAdapterTests
{
    public static IEnumerable<object[]> VirtualKeyMappings =>
    [
        [SnippetMediaCommand.PlayPause, Win32Constants.VkMediaPlayPause],
        [SnippetMediaCommand.PreviousTrack, Win32Constants.VkMediaPreviousTrack],
        [SnippetMediaCommand.NextTrack, Win32Constants.VkMediaNextTrack],
        [SnippetMediaCommand.Stop, Win32Constants.VkMediaStop],
        [SnippetMediaCommand.Mute, Win32Constants.VkVolumeMute],
        [SnippetMediaCommand.VolumeUp, Win32Constants.VkVolumeUp],
        [SnippetMediaCommand.VolumeDown, Win32Constants.VkVolumeDown]
    ];

    [Theory]
    [MemberData(nameof(VirtualKeyMappings))]
    public void MediaCommandsMapToWindowsVirtualKeys(SnippetMediaCommand command, ushort expectedVirtualKey)
    {
        Assert.Equal(expectedVirtualKey, SystemMediaActionGatewayAdapter.GetVirtualKey(command));
    }

    [Fact]
    public void TryExecuteSendsMappedVirtualKey()
    {
        var sentKeys = new List<ushort>();
        var service = new SystemMediaActionGatewayAdapter(key =>
        {
            sentKeys.Add(key);
            return true;
        });

        var executed = service.TryExecute(SnippetMediaCommand.VolumeUp);

        Assert.True(executed);
        Assert.Equal([Win32Constants.VkVolumeUp], sentKeys);
    }
}
