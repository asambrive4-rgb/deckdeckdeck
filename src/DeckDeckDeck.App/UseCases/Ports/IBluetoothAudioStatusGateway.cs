namespace DeckDeckDeck.App.UseCases.Ports;

/// <summary>
/// Windows 기본 재생 블루투스 오디오의 현재 상태를 조회하고 변경을 알린다.
/// </summary>
public interface IBluetoothAudioStatusGateway : IDisposable
{
    event EventHandler? StatusInvalidated;

    void StartMonitoring();

    Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}

/// <param name="IsBluetoothAudioConnected">기본 재생 장치가 연결된 블루투스 오디오인지</param>
/// <param name="DeviceName">표시용 기기 이름. 연결되지 않았으면 null</param>
/// <param name="BatteryPercent">대표 배터리 0–100. 확인할 수 없으면 null</param>
public sealed record BluetoothAudioStatusSnapshot(
    bool IsBluetoothAudioConnected,
    string? DeviceName,
    int? BatteryPercent)
{
    public static BluetoothAudioStatusSnapshot Disconnected { get; } = new(false, null, null);
}
