// 역할: 윈도우 블루투스 기기(오디오/입력장치)의 현재 상태와 배터리(윈도우 값, BLE, 소니 전용 통신 순)를 조회하고 페어링된 기기 목록을 제공합니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using Microsoft.Win32;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class WindowsBluetoothAudioStatusGateway : IBluetoothAudioStatusGateway
{
    private readonly IWindowsCoreAudioEndpointMonitor _endpointMonitor;
    private readonly IWindowsBluetoothDeviceCatalog _deviceCatalog;
    private readonly IWindowsGattBatteryReader _gattBatteryReader;
    private readonly IWindowsSonyBatteryReader? _sonyBatteryReader;
    private readonly IAppLogger? _logger;
    private readonly object _sync = new();
    private bool _monitoring;
    private bool _powerEventsSubscribed;
    private bool _disposed;

    public WindowsBluetoothAudioStatusGateway(IAppLogger? logger = null)
        : this(
            new WindowsCoreAudioEndpointMonitor(),
            new WindowsBluetoothDeviceCatalog(),
            new WindowsBluetoothGattBatteryReader(),
            logger,
            new WindowsSonyBatteryReader())
    {
    }

    internal WindowsBluetoothAudioStatusGateway(
        IWindowsCoreAudioEndpointMonitor endpointMonitor,
        IWindowsBluetoothDeviceCatalog deviceCatalog,
        IWindowsGattBatteryReader gattBatteryReader,
        IAppLogger? logger = null,
        IWindowsSonyBatteryReader? sonyBatteryReader = null)
    {
        _endpointMonitor = endpointMonitor;
        _deviceCatalog = deviceCatalog;
        _gattBatteryReader = gattBatteryReader;
        _logger = logger;
        _sonyBatteryReader = sonyBatteryReader;
    }

    public event EventHandler? StatusInvalidated;

    public void StartMonitoring()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_monitoring)
            {
                return;
            }

            _endpointMonitor.DefaultEndpointChanged += OnStatusInvalidated;
            _deviceCatalog.DevicesChanged += OnStatusInvalidated;
            _gattBatteryReader.BatteryChanged += OnStatusInvalidated;
            try
            {
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
                _powerEventsSubscribed = true;
            }
            catch (Exception ex)
            {
                _logger?.Log("bluetooth audio: power event registration failed", ex);
            }

            try
            {
                _endpointMonitor.StartMonitoring();
                _monitoring = true;
            }
            catch
            {
                UnsubscribeEvents();
                throw;
            }
        }
    }

    public Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default) =>
        GetCurrentAsync(null, cancellationToken);

    public async Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
        string? preferredDeviceName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        WindowsAudioEndpoint? endpoint = null;
        try
        {
            endpoint = _endpointMonitor.GetDefaultRenderEndpoint();
        }
        catch (Exception ex)
        {
            _logger?.Log("bluetooth device: default endpoint query failed", ex);
        }

        WindowsBluetoothResolution resolution;
        try
        {
            resolution = await Task.Run(
                () => _deviceCatalog.ResolveBestConnectedDevice(endpoint, preferredDeviceName),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Log("bluetooth device: device catalog query failed", ex);
            ClearActiveDeviceMonitoring();
            return BluetoothAudioStatusSnapshot.Disconnected;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!resolution.IsBluetoothAudioConnected)
        {
            ClearActiveDeviceMonitoring();
            if (!string.IsNullOrWhiteSpace(resolution.DeviceName))
            {
                // 수동 고정 모드인데 기기가 미연결인 경우
                var deviceCategory = BluetoothAudioStatusRules.DetermineDeviceCategory(resolution.DeviceName);
                Log($"bluetooth device: manual preferred device disconnected: [{resolution.DeviceName}]");
                return new BluetoothAudioStatusSnapshot(
                    false,
                    resolution.DeviceName,
                    null,
                    deviceCategory);
            }

            Log(endpoint is not null
                ? $"bluetooth device: no active bluetooth device found (default audio=[{endpoint.DeviceName}])"
                : "bluetooth device: no active bluetooth device found");
            return BluetoothAudioStatusSnapshot.Disconnected;
        }

        if (string.IsNullOrWhiteSpace(resolution.DeviceName))
        {
            ClearActiveDeviceMonitoring();
            return BluetoothAudioStatusSnapshot.Disconnected;
        }

        _deviceCatalog.WatchMatchedDevices(resolution.MatchedDevices);
        var batteryPercent = resolution.CachedBatteryPercent;
        var source = batteryPercent is null ? "none" : "windows-property";

        if (batteryPercent is null && resolution.GattTarget is { } gattTarget)
        {
            try
            {
                batteryPercent = await _gattBatteryReader.ReadBatteryAsync(
                    gattTarget,
                    cancellationToken);
                if (batteryPercent is not null)
                {
                    source = "gatt-180f-2a19";
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log("bluetooth device: standard GATT battery query failed", ex);
            }
        }
        else
        {
            _gattBatteryReader.StopMonitoring();
        }

        if (batteryPercent is null && await TryReadSonyBatteryAsync(resolution, cancellationToken) is { } sonyBattery)
        {
            batteryPercent = sonyBattery;
            source = "sony-mdr";
        }

        cancellationToken.ThrowIfCancellationRequested();
        var category = BluetoothAudioStatusRules.DetermineDeviceCategory(resolution.DeviceName);
        Log(
            $"bluetooth device: connected name=[{resolution.DeviceName}] "
            + $"category=[{category}] battery=[{batteryPercent?.ToString() ?? "-"}] source=[{source}]");
        return new BluetoothAudioStatusSnapshot(
            true,
            resolution.DeviceName,
            batteryPercent,
            category);
    }

    public Task<IReadOnlyList<BluetoothDeviceItem>> GetPairedDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () => _deviceCatalog.EnumerateAllPairedDevices(),
            cancellationToken);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnsubscribeEvents();
            _gattBatteryReader.Dispose();
            _deviceCatalog.Dispose();
            _endpointMonitor.Dispose();
            _monitoring = false;
        }
    }

    // Windows가 배터리를 주지 않는 소니 헤드폰은 전용 통신으로 직접 물어본다. 소니가 아니면 바로 null.
    private async Task<int?> TryReadSonyBatteryAsync(
        WindowsBluetoothResolution resolution,
        CancellationToken cancellationToken)
    {
        if (_sonyBatteryReader is null)
        {
            return null;
        }

        var addresses = resolution.MatchedDevices
            .SelectMany(device => device.AddressTokens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(token => ulong.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out var address)
                ? address
                : 0UL)
            .Where(address => address != 0);

        foreach (var address in addresses)
        {
            try
            {
                if (await _sonyBatteryReader.ReadBatteryAsync(address, cancellationToken) is { } battery)
                {
                    return battery;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log("bluetooth device: sony battery query failed", ex);
            }
        }

        return null;
    }

    private void OnStatusInvalidated(object? sender, EventArgs e)
    {
        StatusInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            StatusInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ClearActiveDeviceMonitoring()
    {
        try
        {
            _deviceCatalog.WatchMatchedDevices([]);
            _gattBatteryReader.StopMonitoring();
        }
        catch (ObjectDisposedException)
        {
            // Shutdown raced a late status query.
        }
    }

    private void UnsubscribeEvents()
    {
        _endpointMonitor.DefaultEndpointChanged -= OnStatusInvalidated;
        _deviceCatalog.DevicesChanged -= OnStatusInvalidated;
        _gattBatteryReader.BatteryChanged -= OnStatusInvalidated;
        if (_powerEventsSubscribed)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _powerEventsSubscribed = false;
        }
    }

    private void Log(string message)
    {
        _logger?.Log(message);
    }
}
