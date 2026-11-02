// 역할: 테스트 환경에서 실제 블루투스 장치 없이도 기기 상태 및 페어링 목록을 시뮬레이션할 수 있도록 돕는 테스트용 가짜 객체입니다.
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

internal sealed class StubBluetoothAudioStatusGateway : IBluetoothAudioStatusGateway
{
    private readonly Func<string?, CancellationToken, Task<BluetoothAudioStatusSnapshot>> _getCurrentAsync;
    private readonly Func<CancellationToken, Task<IReadOnlyList<BluetoothDeviceItem>>> _getPairedDevicesAsync;

    public StubBluetoothAudioStatusGateway(
        BluetoothAudioStatusSnapshot? snapshot = null,
        IReadOnlyList<BluetoothDeviceItem>? pairedDevices = null)
        : this(
            (_, _) => Task.FromResult(snapshot ?? BluetoothAudioStatusSnapshot.Disconnected),
            _ => Task.FromResult(pairedDevices ?? Array.Empty<BluetoothDeviceItem>()))
    {
    }

    public StubBluetoothAudioStatusGateway(
        Func<CancellationToken, Task<BluetoothAudioStatusSnapshot>> getCurrentAsync)
        : this((_, ct) => getCurrentAsync(ct), _ => Task.FromResult<IReadOnlyList<BluetoothDeviceItem>>([]))
    {
    }

    public StubBluetoothAudioStatusGateway(
        Func<string?, CancellationToken, Task<BluetoothAudioStatusSnapshot>> getCurrentAsync,
        Func<CancellationToken, Task<IReadOnlyList<BluetoothDeviceItem>>>? getPairedDevicesAsync = null)
    {
        _getCurrentAsync = getCurrentAsync;
        _getPairedDevicesAsync = getPairedDevicesAsync ?? (_ => Task.FromResult<IReadOnlyList<BluetoothDeviceItem>>([]));
    }

    public event EventHandler? StatusInvalidated;

    public bool IsMonitoring { get; private set; }

    public bool IsDisposed { get; private set; }

    public int QueryCount { get; private set; }

    public string? LastRequestedPreferredDeviceName { get; private set; }

    public void StartMonitoring()
    {
        IsMonitoring = true;
    }

    public Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default) =>
        GetCurrentAsync(null, cancellationToken);

    public Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        string? preferredDeviceName,
        CancellationToken cancellationToken = default)
    {
        QueryCount++;
        LastRequestedPreferredDeviceName = preferredDeviceName;
        return _getCurrentAsync(preferredDeviceName, cancellationToken);
    }

    public Task<IReadOnlyList<BluetoothDeviceItem>> GetPairedDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        return _getPairedDevicesAsync(cancellationToken);
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
