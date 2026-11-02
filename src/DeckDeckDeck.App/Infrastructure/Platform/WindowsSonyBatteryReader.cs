// 역할: 소니 헤드폰(WH-1000XM5 등)에 전용 블루투스 통신으로 배터리 잔량을 조회하고 성공·실패 결과별 유효 시간을 두어 반복 조회를 방지합니다.
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal interface IWindowsSonyBatteryReader
{
    Task<int?> ReadBatteryAsync(ulong bluetoothAddress, CancellationToken cancellationToken);
}

internal sealed class WindowsSonyBatteryReader : IWindowsSonyBatteryReader
{
    internal static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(5);

    private static readonly Guid SonyServiceUuid = new("956C7B26-D49A-4BA8-B03F-B17D393CB6E2");
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(4);

    private readonly Func<ulong, CancellationToken, Task<int?>> _queryRunner;
    private readonly Func<DateTime> _utcNowProvider;
    private readonly object _sync = new();
    private readonly Dictionary<ulong, (int? Battery, DateTime At)> _cache = [];

    public WindowsSonyBatteryReader()
        : this(QueryAsync, () => DateTime.UtcNow)
    {
    }

    internal WindowsSonyBatteryReader(
        Func<ulong, CancellationToken, Task<int?>> queryRunner,
        Func<DateTime>? utcNowProvider = null)
    {
        _queryRunner = queryRunner;
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
    }

    public async Task<int?> ReadBatteryAsync(ulong bluetoothAddress, CancellationToken cancellationToken)
    {
        var now = _utcNowProvider();
        lock (_sync)
        {
            if (_cache.TryGetValue(bluetoothAddress, out var cached))
            {
                var duration = cached.Battery is not null ? SuccessCacheDuration : FailureCacheDuration;
                if (now - cached.At < duration)
                {
                    return cached.Battery;
                }
            }
        }

        var battery = await _queryRunner(bluetoothAddress, cancellationToken);
        lock (_sync)
        {
            _cache[bluetoothAddress] = (battery, _utcNowProvider());
        }

        return battery;
    }

    private static async Task<int?> QueryAsync(ulong bluetoothAddress, CancellationToken cancellationToken)
    {
        using var device = await BluetoothDevice.FromBluetoothAddressAsync(bluetoothAddress);
        if (device is null)
        {
            return null;
        }

        var services = await device.GetRfcommServicesForIdAsync(
            RfcommServiceId.FromUuid(SonyServiceUuid),
            BluetoothCacheMode.Uncached);
        if (services.Services.Count == 0)
        {
            return null;
        }

        var service = services.Services[0];
        using var socket = new StreamSocket();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(QueryTimeout);

        try
        {
            await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName)
                .AsTask(timeout.Token);

            using var writer = new DataWriter(socket.OutputStream);
            using var reader = new DataReader(socket.InputStream)
            {
                InputStreamOptions = InputStreamOptions.Partial
            };

            writer.WriteBytes(SonyMdrProtocol.BuildFrame(
                SonyMdrProtocol.DataTypeMdr,
                0,
                SonyMdrProtocol.BatteryRequestPayload));
            await writer.StoreAsync().AsTask(timeout.Token);

            var accumulator = new SonyMdrProtocol.FrameAccumulator();
            while (true)
            {
                var received = await reader.LoadAsync(256).AsTask(timeout.Token);
                var bytes = new byte[received];
                reader.ReadBytes(bytes);

                foreach (var frame in accumulator.Feed(bytes))
                {
                    if (frame.DataType != SonyMdrProtocol.DataTypeMdr)
                    {
                        continue;
                    }

                    writer.WriteBytes(SonyMdrProtocol.BuildAck(frame.Sequence));
                    await writer.StoreAsync().AsTask(timeout.Token);
                    if (SonyMdrProtocol.TryParseBattery(frame.Payload, out var battery))
                    {
                        return battery;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
