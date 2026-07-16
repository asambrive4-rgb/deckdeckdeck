using System.Runtime.InteropServices;
using System.Text;
using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal interface IWindowsBluetoothDeviceCatalog : IDisposable
{
    event EventHandler? DevicesChanged;

    WindowsBluetoothResolution Resolve(WindowsAudioEndpoint endpoint);

    void WatchMatchedDevices(IReadOnlyList<WindowsBluetoothDevice> devices);
}

internal sealed record WindowsBluetoothDevice(
    string InstanceId,
    string DeviceName,
    Guid? ContainerId,
    IReadOnlySet<string> AddressTokens,
    int? BatteryPercent,
    string? GattInterfacePath);

internal sealed record WindowsBluetoothResolution(
    bool IsBluetoothAudioConnected,
    string DeviceName,
    IReadOnlyList<WindowsBluetoothDevice> MatchedDevices)
{
    public int? CachedBatteryPercent => MatchedDevices
        .Select(device => device.BatteryPercent)
        .FirstOrDefault(level => level is >= 0 and <= 100);

    public WindowsGattTarget? GattTarget => MatchedDevices
        .Where(device => !string.IsNullOrWhiteSpace(device.GattInterfacePath))
        .Select(device => new WindowsGattTarget(device.InstanceId, device.GattInterfacePath!))
        .FirstOrDefault();
}

internal sealed class WindowsBluetoothDeviceCatalog : IWindowsBluetoothDeviceCatalog
{
    internal static readonly Guid ContainerIdPropertyFormatId = new(
        "8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C");
    internal const uint ContainerIdPropertyId = 2;

    private static readonly Guid BluetoothClassGuid = new("E0CBF06C-CD8B-4647-BB8A-263B43F0F974");
    private static readonly Guid BluetoothLeDeviceInterfaceGuid = new("781AEE18-7733-4CE4-ADD0-91F41C67B592");
    private static readonly DEVPROPKEY BatteryLevelKey = new(
        new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
    private static readonly DEVPROPKEY FriendlyNameKey = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    private static readonly DEVPROPKEY DeviceNameKey = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);
    private static readonly DEVPROPKEY ContainerIdKey = new(
        ContainerIdPropertyFormatId, ContainerIdPropertyId);

    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint CrSuccess = 0;
    private const uint CmLocateDevNodePhantom = 0x00000001;
    private const uint CmNotifyFilterTypeDeviceInstance = 2;
    private const int MaxDeviceIdLength = 200;
    private const int MaxAncestryDepth = 12;

    private readonly object _sync = new();
    private readonly CmNotificationCallback _notificationCallback;
    private readonly List<IntPtr> _notificationHandles = [];
    private bool _disposed;

    public WindowsBluetoothDeviceCatalog()
    {
        _notificationCallback = OnDeviceNotification;
    }

    public event EventHandler? DevicesChanged;

    public WindowsBluetoothResolution Resolve(WindowsAudioEndpoint endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var context = BuildEndpointContext(endpoint, out var endpointLooksBluetooth);
        var devices = EnumerateBluetoothDevices();
        var matchCandidates = devices
            .Select(device => new BluetoothDeviceMatchCandidate(
                device.InstanceId,
                device.DeviceName,
                device.ContainerId,
                device.AddressTokens))
            .ToList();
        var matchedCandidates = BluetoothAudioStatusRules.SelectBestDeviceGroup(context, matchCandidates);
        var matchedIds = matchedCandidates
            .Select(candidate => candidate.InstanceId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedDevices = devices
            .Where(device => matchedIds.Contains(device.InstanceId)
                || (matchedCandidates.Any(candidate => candidate.ContainerId is not null)
                    && matchedCandidates.Any(candidate => candidate.ContainerId == device.ContainerId)))
            .ToList();

        var connected = endpointLooksBluetooth || matchedDevices.Count > 0;
        var displayName = BluetoothAudioStatusRules.CleanDeviceName(endpoint.DeviceName);
        if (displayName.Length == 0 && matchedDevices.Count > 0)
        {
            displayName = BluetoothAudioStatusRules.CleanDeviceName(matchedDevices[0].DeviceName);
        }

        return new WindowsBluetoothResolution(connected, displayName, matchedDevices);
    }

    public void WatchMatchedDevices(IReadOnlyList<WindowsBluetoothDevice> devices)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ClearNotifications();

            foreach (var instanceId in devices
                .Select(device => device.InstanceId)
                .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var filter = new CM_NOTIFY_FILTER
                {
                    Size = (uint)Marshal.SizeOf<CM_NOTIFY_FILTER>(),
                    Flags = 0,
                    FilterType = CmNotifyFilterTypeDeviceInstance,
                    Reserved = 0,
                    DeviceInstance = new CM_NOTIFY_FILTER_DEVICEINSTANCE
                    {
                        InstanceId = instanceId
                    }
                };

                if (CM_Register_Notification(
                        ref filter,
                        IntPtr.Zero,
                        _notificationCallback,
                        out var notificationHandle) == CrSuccess
                    && notificationHandle != IntPtr.Zero)
                {
                    _notificationHandles.Add(notificationHandle);
                }
            }
        }
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
            ClearNotifications();
        }
    }

    private uint OnDeviceNotification(
        IntPtr notificationHandle,
        IntPtr context,
        uint action,
        IntPtr eventData,
        uint eventDataSize)
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        return CrSuccess;
    }

    private void ClearNotifications()
    {
        foreach (var notificationHandle in _notificationHandles)
        {
            try
            {
                CM_Unregister_Notification(notificationHandle);
            }
            catch
            {
                // Best-effort native cleanup.
            }
        }

        _notificationHandles.Clear();
    }

    private static BluetoothDeviceMatchContext BuildEndpointContext(
        WindowsAudioEndpoint endpoint,
        out bool looksBluetooth)
    {
        looksBluetooth = false;
        var containerIds = new List<Guid>();
        var addressTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var instanceId = endpoint.PnpInstanceId;
        if (!TryLocateDevNode(instanceId, out var devInst)
            && !instanceId.StartsWith(@"SWD\MMDEVAPI\", StringComparison.OrdinalIgnoreCase))
        {
            TryLocateDevNode(@"SWD\MMDEVAPI\" + endpoint.MmDeviceId, out devInst);
        }

        if (devInst != 0)
        {
            var current = devInst;
            for (var depth = 0; depth < MaxAncestryDepth; depth++)
            {
                if (TryGetDeviceId(current, out var currentId))
                {
                    looksBluetooth |= BluetoothAudioStatusRules.LooksLikeBluetoothPeripheralId(currentId);
                    foreach (var address in BluetoothAudioStatusRules.ExtractBluetoothAddressTokens(currentId))
                    {
                        addressTokens.Add(address);
                    }
                }

                if (TryGetDevNodeGuidProperty(current, ContainerIdKey, out var containerId)
                    && containerId != Guid.Empty
                    && !containerIds.Contains(containerId))
                {
                    containerIds.Add(containerId);
                }

                if (CM_Get_Parent(out var parent, current, 0) != CrSuccess
                    || parent == 0
                    || parent == current)
                {
                    break;
                }

                current = parent;
            }
        }

        looksBluetooth |= BluetoothAudioStatusRules.LooksLikeBluetoothPeripheralId(endpoint.PnpInstanceId);
        foreach (var address in BluetoothAudioStatusRules.ExtractBluetoothAddressTokens(endpoint.PnpInstanceId))
        {
            addressTokens.Add(address);
        }

        return new BluetoothDeviceMatchContext(endpoint.DeviceName, containerIds, addressTokens);
    }

    private static IReadOnlyList<WindowsBluetoothDevice> EnumerateBluetoothDevices()
    {
        var devices = new Dictionary<string, WindowsBluetoothDevice>(StringComparer.OrdinalIgnoreCase);
        EnumerateBluetoothClassDevices(devices);
        EnumerateBluetoothLeInterfaces(devices);
        return devices.Values.ToList();
    }

    private static void EnumerateBluetoothClassDevices(
        IDictionary<string, WindowsBluetoothDevice> devices)
    {
        var classGuid = BluetoothClassGuid;
        var deviceInfoSet = SetupDiGetClassDevs(ref classGuid, null, IntPtr.Zero, 0);
        if (IsInvalidHandle(deviceInfoSet))
        {
            return;
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var deviceInfo = CreateDeviceInfoData();
                if (!SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfo))
                {
                    break;
                }

                AddOrMergeDevice(devices, ReadDevice(deviceInfoSet, ref deviceInfo, null));
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static void EnumerateBluetoothLeInterfaces(
        IDictionary<string, WindowsBluetoothDevice> devices)
    {
        var interfaceGuid = BluetoothLeDeviceInterfaceGuid;
        var deviceInfoSet = SetupDiGetClassDevs(
            ref interfaceGuid,
            null,
            IntPtr.Zero,
            DigcfPresent | DigcfDeviceInterface);
        if (IsInvalidHandle(deviceInfoSet))
        {
            return;
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new SP_DEVICE_INTERFACE_DATA
                {
                    Size = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
                };
                if (!SetupDiEnumDeviceInterfaces(
                        deviceInfoSet,
                        IntPtr.Zero,
                        ref interfaceGuid,
                        index,
                        ref interfaceData))
                {
                    break;
                }

                SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    IntPtr.Zero,
                    0,
                    out var requiredSize,
                    IntPtr.Zero);
                if (requiredSize == 0)
                {
                    continue;
                }

                var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                    var deviceInfo = CreateDeviceInfoData();
                    if (!SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet,
                            ref interfaceData,
                            detailBuffer,
                            requiredSize,
                            out _,
                            ref deviceInfo))
                    {
                        continue;
                    }

                    var path = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
                    AddOrMergeDevice(devices, ReadDevice(deviceInfoSet, ref deviceInfo, path));
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static WindowsBluetoothDevice? ReadDevice(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfo,
        string? gattInterfacePath)
    {
        if (!TryGetDeviceId(deviceInfo.DeviceInstance, out var instanceId)
            || string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        TryGetDeviceStringProperty(deviceInfoSet, ref deviceInfo, FriendlyNameKey, out var deviceName);
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            TryGetDeviceStringProperty(deviceInfoSet, ref deviceInfo, DeviceNameKey, out deviceName);
        }

        deviceName = string.IsNullOrWhiteSpace(deviceName) ? instanceId : deviceName;
        Guid? containerId = TryGetDeviceGuidProperty(
            deviceInfoSet,
            ref deviceInfo,
            ContainerIdKey,
            out var value)
            ? value
            : null;
        int? battery = TryGetDeviceByteProperty(
            deviceInfoSet,
            ref deviceInfo,
            BatteryLevelKey,
            out var level)
            ? level
            : null;

        return new WindowsBluetoothDevice(
            instanceId,
            deviceName,
            containerId,
            BluetoothAudioStatusRules.ExtractBluetoothAddressTokens(instanceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            battery,
            gattInterfacePath);
    }

    private static void AddOrMergeDevice(
        IDictionary<string, WindowsBluetoothDevice> devices,
        WindowsBluetoothDevice? device)
    {
        if (device is null)
        {
            return;
        }

        if (!devices.TryGetValue(device.InstanceId, out var existing))
        {
            devices[device.InstanceId] = device;
            return;
        }

        devices[device.InstanceId] = existing with
        {
            DeviceName = existing.DeviceName == existing.InstanceId ? device.DeviceName : existing.DeviceName,
            ContainerId = existing.ContainerId ?? device.ContainerId,
            BatteryPercent = existing.BatteryPercent ?? device.BatteryPercent,
            GattInterfacePath = existing.GattInterfacePath ?? device.GattInterfacePath,
            AddressTokens = existing.AddressTokens.Concat(device.AddressTokens)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static bool TryLocateDevNode(string instanceId, out uint devInst)
    {
        devInst = 0;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        if (CM_Locate_DevNodeW(out devInst, instanceId, 0) == CrSuccess && devInst != 0)
        {
            return true;
        }

        return CM_Locate_DevNodeW(out devInst, instanceId, CmLocateDevNodePhantom) == CrSuccess
            && devInst != 0;
    }

    private static bool TryGetDeviceId(uint devInst, out string instanceId)
    {
        instanceId = string.Empty;
        var buffer = new StringBuilder(512);
        if (CM_Get_Device_IDW(devInst, buffer, (uint)buffer.Capacity, 0) != CrSuccess)
        {
            return false;
        }

        instanceId = buffer.ToString();
        return instanceId.Length > 0;
    }

    private static bool TryGetDevNodeGuidProperty(uint devInst, DEVPROPKEY key, out Guid value)
    {
        value = Guid.Empty;
        var propertyKey = key;
        uint size = 16;
        var buffer = new byte[16];
        if (CM_Get_DevNode_PropertyW(
                devInst,
                ref propertyKey,
                out var propertyType,
                buffer,
                ref size,
                0) != CrSuccess
            || propertyType != 0x0D)
        {
            return false;
        }

        value = new Guid(buffer);
        return value != Guid.Empty;
    }

    private static bool TryGetDeviceStringProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfo,
        DEVPROPKEY key,
        out string value)
    {
        value = string.Empty;
        var propertyKey = key;
        SetupDiGetDeviceProperty(
            deviceInfoSet,
            ref deviceInfo,
            ref propertyKey,
            out var propertyType,
            null,
            0,
            out var requiredSize,
            0);
        if (requiredSize == 0 || propertyType != 0x12)
        {
            return false;
        }

        var buffer = new byte[requiredSize];
        if (!SetupDiGetDeviceProperty(
                deviceInfoSet,
                ref deviceInfo,
                ref propertyKey,
                out propertyType,
                buffer,
                requiredSize,
                out _,
                0)
            || propertyType != 0x12)
        {
            return false;
        }

        value = Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
        return value.Length > 0;
    }

    private static bool TryGetDeviceGuidProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfo,
        DEVPROPKEY key,
        out Guid value)
    {
        value = Guid.Empty;
        var propertyKey = key;
        var buffer = new byte[16];
        if (!SetupDiGetDeviceProperty(
                deviceInfoSet,
                ref deviceInfo,
                ref propertyKey,
                out var propertyType,
                buffer,
                (uint)buffer.Length,
                out _,
                0)
            || propertyType != 0x0D)
        {
            return false;
        }

        value = new Guid(buffer);
        return value != Guid.Empty;
    }

    private static bool TryGetDeviceByteProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfo,
        DEVPROPKEY key,
        out byte value)
    {
        value = 0;
        var propertyKey = key;
        var buffer = new byte[16];
        if (!SetupDiGetDeviceProperty(
                deviceInfoSet,
                ref deviceInfo,
                ref propertyKey,
                out var propertyType,
                buffer,
                (uint)buffer.Length,
                out var requiredSize,
                0))
        {
            if (requiredSize == 0 || requiredSize > 64)
            {
                return false;
            }

            buffer = new byte[requiredSize];
            if (!SetupDiGetDeviceProperty(
                    deviceInfoSet,
                    ref deviceInfo,
                    ref propertyKey,
                    out propertyType,
                    buffer,
                    requiredSize,
                    out _,
                    0))
            {
                return false;
            }
        }

        uint number = propertyType switch
        {
            0x03 when buffer.Length >= 1 => buffer[0],
            0x05 when buffer.Length >= 2 => BitConverter.ToUInt16(buffer, 0),
            0x06 or 0x07 when buffer.Length >= 4 => BitConverter.ToUInt32(buffer, 0),
            _ => uint.MaxValue
        };
        if (number > 100)
        {
            return false;
        }

        value = (byte)number;
        return true;
    }

    private static SP_DEVINFO_DATA CreateDeviceInfoData()
    {
        return new SP_DEVINFO_DATA
        {
            Size = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
        };
    }

    private static bool IsInvalidHandle(IntPtr handle)
    {
        return handle == IntPtr.Zero || handle == new IntPtr(-1);
    }

    private delegate uint CmNotificationCallback(
        IntPtr notificationHandle,
        IntPtr context,
        uint action,
        IntPtr eventData,
        uint eventDataSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DeviceInstance;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public uint Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CM_NOTIFY_FILTER
    {
        public uint Size;
        public uint Flags;
        public uint FilterType;
        public uint Reserved;
        public CM_NOTIFY_FILTER_DEVICEINSTANCE DeviceInstance;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CM_NOTIFY_FILTER_DEVICEINSTANCE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxDeviceIdLength)]
        public string InstanceId;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr parentWindow,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SP_DEVINFO_DATA deviceInfo);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfo,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfo,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint deviceInstance, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Parent(out uint parentDeviceInstance, uint deviceInstance, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_IDW(
        uint deviceInstance,
        StringBuilder buffer,
        uint bufferLength,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_PropertyW(
        uint deviceInstance,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Register_Notification(
        ref CM_NOTIFY_FILTER filter,
        IntPtr context,
        CmNotificationCallback callback,
        out IntPtr notificationHandle);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Unregister_Notification(IntPtr notificationHandle);
}
