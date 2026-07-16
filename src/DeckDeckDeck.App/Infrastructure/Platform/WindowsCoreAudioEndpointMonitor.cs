using System.Runtime.InteropServices;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal interface IWindowsCoreAudioEndpointMonitor : IDisposable
{
    event EventHandler? DefaultEndpointChanged;

    void StartMonitoring();

    WindowsAudioEndpoint? GetDefaultRenderEndpoint();
}

internal sealed record WindowsAudioEndpoint(
    string DeviceName,
    string MmDeviceId,
    string PnpInstanceId);

internal sealed class WindowsCoreAudioEndpointMonitor : IWindowsCoreAudioEndpointMonitor
{
    private static readonly PROPERTYKEY DeviceFriendlyNameKey = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        14);
    private static readonly PROPERTYKEY DeviceInstanceIdKey = new(
        new Guid("78C34FC8-104A-4ACA-9EA4-524D52996E57"),
        256);

    private const int StgmRead = 0;
    private const ushort VtLpWStr = 31;

    private readonly object _sync = new();
    private IMMDeviceEnumerator? _enumerator;
    private EndpointNotificationClient? _notificationClient;
    private bool _monitoring;
    private bool _disposed;

    public event EventHandler? DefaultEndpointChanged;

    public void StartMonitoring()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_monitoring)
            {
                return;
            }

            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _notificationClient = new EndpointNotificationClient(this);
            var hr = _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
            if (hr != 0)
            {
                Marshal.ReleaseComObject(_enumerator);
                _enumerator = null;
                _notificationClient = null;
                Marshal.ThrowExceptionForHR(hr);
            }

            _monitoring = true;
        }
    }

    public WindowsAudioEndpoint? GetDefaultRenderEndpoint()
    {
        IMMDeviceEnumerator? temporaryEnumerator = null;
        IMMDevice? device = null;
        IPropertyStore? propertyStore = null;

        try
        {
            IMMDeviceEnumerator enumerator;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                enumerator = _enumerator ?? (temporaryEnumerator =
                    (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject());
            }

            var hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
            if (hr != 0 || device is null)
            {
                return null;
            }

            hr = device.OpenPropertyStore(StgmRead, out propertyStore);
            if (hr != 0 || propertyStore is null
                || !TryReadString(propertyStore, DeviceFriendlyNameKey, out var deviceName)
                || string.IsNullOrWhiteSpace(deviceName))
            {
                return null;
            }

            hr = device.GetId(out var mmDeviceId);
            if (hr != 0 || string.IsNullOrWhiteSpace(mmDeviceId))
            {
                return null;
            }

            TryReadString(propertyStore, DeviceInstanceIdKey, out var pnpInstanceId);
            if (string.IsNullOrWhiteSpace(pnpInstanceId))
            {
                pnpInstanceId = mmDeviceId.StartsWith(@"SWD\", StringComparison.OrdinalIgnoreCase)
                    ? mmDeviceId
                    : @"SWD\MMDEVAPI\" + mmDeviceId;
            }

            return new WindowsAudioEndpoint(deviceName, mmDeviceId, pnpInstanceId);
        }
        finally
        {
            if (propertyStore is not null)
            {
                Marshal.ReleaseComObject(propertyStore);
            }

            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }

            if (temporaryEnumerator is not null)
            {
                Marshal.ReleaseComObject(temporaryEnumerator);
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
            if (_enumerator is not null && _notificationClient is not null && _monitoring)
            {
                try
                {
                    _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
                }
                catch
                {
                    // Native notification cleanup must not block application shutdown.
                }
            }

            if (_enumerator is not null)
            {
                Marshal.ReleaseComObject(_enumerator);
            }

            _enumerator = null;
            _notificationClient = null;
            _monitoring = false;
        }
    }

    private void NotifyDefaultEndpointChanged()
    {
        DefaultEndpointChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryReadString(
        IPropertyStore propertyStore,
        PROPERTYKEY propertyKey,
        out string value)
    {
        value = string.Empty;
        var key = propertyKey;
        var hr = propertyStore.GetValue(ref key, out var variant);
        if (hr != 0)
        {
            return false;
        }

        try
        {
            if (variant.VariantType != VtLpWStr || variant.PointerValue == IntPtr.Zero)
            {
                return false;
            }

            value = Marshal.PtrToStringUni(variant.PointerValue)?.Trim() ?? string.Empty;
            return value.Length > 0;
        }
        finally
        {
            PropVariantClear(ref variant);
        }
    }

    private sealed class EndpointNotificationClient(WindowsCoreAudioEndpointMonitor owner)
        : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, uint newState)
        {
            owner.NotifyDefaultEndpointChanged();
            return 0;
        }

        public int OnDeviceAdded(string deviceId)
        {
            owner.NotifyDefaultEndpointChanged();
            return 0;
        }

        public int OnDeviceRemoved(string deviceId)
        {
            owner.NotifyDefaultEndpointChanged();
            return 0;
        }

        public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
        {
            if (flow == EDataFlow.Render && role is ERole.Console or ERole.Multimedia)
            {
                owner.NotifyDefaultEndpointChanged();
            }

            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PROPERTYKEY key)
        {
            owner.NotifyDefaultEndpointChanged();
            return 0;
        }
    }

    private enum EDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IntPtr device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IMMNotificationClient client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, int classContext, IntPtr activationParameters, out IntPtr interfacePointer);

        [PreserveSig]
        int OpenPropertyStore(int accessMode, out IPropertyStore properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig]
        int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);

        [PreserveSig]
        int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        [PreserveSig]
        int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        [PreserveSig]
        int OnDefaultDeviceChanged(
            EDataFlow flow,
            ERole role,
            [MarshalAs(UnmanagedType.LPWStr)] string? defaultDeviceId);

        [PreserveSig]
        int OnPropertyValueChanged(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            PROPERTYKEY key);
    }

    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PROPERTYKEY key);

        [PreserveSig]
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);

        [PreserveSig]
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);

        [PreserveSig]
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)]
        public ushort VariantType;

        [FieldOffset(8)]
        public IntPtr PointerValue;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT variant);
}
