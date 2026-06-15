using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using System.Runtime.InteropServices;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class SystemMediaActionGatewayAdapter : IMediaActionGateway
{
    private readonly Func<ushort, bool> _sendVirtualKey;

    public SystemMediaActionGatewayAdapter()
        : this(SendVirtualKey)
    {
    }

    internal SystemMediaActionGatewayAdapter(Func<ushort, bool> sendVirtualKey)
    {
        _sendVirtualKey = sendVirtualKey;
    }

    public bool TryExecute(SnippetMediaCommand command)
    {
        return _sendVirtualKey(GetVirtualKey(command));
    }

    internal static ushort GetVirtualKey(SnippetMediaCommand command)
    {
        return command switch
        {
            SnippetMediaCommand.PlayPause => Win32Constants.VkMediaPlayPause,
            SnippetMediaCommand.PreviousTrack => Win32Constants.VkMediaPreviousTrack,
            SnippetMediaCommand.NextTrack => Win32Constants.VkMediaNextTrack,
            SnippetMediaCommand.Stop => Win32Constants.VkMediaStop,
            SnippetMediaCommand.Mute => Win32Constants.VkVolumeMute,
            SnippetMediaCommand.VolumeUp => Win32Constants.VkVolumeUp,
            SnippetMediaCommand.VolumeDown => Win32Constants.VkVolumeDown,
            _ => Win32Constants.VkMediaPlayPause
        };
    }

    private static bool SendVirtualKey(ushort virtualKey)
    {
        var inputs = new[]
        {
            CreateKeyInput(virtualKey, keyUp: false),
            CreateKeyInput(virtualKey, keyUp: true)
        };

        var sent = User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<User32.Input>());
        return sent == inputs.Length;
    }

    private static User32.Input CreateKeyInput(ushort virtualKey, bool keyUp)
    {
        return new User32.Input
        {
            Type = Win32Constants.InputKeyboard,
            Data = new User32.InputUnion
            {
                Keyboard = new User32.KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = keyUp ? Win32Constants.KeyeventfKeyup : 0
                }
            }
        };
    }
}
