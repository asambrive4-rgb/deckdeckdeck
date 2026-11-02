// 역할: 블루투스 기기(오디오/입력장치)의 연결 상태, 배터리 정보 및 페어링된 기기 목록을 조회하기 위한 포트 규격을 정의합니다.
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
        CancellationToken cancellationToken = default) =>
        GetCurrentAsync(null, cancellationToken);

    Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        string? preferredDeviceName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BluetoothDeviceItem>> GetPairedDevicesAsync(
        CancellationToken cancellationToken = default);
}

/// <param name="IsBluetoothAudioConnected">블루투스 기기가 활성 연결되어 있는지</param>
/// <param name="DeviceName">표시용 기기 이름. 연결되지 않았거나 미선택이면 null</param>
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

/// <param name="DeviceName">정리된 표시용 기기 이름</param>
/// <param name="Category">기기 범주 (오디오, 입력장치 등)</param>
/// <param name="IsConnected">현재 연결(Present)되어 있는지</param>
/// <param name="BatteryPercent">배터리 잔량(0~100) 또는 null</param>
public sealed record BluetoothDeviceItem(
    string DeviceName,
    BluetoothDeviceCategory Category,
    bool IsConnected,
    int? BatteryPercent);
