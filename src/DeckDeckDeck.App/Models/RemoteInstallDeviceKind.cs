// 역할: Android 원격설치 대상 기기의 종류(태블릿, 폰, 워치)를 구분하는 열거형입니다.
namespace DeckDeckDeck.App.Models;

/// <summary>
/// Target Android device category for remote install slots.
/// Ordered to match Numpad 4 (Tablet) / 5 (Phone) / 6 (Watch) layout.
/// </summary>
public enum RemoteInstallDeviceKind
{
    Tablet,
    Phone,
    Watch
}
