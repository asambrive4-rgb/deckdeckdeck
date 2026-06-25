using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Runtime.InteropServices;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class Win32KeyboardInputAdapter : IWin32KeyboardInputAdapter
{
    public bool SendCtrlV()
    {
        var inputs = new[]
        {
            CreateKeyInput(Win32Constants.VkControl, keyUp: false),
            CreateKeyInput(Win32Constants.VkV, keyUp: false),
            CreateKeyInput(Win32Constants.VkV, keyUp: true),
            CreateKeyInput(Win32Constants.VkControl, keyUp: true)
        };

        var sent = User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<User32.Input>());
        return sent == inputs.Length;
    }

    public bool SendCtrlShiftV()
    {
        var inputs = new[]
        {
            CreateKeyInput(Win32Constants.VkControl, keyUp: false),
            CreateKeyInput(Win32Constants.VkShift, keyUp: false),
            CreateKeyInput(Win32Constants.VkV, keyUp: false),
            CreateKeyInput(Win32Constants.VkV, keyUp: true),
            CreateKeyInput(Win32Constants.VkShift, keyUp: true),
            CreateKeyInput(Win32Constants.VkControl, keyUp: true)
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
