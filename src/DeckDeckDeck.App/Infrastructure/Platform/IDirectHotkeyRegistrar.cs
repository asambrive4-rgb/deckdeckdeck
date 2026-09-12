// 역할: 백그라운드 단축키 등록 및 해제 기능을 정의한 인터페이스입니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public interface IDirectHotkeyRegistrar : IDisposable
{
    event EventHandler<DirectHotkeyPressedEventArgs>? DirectHotkeyPressed;

    bool IsSuspended { get; set; }

    IReadOnlyList<string> Start();

    void Refresh(IReadOnlyList<DirectHotkeyRegistration> registrations);
}
