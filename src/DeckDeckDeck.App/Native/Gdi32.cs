using System.Runtime.InteropServices;

namespace DeckDeckDeck.App.Native;

public static partial class Gdi32
{
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);
}
