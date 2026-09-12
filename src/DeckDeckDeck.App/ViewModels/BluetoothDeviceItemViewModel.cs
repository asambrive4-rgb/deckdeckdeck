// 역할: 블루투스 오디오 설정 화면에서 개별 장치의 이름, 연결 상태, 배터리 잔량을 표시하는 화면 모델입니다.
using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

/// <summary>
/// 블루투스 기기 선택 드롭다운의 개별 기기 항목 뷰모델.
/// </summary>
public sealed class BluetoothDeviceItemViewModel : ObservableObject
{
    public BluetoothAudioStatusSnapshot DeviceInfo { get; }

    public string DeviceName => DeviceInfo.DeviceName ?? string.Empty;

    public bool IsConnected => DeviceInfo.IsBluetoothAudioConnected;

    public int? BatteryPercent => DeviceInfo.BatteryPercent;

    public string BatteryText => BatteryPercent.HasValue ? $"{BatteryPercent.Value}%" : string.Empty;

    public bool HasBattery => BatteryPercent is >= 0 and <= 100;

    public BluetoothDeviceCategory Category => DeviceInfo.Category;

    public bool IsSelected { get; }

    public string StatusText => IsConnected ? (HasBattery ? BatteryText : "연결됨") : "미연결";

    public BluetoothDeviceItemViewModel(BluetoothAudioStatusSnapshot deviceInfo, bool isSelected)
    {
        DeviceInfo = deviceInfo;
        IsSelected = isSelected;
    }
}