using System.Runtime.InteropServices;

namespace DeckDeckDeck.App.Native;

public static partial class Kernel32
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}
