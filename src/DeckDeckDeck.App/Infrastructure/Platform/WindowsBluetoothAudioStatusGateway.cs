// 역할: 윈도우 시스템에서 블루투스 오디오 장치의 연결 상태와 배터리 잔량, 볼륨 정보를 조회합니다.
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
            logger)
    {
    }

    internal WindowsBluetoothAudioStatusGateway(
        IWindowsCoreAudioEndpointMonitor endpointMonitor,
        IWindowsBluetoothDeviceCatalog deviceCatalog,
        IWindowsGattBatteryReader gattBatteryReader,
        IAppLogger? logger = null)
    {
        _endpointMonitor = endpointMonitor;
        _deviceCatalog = deviceCatalog;
        _gattBatteryReader = gattBatteryReader;
        _logger = logger;
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

    public async Task<BluetoothAudioStatusSnapshot> GetCurrentAsync(
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
                () => _deviceCatalog.ResolveBestConnectedDevice(endpoint),
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
        if (!resolution.IsBluetoothAudioConnected
            || string.IsNullOrWhiteSpace(resolution.DeviceName))
        {
            ClearActiveDeviceMonitoring();
            Log(endpoint is not null
                ? $"bluetooth device: no active bluetooth device found (default audio=[{endpoint.DeviceName}])"
                : "bluetooth device: no active bluetooth device found");
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
