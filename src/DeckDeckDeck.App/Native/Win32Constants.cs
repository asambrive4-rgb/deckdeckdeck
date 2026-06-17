namespace DeckDeckDeck.App.Native;

public static class Win32Constants
{
    public static readonly IntPtr HwndTopmost = new(-1);

    public static readonly IntPtr HwndNotopmost = new(-2);

    public static readonly IntPtr HwndBottom = new(1);

    public const int WmHotkey = 0x0312;

    public const int WmKeydown = 0x0100;

    public const int WmKeyup = 0x0101;

    public const int WmSyskeydown = 0x0104;

    public const int WmSyskeyup = 0x0105;

    public const int WhKeyboardLl = 13;

    public const int SwShownoactivate = 4;

    public const uint SwpNosize = 0x0001;

    public const uint SwpNomove = 0x0002;

    public const uint SwpNoactivate = 0x0010;

    public const uint SwpShowwindow = 0x0040;

    public const uint InputKeyboard = 1;

    public const uint ModControl = 0x0002;

    public const uint ModNoRepeat = 0x4000;

    public const uint KeyeventfKeyup = 0x0002;

    public const ushort VkControl = 0x11;

    public const ushort VkShift = 0x10;

    public const ushort VkMenu = 0x12;

    public const ushort VkLWin = 0x5B;

    public const ushort VkRWin = 0x5C;

    public const ushort VkV = 0x56;

    public const ushort VkVolumeMute = 0xAD;

    public const ushort VkVolumeDown = 0xAE;

    public const ushort VkVolumeUp = 0xAF;

    public const ushort VkMediaNextTrack = 0xB0;

    public const ushort VkMediaPreviousTrack = 0xB1;

    public const ushort VkMediaStop = 0xB2;

    public const ushort VkMediaPlayPause = 0xB3;

    public const uint VkNumpad0 = 0x60;

    public const uint VkMultiply = 0x6A;

    public const uint VkAdd = 0x6B;

    public const uint VkSubtract = 0x6D;

    public const uint VkDecimal = 0x6E;

    public const uint VkDivide = 0x6F;
}
