using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.Tests;

public sealed class MediaActionServiceTests
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
        Assert.Equal(expectedVirtualKey, MediaActionService.GetVirtualKey(command));
    }

    [Fact]
    public void TryExecuteSendsMappedVirtualKey()
    {
        var sentKeys = new List<ushort>();
        var service = new MediaActionService(key =>
        {
            sentKeys.Add(key);
            return true;
        });

        var executed = service.TryExecute(SnippetMediaCommand.VolumeUp);

        Assert.True(executed);
        Assert.Equal([Win32Constants.VkVolumeUp], sentKeys);
    }
}
