// 역할: 블루투스 오디오 기기 상태 조회, 모니터링 이벤트 중계 및 선호 기기 설정을 관리하는 유스케이스입니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed record BluetoothTopBarPresentation(
    string DeviceName,
    string StatusMessage,
    string StatusToolTip,
    int? BatteryPercent,
    string BatteryText,
    bool HasBattery,
    bool IsBatteryLow,
    BluetoothDeviceCategory DeviceCategory,
    bool IsConnected)
{
    public static BluetoothTopBarPresentation Loading { get; } = new(
        DeviceName: BluetoothAudioStatusRules.LoadingText,
        StatusMessage: BluetoothAudioStatusRules.LoadingText,
        StatusToolTip: BluetoothAudioStatusRules.LoadingToolTip,
        BatteryPercent: null,
        BatteryText: string.Empty,
        HasBattery: false,
        IsBatteryLow: false,
        DeviceCategory: BluetoothDeviceCategory.Unknown,
        IsConnected: false);
}

public interface IBluetoothAudioStatusUseCase : IDisposable
{
    event EventHandler? StatusInvalidated;

    Task<BluetoothTopBarPresentation> GetPresentationAsync(
        string? preferredDeviceName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BluetoothDeviceItem>> GetPairedDevicesAsync();

    string? GetPreferredDeviceName();

    void SetPreferredDeviceName(string? deviceName);

    void StartMonitoring();
}

public sealed class BluetoothAudioStatusUseCase : IBluetoothAudioStatusUseCase
{
    private readonly IBluetoothAudioStatusGateway _gateway;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAppLogger? _logger;
    private bool _disposed;

    public event EventHandler? StatusInvalidated;

    public BluetoothAudioStatusUseCase(
        IBluetoothAudioStatusGateway gateway,
        ISettingsRepository settingsRepository,
        IAppLogger? logger = null)
    {
        _gateway = gateway;
        _settingsRepository = settingsRepository;
        _logger = logger;
        _gateway.StatusInvalidated += OnStatusInvalidated;
    }

    public async Task<BluetoothTopBarPresentation> GetPresentationAsync(
        string? preferredDeviceName,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _gateway.GetCurrentAsync(preferredDeviceName, cancellationToken);
        return CreatePresentation(snapshot, !string.IsNullOrWhiteSpace(preferredDeviceName));
    }

    public Task<IReadOnlyList<BluetoothDeviceItem>> GetPairedDevicesAsync() =>
        _gateway.GetPairedDevicesAsync();

    public string? GetPreferredDeviceName() =>
        _settingsRepository.Load().PreferredBluetoothDeviceName;

    public void SetPreferredDeviceName(string? deviceName)
    {
        var targetName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim();
        _settingsRepository.SetPreferredBluetoothDeviceName(targetName);
    }

    public void StartMonitoring()
    {
        try
        {
            _gateway.StartMonitoring();
        }
        catch (Exception ex)
        {
            _logger?.Log("블루투스 오디오 변경 감시 시작 실패", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gateway.StatusInvalidated -= OnStatusInvalidated;
        _gateway.Dispose();
    }

    private void OnStatusInvalidated(object? sender, EventArgs e) =>
        StatusInvalidated?.Invoke(this, e);

    public static BluetoothTopBarPresentation CreatePresentation(
        BluetoothAudioStatusSnapshot snapshot,
        bool isManualMode)
    {
        if (!snapshot.IsBluetoothAudioConnected)
        {
            return CreateDisconnectedPresentation(snapshot, isManualMode);
        }

        var cleanedName = BluetoothAudioStatusRules.CleanDeviceName(snapshot.DeviceName);
        var deviceName = cleanedName.Length > 0 ? cleanedName : BluetoothAudioStatusRules.DisconnectedText;
        var batteryPercent = snapshot.BatteryPercent;

        return new BluetoothTopBarPresentation(
            DeviceName: deviceName,
            StatusMessage: BluetoothAudioStatusRules.FormatDisplayText(snapshot.DeviceName, batteryPercent),
            StatusToolTip: BluetoothAudioStatusRules.FormatToolTip(snapshot.DeviceName, batteryPercent),
            BatteryPercent: batteryPercent,
            BatteryText: BluetoothAudioStatusRules.FormatBatteryText(batteryPercent),
            HasBattery: batteryPercent is >= 0 and <= 100,
            IsBatteryLow: BluetoothAudioStatusRules.IsBatteryLow(batteryPercent),
            DeviceCategory: snapshot.Category,
            IsConnected: true);
    }

    private static BluetoothTopBarPresentation CreateDisconnectedPresentation(
        BluetoothAudioStatusSnapshot snapshot,
        bool isManualMode)
    {
        if (isManualMode && !string.IsNullOrWhiteSpace(snapshot.DeviceName))
        {
            var cleaned = BluetoothAudioStatusRules.CleanDeviceName(snapshot.DeviceName);
            return new BluetoothTopBarPresentation(
                DeviceName: cleaned,
                StatusMessage: $"{cleaned} · {BluetoothAudioStatusRules.DisconnectedTagText}",
                StatusToolTip: BluetoothAudioStatusRules.FormatPinnedDeviceToolTip(cleaned, false, null),
                BatteryPercent: null,
                BatteryText: string.Empty,
                HasBattery: false,
                IsBatteryLow: false,
                DeviceCategory: snapshot.Category,
                IsConnected: false);
        }

        return new BluetoothTopBarPresentation(
            DeviceName: BluetoothAudioStatusRules.DisconnectedText,
            StatusMessage: BluetoothAudioStatusRules.DisconnectedText,
            StatusToolTip: BluetoothAudioStatusRules.DisconnectedToolTip,
            BatteryPercent: null,
            BatteryText: string.Empty,
            HasBattery: false,
            IsBatteryLow: false,
            DeviceCategory: BluetoothDeviceCategory.Unknown,
            IsConnected: false);
    }
}
