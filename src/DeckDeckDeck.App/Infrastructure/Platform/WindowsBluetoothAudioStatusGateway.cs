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

        WindowsAudioEndpoint? endpoint;
        try
        {
            endpoint = _endpointMonitor.GetDefaultRenderEndpoint();
        }
        catch (Exception ex)
        {
            _logger?.Log("bluetooth audio: default endpoint query failed", ex);
            ClearActiveDeviceMonitoring();
            return BluetoothAudioStatusSnapshot.Disconnected;
        }

        if (endpoint is null)
        {
            ClearActiveDeviceMonitoring();
            Log("bluetooth audio: no default render endpoint");
            return BluetoothAudioStatusSnapshot.Disconnected;
        }

        WindowsBluetoothResolution resolution;
        try
        {
            resolution = await Task.Run(
                () => _deviceCatalog.Resolve(endpoint),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Log("bluetooth audio: device catalog query failed", ex);
            ClearActiveDeviceMonitoring();
            return BluetoothAudioStatusSnapshot.Disconnected;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!resolution.IsBluetoothAudioConnected
            || string.IsNullOrWhiteSpace(resolution.DeviceName))
        {
            ClearActiveDeviceMonitoring();
            Log($"bluetooth audio: non-bluetooth default name=[{endpoint.DeviceName}]");
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
                _logger?.Log("bluetooth audio: standard GATT battery query failed", ex);
            }
        }
        else
        {
            _gattBatteryReader.StopMonitoring();
        }

        cancellationToken.ThrowIfCancellationRequested();
        Log(
            $"bluetooth audio: connected name=[{resolution.DeviceName}] "
            + $"battery=[{batteryPercent?.ToString() ?? "-"}] source=[{source}]");
        return new BluetoothAudioStatusSnapshot(
            true,
            resolution.DeviceName,
            batteryPercent);
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
