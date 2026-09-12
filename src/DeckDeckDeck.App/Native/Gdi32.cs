// 역할: 윈도우 그래픽 장치 인터페이스(GDI) API를 호출하여 화면 비트맵이나 그래픽 리소스를 처리합니다.
using System.Runtime.InteropServices;

namespace DeckDeckDeck.App.Native;

public static partial class Gdi32
{
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);
}
