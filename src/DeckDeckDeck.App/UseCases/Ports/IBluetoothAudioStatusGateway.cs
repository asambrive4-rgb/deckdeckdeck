// 역할: 블루투스 오디오 기기의 연결 상태와 배터리 정보를 외부에서 조회하기 위한 규격을 정의합니다.
using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.UseCases.Ports;

/// <summary>
/// Windows 블루투스 기기(오디오/입력장치)의 현재 상태를 조회하고 변경을 알린다.
/// </summary>
public interface IBluetoothAudioStatusGateway : IDisposable
{
    event EventHandler? StatusInvalidated;

    void StartMonitoring();

    Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}

/// <param name="IsBluetoothAudioConnected">블루투스 기기가 활성 연결되어 있는지</param>
/// <param name="DeviceName">표시용 기기 이름. 연결되지 않았으면 null</param>
/// <param name="BatteryPercent">대표 배터리 0–100. 확인할 수 없으면 null</param>
/// <param name="Category">기기 범주 (오디오, 입력장치 등)</param>
public sealed record BluetoothAudioStatusSnapshot(
    bool IsBluetoothAudioConnected,
    string? DeviceName,
    int? BatteryPercent,
    BluetoothDeviceCategory Category = BluetoothDeviceCategory.Unknown)
{
    public static BluetoothAudioStatusSnapshot Disconnected { get; } = new(false, null, null);
}
