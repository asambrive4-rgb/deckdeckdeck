// 역할: Android 원격설치 대상 기기의 종류(태블릿, 폰, 워치)를 단일 또는 복수 조합으로 구분하는 플래그 열거형입니다.
namespace DeckDeckDeck.App.Models;

/// <summary>
/// Target Android device categories for remote install slots.
/// Supports multi-selection (e.g. Tablet | Phone) via [Flags].
/// </summary>
[Flags]
public enum RemoteInstallDeviceKind
{
    None = 0,
    Tablet = 1 << 0,
    Phone = 1 << 1,
    Watch = 1 << 2
}
