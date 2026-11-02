// 역할: 블루투스 기기 드롭다운에서 각 기기의 아이콘, 이름, 연결 상태, 배터리를 표현하는 뷰모델입니다.
using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

/// <summary>
/// 블루투스 기기 선택 드롭다운의 개별 기기 항목 뷰모델.
/// </summary>
public sealed class BluetoothDeviceItemViewModel : ObservableObject
{
    public BluetoothDeviceItem DeviceItem { get; }

    public string DeviceName => DeviceItem.DeviceName;

    public bool IsConnected => DeviceItem.IsConnected;

    public int? BatteryPercent => DeviceItem.BatteryPercent;

    public string BatteryText => BatteryPercent.HasValue ? $"{BatteryPercent.Value}%" : string.Empty;

    public bool HasBattery => BatteryPercent is >= 0 and <= 100;

    public BluetoothDeviceCategory Category => DeviceItem.Category;

    public bool IsSelected { get; }

    public string StatusText => IsConnected
        ? (HasBattery ? BatteryText : BluetoothAudioStatusRules.ConnectedTagText)
        : BluetoothAudioStatusRules.DisconnectedTagText;

    public string CategoryEmoji => Category switch
    {
        BluetoothDeviceCategory.Audio => DeviceName.Contains("스피커", StringComparison.OrdinalIgnoreCase)
            || DeviceName.Contains("speaker", StringComparison.OrdinalIgnoreCase)
            || DeviceName.Contains("flip", StringComparison.OrdinalIgnoreCase) ? "🔊" : "🎧",
        BluetoothDeviceCategory.Input => DeviceName.Contains("mouse", StringComparison.OrdinalIgnoreCase)
            || DeviceName.Contains("마우스", StringComparison.OrdinalIgnoreCase) ? "🖱️" : "⌨️",
        _ => "📱"
    };

    public BluetoothDeviceItemViewModel(BluetoothDeviceItem deviceItem, bool isSelected)
    {
        DeviceItem = deviceItem;
        IsSelected = isSelected;
    }
}