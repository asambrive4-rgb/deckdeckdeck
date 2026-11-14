// 역할: 블루투스 기기 드롭다운에서 각 기기의 아이콘(헤드폰/이어폰/스피커), 이름, 연결 상태, 배터리를 표현하는 뷰모델입니다.
using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Storage;
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

    public BluetoothAudioDeviceType AudioDeviceType =>
        Category == BluetoothDeviceCategory.Audio
            ? BluetoothAudioStatusRules.DetermineAudioDeviceType(DeviceName)
            : BluetoothAudioDeviceType.Unknown;

    public bool IsSelected { get; }

    public string StatusText => IsConnected
        ? (HasBattery ? BatteryText : BluetoothAudioStatusRules.ConnectedTagText)
        : BluetoothAudioStatusRules.DisconnectedTagText;

    public string DeviceIconResourcePath => Category switch
    {
        BluetoothDeviceCategory.Audio => AudioDeviceType switch
        {
            BluetoothAudioDeviceType.Headphones => IsConnected ? AudioVectorIconResources.HeadphonesOn : AudioVectorIconResources.HeadphonesOff,
            BluetoothAudioDeviceType.Earbuds => IsConnected ? AudioVectorIconResources.EarbudsOn : AudioVectorIconResources.EarbudsOff,
            BluetoothAudioDeviceType.Speaker => IsConnected ? AudioVectorIconResources.SpeakerOn : AudioVectorIconResources.SpeakerOff,
            _ => IsConnected ? AudioVectorIconResources.HeadphonesOn : AudioVectorIconResources.HeadphonesOff
        },
        _ => string.Empty
    };

    public bool HasDeviceIcon => !string.IsNullOrWhiteSpace(DeviceIconResourcePath);

    public string CategoryEmoji => Category switch
    {
        BluetoothDeviceCategory.Audio => AudioDeviceType switch
        {
            BluetoothAudioDeviceType.Speaker => "🔊",
            BluetoothAudioDeviceType.Earbuds => "🎧",
            BluetoothAudioDeviceType.Headphones => "🎧",
            _ => "🎧"
        },
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
