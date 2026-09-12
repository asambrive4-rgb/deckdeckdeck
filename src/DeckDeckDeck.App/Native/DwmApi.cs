// 역할: 윈도우 데스크톱 창 관리자(DWM) API를 호출하여 창의 모서리 둥글기나 그림자 효과를 제어합니다.
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

    // DWMWA_BORDER_COLOR (Windows 11 build 22000+)
    public const int DWMWA_BORDER_COLOR = 34;

    // DWMWA_COLOR_NONE: OS 레벨 창 외곽 테두리를 그리지 않음 (0xFFFFFFFE)
    public const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    // DWM_WINDOW_CORNER_PREFERENCE
    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }
}
