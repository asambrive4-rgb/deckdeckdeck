using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal interface IWindowsGattBatteryReader : IDisposable
{
    event EventHandler? BatteryChanged;

    Task<int?> ReadBatteryAsync(
        WindowsGattTarget target,
        CancellationToken cancellationToken);

    void StopMonitoring();
}

internal sealed record WindowsGattTarget(string InstanceId, string InterfacePath);

internal sealed class WindowsBluetoothGattBatteryReader : IWindowsGattBatteryReader
{
    private const ushort BatteryServiceUuid = 0x180F;
    private const ushort BatteryLevelCharacteristicUuid = 0x2A19;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint GattFlagNone = 0;
    private const uint GattFlagForceReadFromDevice = 0x00000004;
    private static readonly TimeSpan DirectReadTimeout = TimeSpan.FromSeconds(3);

    private readonly object _sync = new();
    private readonly GattEventCallback _callback;
    private GattEventSubscription? _subscription;
    private bool _disposed;

    public WindowsBluetoothGattBatteryReader()
    {
        _callback = OnGattEvent;
    }

    public event EventHandler? BatteryChanged;

    public async Task<int?> ReadBatteryAsync(
        WindowsGattTarget target,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var probeTask = Task.Run(() => Probe(target), CancellationToken.None);

        GattProbeResult result;
        try
        {
            result = await probeTask.WaitAsync(DirectReadTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            DisposeLateProbe(probeTask);
            return null;
        }
        catch (OperationCanceledException)
        {
            DisposeLateProbe(probeTask);
            throw;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            result.Subscription?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }

        ReplaceSubscription(result.Subscription);
        return result.BatteryPercent;
    }

    public void StopMonitoring()
    {
        ReplaceSubscription(null);
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
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    private GattProbeResult Probe(WindowsGattTarget target)
    {
        var deviceHandle = OpenGattDevice(target.InterfacePath);
        if (deviceHandle.IsInvalid)
        {
            deviceHandle.Dispose();
            return GattProbeResult.Empty;
        }

        try
        {
            if (!TryFindBatteryCharacteristic(deviceHandle, out var characteristic)
                || !TryReadBatteryValue(deviceHandle, ref characteristic, out var batteryPercent))
            {
                return GattProbeResult.Empty;
            }

            GattEventSubscription? subscription = null;
            if (characteristic.IsNotifiable != 0 || characteristic.IsIndicatable != 0)
            {
                subscription = TryRegisterValueChanged(deviceHandle, characteristic);
            }

            if (subscription is not null)
            {
                deviceHandle = null!;
            }

            return new GattProbeResult(batteryPercent, subscription);
        }
        finally
        {
            deviceHandle?.Dispose();
        }
    }

    private GattEventSubscription? TryRegisterValueChanged(
        SafeFileHandle deviceHandle,
        BTH_LE_GATT_CHARACTERISTIC characteristic)
    {
        var registrationSize = 4 + Marshal.SizeOf<BTH_LE_GATT_CHARACTERISTIC>();
        var registration = Marshal.AllocHGlobal(registrationSize);
        try
        {
            Span<byte> zeroes = stackalloc byte[registrationSize];
            Marshal.Copy(zeroes.ToArray(), 0, registration, registrationSize);
            Marshal.WriteInt16(registration, 0, 1);
            Marshal.StructureToPtr(characteristic, IntPtr.Add(registration, 4), false);

            var hr = BluetoothGATTRegisterEvent(
                deviceHandle,
                BTH_LE_GATT_EVENT_TYPE.CharacteristicValueChanged,
                registration,
                _callback,
                IntPtr.Zero,
                out var eventHandle,
                GattFlagNone);
            return hr == 0 && eventHandle != IntPtr.Zero
                ? new GattEventSubscription(deviceHandle, eventHandle)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(registration);
        }
    }

    private void ReplaceSubscription(GattEventSubscription? subscription)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                subscription?.Dispose();
                return;
            }

            var previous = _subscription;
            _subscription = subscription;
            previous?.Dispose();
        }
    }

    private void OnGattEvent(
        BTH_LE_GATT_EVENT_TYPE eventType,
        IntPtr eventOutput,
        IntPtr context)
    {
        if (eventType == BTH_LE_GATT_EVENT_TYPE.CharacteristicValueChanged)
        {
            BatteryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void DisposeLateProbe(Task<GattProbeResult> probeTask)
    {
        _ = probeTask.ContinueWith(
            completedTask =>
            {
                if (completedTask.Status == TaskStatus.RanToCompletion)
                {
                    completedTask.Result.Subscription?.Dispose();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static SafeFileHandle OpenGattDevice(string interfacePath)
    {
        var handle = CreateFile(
            interfacePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (!handle.IsInvalid)
        {
            return handle;
        }

        handle.Dispose();
        return CreateFile(
            interfacePath,
            GenericRead,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
    }

    private static bool TryFindBatteryCharacteristic(
        SafeFileHandle deviceHandle,
        out BTH_LE_GATT_CHARACTERISTIC characteristic)
    {
        characteristic = default;
        BluetoothGATTGetServices(deviceHandle, 0, null, out var serviceCount, GattFlagNone);
        if (serviceCount == 0)
        {
            return false;
        }

        var services = new BTH_LE_GATT_SERVICE[serviceCount];
        if (BluetoothGATTGetServices(
                deviceHandle,
                serviceCount,
                services,
                out var returnedServices,
                GattFlagNone) != 0)
        {
            return false;
        }

        for (var serviceIndex = 0; serviceIndex < returnedServices; serviceIndex++)
        {
            var service = services[serviceIndex];
            if (!service.ServiceUuid.IsShort(BatteryServiceUuid))
            {
                continue;
            }

            BluetoothGATTGetCharacteristics(
                deviceHandle,
                ref service,
                0,
                null,
                out var characteristicCount,
                GattFlagNone);
            if (characteristicCount == 0)
            {
                continue;
            }

            var characteristics = new BTH_LE_GATT_CHARACTERISTIC[characteristicCount];
            if (BluetoothGATTGetCharacteristics(
                    deviceHandle,
                    ref service,
                    characteristicCount,
                    characteristics,
                    out var returnedCharacteristics,
                    GattFlagNone) != 0)
            {
                continue;
            }

            for (var characteristicIndex = 0;
                 characteristicIndex < returnedCharacteristics;
                 characteristicIndex++)
            {
                var candidate = characteristics[characteristicIndex];
                if (candidate.CharacteristicUuid.IsShort(BatteryLevelCharacteristicUuid)
                    && candidate.IsReadable != 0)
                {
                    characteristic = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadBatteryValue(
        SafeFileHandle deviceHandle,
        ref BTH_LE_GATT_CHARACTERISTIC characteristic,
        out int batteryPercent)
    {
        batteryPercent = 0;
        BluetoothGATTGetCharacteristicValue(
            deviceHandle,
            ref characteristic,
            0,
            IntPtr.Zero,
            out var requiredSize,
            GattFlagForceReadFromDevice);
        if (requiredSize < 5)
        {
            return false;
        }

        var valueBuffer = Marshal.AllocHGlobal(requiredSize);
        try
        {
            var hr = BluetoothGATTGetCharacteristicValue(
                deviceHandle,
                ref characteristic,
                requiredSize,
                valueBuffer,
                out _,
                GattFlagForceReadFromDevice);
            if (hr != 0)
            {
                return false;
            }

            var dataSize = Marshal.ReadInt32(valueBuffer, 0);
            if (dataSize < 1)
            {
                return false;
            }

            var value = Marshal.ReadByte(valueBuffer, 4);
            if (value > 100)
            {
                return false;
            }

            batteryPercent = value;
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(valueBuffer);
        }
    }

    private sealed record GattProbeResult(int? BatteryPercent, GattEventSubscription? Subscription)
    {
        public static GattProbeResult Empty { get; } = new(null, null);
    }

    private sealed class GattEventSubscription(
        SafeFileHandle deviceHandle,
        IntPtr eventHandle) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                BluetoothGATTUnregisterEvent(eventHandle, GattFlagNone);
            }
            finally
            {
                deviceHandle.Dispose();
            }
        }
    }

    private delegate void GattEventCallback(
        BTH_LE_GATT_EVENT_TYPE eventType,
        IntPtr eventOutput,
        IntPtr context);

    private enum BTH_LE_GATT_EVENT_TYPE
    {
        CharacteristicValueChanged = 0
    }

    [StructLayout(LayoutKind.Explicit, Size = 20)]
    private struct BTH_LE_UUID
    {
        [FieldOffset(0)]
        public byte IsShortUuid;

        [FieldOffset(4)]
        public ushort ShortUuid;

        [FieldOffset(4)]
        public Guid LongUuid;

        public readonly bool IsShort(ushort value)
        {
            return IsShortUuid != 0 && ShortUuid == value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct BTH_LE_GATT_SERVICE
    {
        [FieldOffset(0)]
        public BTH_LE_UUID ServiceUuid;

        [FieldOffset(20)]
        public ushort AttributeHandle;
    }

    [StructLayout(LayoutKind.Explicit, Size = 36)]
    private struct BTH_LE_GATT_CHARACTERISTIC
    {
        [FieldOffset(0)]
        public ushort ServiceHandle;

        [FieldOffset(4)]
        public BTH_LE_UUID CharacteristicUuid;

        [FieldOffset(24)]
        public ushort AttributeHandle;

        [FieldOffset(26)]
        public ushort CharacteristicValueHandle;

        [FieldOffset(28)]
        public byte IsBroadcastable;

        [FieldOffset(29)]
        public byte IsReadable;

        [FieldOffset(30)]
        public byte IsWritable;

        [FieldOffset(31)]
        public byte IsWritableWithoutResponse;

        [FieldOffset(32)]
        public byte IsSignedWritable;

        [FieldOffset(33)]
        public byte IsNotifiable;

        [FieldOffset(34)]
        public byte IsIndicatable;

        [FieldOffset(35)]
        public byte HasExtendedProperties;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("BluetoothApis.dll")]
    private static extern int BluetoothGATTGetServices(
        SafeFileHandle device,
        ushort servicesBufferCount,
        [Out] BTH_LE_GATT_SERVICE[]? servicesBuffer,
        out ushort servicesBufferActual,
        uint flags);

    [DllImport("BluetoothApis.dll")]
    private static extern int BluetoothGATTGetCharacteristics(
        SafeFileHandle device,
        ref BTH_LE_GATT_SERVICE service,
        ushort characteristicsBufferCount,
        [Out] BTH_LE_GATT_CHARACTERISTIC[]? characteristicsBuffer,
        out ushort characteristicsBufferActual,
        uint flags);

    [DllImport("BluetoothApis.dll")]
    private static extern int BluetoothGATTGetCharacteristicValue(
        SafeFileHandle device,
        ref BTH_LE_GATT_CHARACTERISTIC characteristic,
        uint characteristicValueDataSize,
        IntPtr characteristicValue,
        out ushort characteristicValueSizeRequired,
        uint flags);

    [DllImport("BluetoothApis.dll")]
    private static extern int BluetoothGATTRegisterEvent(
        SafeFileHandle service,
        BTH_LE_GATT_EVENT_TYPE eventType,
        IntPtr eventParameter,
        GattEventCallback callback,
        IntPtr callbackContext,
        out IntPtr eventHandle,
        uint flags);

    [DllImport("BluetoothApis.dll")]
    private static extern int BluetoothGATTUnregisterEvent(IntPtr eventHandle, uint flags);
}
