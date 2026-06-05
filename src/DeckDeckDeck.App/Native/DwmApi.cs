using System;
using System.Runtime.InteropServices;

namespace DeckDeckDeck.App.Native;

public static class DwmApi
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attr,
        ref int attrValue,
        int attrSize);

    // DWMWA_WINDOW_CORNER_PREFERENCE
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    // DWM_WINDOW_CORNER_PREFERENCE
    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }
}
