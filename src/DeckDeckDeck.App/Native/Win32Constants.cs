namespace DeckDeckDeck.App.Native;

public static class Win32Constants
{
    public static readonly IntPtr HwndTopmost = new(-1);

    public static readonly IntPtr HwndNotopmost = new(-2);

    public static readonly IntPtr HwndBottom = new(1);

    public const int WmMouseActivate = 0x0021;

    public const int WmHotkey = 0x0312;

    public const int GwlExStyle = -20;

    public const int MaNoActivate = 3;

    public const int SwShownoactivate = 4;

    public const uint SwpNosize = 0x0001;

    public const uint SwpNomove = 0x0002;

    public const uint SwpNoactivate = 0x0010;

    public const uint SwpShowwindow = 0x0040;

    public const int WsExNoactivate = 0x08000000;

    public const uint InputKeyboard = 1;

    public const uint ModControl = 0x0002;

    public const uint ModNoRepeat = 0x4000;

    public const uint KeyeventfKeyup = 0x0002;

    public const ushort VkControl = 0x11;

    public const ushort VkV = 0x56;

    public const uint VkNumpad0 = 0x60;

    public const uint VkMultiply = 0x6A;

    public const uint VkAdd = 0x6B;

    public const uint VkSubtract = 0x6D;

    public const uint VkDecimal = 0x6E;

    public const uint VkDivide = 0x6F;
}
