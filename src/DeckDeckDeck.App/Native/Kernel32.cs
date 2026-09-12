// 역할: 윈도우 커널 API를 호출하여 시스템 프로세스 및 메모리 관련 기본 운영체제 기능을 다룹니다.
using System.Runtime.InteropServices;

namespace DeckDeckDeck.App.Native;

public static partial class Kernel32
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}
