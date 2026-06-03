using System.Runtime.InteropServices;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class KeyboardInputService : IKeyboardInputService
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
