using DeckDeckDeck.App.Infrastructure.Platform;

namespace DeckDeckDeck.App.Tests;

public sealed class WindowsBluetoothAudioStatusGatewayTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task GetCurrentAsync_WithWindowsBattery_DoesNotCallGatt(int battery)
    {
        var endpoint = new FakeEndpointMonitor();
        var device = Device(battery, gattPath: "gatt-path");
        var catalog = new FakeDeviceCatalog(Resolution(device));
        var gatt = new FakeGattBatteryReader(42);
        using var gateway = new WindowsBluetoothAudioStatusGateway(endpoint, catalog, gatt);

        var result = await gateway.GetCurrentAsync();

        Assert.True(result.IsBluetoothAudioConnected);
        Assert.Equal(battery, result.BatteryPercent);
        Assert.Equal(0, gatt.ReadCount);
        Assert.Equal(1, gatt.StopCount);
        Assert.Equal([device.InstanceId], catalog.WatchedInstanceIds);
    }

    [Fact]
    public async Task GetCurrentAsync_WithoutWindowsBattery_UsesGatt()
    {
        var endpoint = new FakeEndpointMonitor();
        var catalog = new FakeDeviceCatalog(Resolution(Device(null, "gatt-path")));
        var gatt = new FakeGattBatteryReader(73);
        using var gateway = new WindowsBluetoothAudioStatusGateway(endpoint, catalog, gatt);

        var result = await gateway.GetCurrentAsync();

        Assert.Equal(73, result.BatteryPercent);
        Assert.Equal(1, gatt.ReadCount);
    }

    [Fact]
    public async Task GetCurrentAsync_WhenGattFails_PreservesConnectionAndName()
    {
        var endpoint = new FakeEndpointMonitor();
        var catalog = new FakeDeviceCatalog(Resolution(Device(null, "gatt-path")));
        var gatt = new FakeGattBatteryReader(new InvalidOperationException("not supported"));
        using var gateway = new WindowsBluetoothAudioStatusGateway(endpoint, catalog, gatt);

        var result = await gateway.GetCurrentAsync();

        Assert.True(result.IsBluetoothAudioConnected);
        Assert.Equal("Buds3 Pro", result.DeviceName);
        Assert.Null(result.BatteryPercent);
    }

    [Fact]
    public async Task GetCurrentAsync_WhenDefaultIsNotBluetooth_ReturnsDisconnectedAndClearsMonitoring()
    {
        var endpoint = new FakeEndpointMonitor();
        var catalog = new FakeDeviceCatalog(new WindowsBluetoothResolution(false, "Monitor", []));
        var gatt = new FakeGattBatteryReader(50);
        using var gateway = new WindowsBluetoothAudioStatusGateway(endpoint, catalog, gatt);

        var result = await gateway.GetCurrentAsync();

        Assert.False(result.IsBluetoothAudioConnected);
        Assert.Empty(catalog.WatchedInstanceIds);
        Assert.Equal(1, gatt.StopCount);
    }

    [Fact]
    public void StartMonitoring_ForwardsNativeInvalidationsAndDisposeReleasesComponents()
    {
        var endpoint = new FakeEndpointMonitor();
        var catalog = new FakeDeviceCatalog(Resolution(Device(50)));
        var gatt = new FakeGattBatteryReader(50);
        var gateway = new WindowsBluetoothAudioStatusGateway(endpoint, catalog, gatt);
        var invalidations = 0;
        gateway.StatusInvalidated += (_, _) => invalidations++;

        gateway.StartMonitoring();
        endpoint.Invalidate();
        catalog.Invalidate();
        gatt.Invalidate();
        gateway.Dispose();

        Assert.True(endpoint.Started);
        Assert.Equal(3, invalidations);
        Assert.True(endpoint.Disposed);
        Assert.True(catalog.Disposed);
        Assert.True(gatt.Disposed);
    }

    private static WindowsBluetoothDevice Device(int? battery, string? gattPath = null)
    {
        return new WindowsBluetoothDevice(
            "BTHLE\\DEV_001122334455",
            "Buds3 Pro",
            Guid.NewGuid(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "001122334455" },
            battery,
            gattPath);
    }

    private static WindowsBluetoothResolution Resolution(WindowsBluetoothDevice device)
    {
        return new WindowsBluetoothResolution(true, "Buds3 Pro", [device]);
    }

    private sealed class FakeEndpointMonitor : IWindowsCoreAudioEndpointMonitor
    {
        public event EventHandler? DefaultEndpointChanged;

        public bool Started { get; private set; }

        public bool Disposed { get; private set; }

        public void StartMonitoring() => Started = true;

        public WindowsAudioEndpoint? GetDefaultRenderEndpoint() =>
            new("Headphones (Buds3 Pro)", "mm-id", @"SWD\MMDEVAPI\mm-id");

        public void Invalidate() => DefaultEndpointChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeDeviceCatalog(WindowsBluetoothResolution resolution)
        : IWindowsBluetoothDeviceCatalog
    {
        public event EventHandler? DevicesChanged;

        public bool Disposed { get; private set; }

        public IReadOnlyList<string> WatchedInstanceIds { get; private set; } = [];

        public WindowsBluetoothResolution Resolve(WindowsAudioEndpoint endpoint) => resolution;

        public void WatchMatchedDevices(IReadOnlyList<WindowsBluetoothDevice> devices)
        {
            WatchedInstanceIds = devices.Select(device => device.InstanceId).ToList();
        }

        public void Invalidate() => DevicesChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeGattBatteryReader : IWindowsGattBatteryReader
    {
        private readonly int? _battery;
        private readonly Exception? _exception;

        public FakeGattBatteryReader(int? battery)
        {
            _battery = battery;
        }

        public FakeGattBatteryReader(Exception exception)
        {
            _exception = exception;
        }

        public event EventHandler? BatteryChanged;

        public int ReadCount { get; private set; }

        public int StopCount { get; private set; }

        public bool Disposed { get; private set; }

        public Task<int?> ReadBatteryAsync(
            WindowsGattTarget target,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return _exception is null
                ? Task.FromResult(_battery)
                : Task.FromException<int?>(_exception);
        }

        public void StopMonitoring() => StopCount++;

        public void Invalidate() => BatteryChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() => Disposed = true;
    }
}
