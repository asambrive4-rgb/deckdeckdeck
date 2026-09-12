// 역할: 테스트 환경에서 실제 블루투스 장치 없이도 오디오 상태를 시뮬레이션할 수 있도록 돕는 테스트용 가짜 객체입니다.
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

internal sealed class StubBluetoothAudioStatusGateway : IBluetoothAudioStatusGateway
{
    private readonly Func<CancellationToken, Task<BluetoothAudioStatusSnapshot>> _getCurrentAsync;

    public StubBluetoothAudioStatusGateway(BluetoothAudioStatusSnapshot? snapshot = null)
        : this(_ => Task.FromResult(snapshot ?? BluetoothAudioStatusSnapshot.Disconnected))
    {
    }

    public StubBluetoothAudioStatusGateway(
        Func<CancellationToken, Task<BluetoothAudioStatusSnapshot>> getCurrentAsync)
    {
        _getCurrentAsync = getCurrentAsync;
    }

    public event EventHandler? StatusInvalidated;

    public bool IsMonitoring { get; private set; }

    public bool IsDisposed { get; private set; }

    public int QueryCount { get; private set; }

    public void StartMonitoring()
    {
        IsMonitoring = true;
    }

    public Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        QueryCount++;
        return _getCurrentAsync(cancellationToken);
    }

    public void Invalidate()
    {
        StatusInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
