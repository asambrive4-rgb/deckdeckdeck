// 역할: 윈도우 BluetoothAPIs(BluetoothSetServiceState)를 비동기 백그라운드로 호출하여 블루투스 오디오 기기를 연결/해제하며 모바일 플로팅 스낵바로 진행 상태를 표시합니다.
using System.Runtime.InteropServices;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed record PairedBluetoothAudioDeviceEntry(
    string DisplayName,
    string MacAddress,
    ulong Address,
    bool IsConnected);

public sealed class WindowsBluetoothDeviceControlGateway : IBluetoothDeviceControlGateway
{
    public static readonly Guid AudioSinkServiceClassGuid = new("0000110B-0000-1000-8000-00805F9B34FB");
    public static readonly Guid HandsfreeServiceClassGuid = new("0000111E-0000-1000-8000-00805F9B34FB");

    private const uint ErrorSuccess = 0;
    private const uint ErrorAlreadyExists = 183;

    private readonly Func<ulong, IReadOnlySet<Guid>> _getInstalledServices;
    private readonly Func<ulong, Guid, bool, uint> _setServiceState;
    private readonly Func<string, ulong?> _findDeviceAddressByName;
    private readonly IAppLogger? _logger;
    private readonly IBluetoothActionFeedbackNotifier? _feedbackNotifier;
    private readonly Func<string, string?> _resolveDeviceDisplayName;
    internal readonly bool _waitForCompletion;

    public WindowsBluetoothDeviceControlGateway(IAppLogger? logger = null)
        : this(
            NativeGetInstalledServices,
            NativeSetServiceState,
            FindDeviceAddressByName,
            logger,
            waitForCompletion: false,
            feedbackNotifier: new WpfBluetoothActionSnackbarNotifier(),
            resolveDeviceDisplayName: DefaultResolveDeviceDisplayName)
    {
    }

    internal WindowsBluetoothDeviceControlGateway(
        Func<ulong, IReadOnlySet<Guid>> getInstalledServices,
        Func<ulong, Guid, bool, uint> setServiceState,
        Func<string, ulong?> findDeviceAddressByName,
        IAppLogger? logger = null,
        bool waitForCompletion = true,
        IBluetoothActionFeedbackNotifier? feedbackNotifier = null,
        Func<string, string?>? resolveDeviceDisplayName = null)
    {
        _getInstalledServices = getInstalledServices;
        _setServiceState = setServiceState;
        _findDeviceAddressByName = findDeviceAddressByName;
        _logger = logger;
        _waitForCompletion = waitForCompletion;
        _feedbackNotifier = feedbackNotifier;
        _resolveDeviceDisplayName = resolveDeviceDisplayName ?? DefaultResolveDeviceDisplayName;
    }

    public Task<bool> TryControlAsync(BluetoothAudioCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(command.Target))
        {
            _logger?.Log("Bluetooth control failed: target is empty.");
            return Task.FromResult(false);
        }

        if (_waitForCompletion)
        {
            return Task.Run(() =>
            {
                var displayName = ResolveDisplayName(command.Target);
                _feedbackNotifier?.NotifyStarting(command.Action, displayName);
                var succeeded = false;
                try
                {
                    succeeded = ControlDeviceCore(command);
                    return succeeded;
                }
                finally
                {
                    _feedbackNotifier?.NotifyCompleted(command.Action, displayName, succeeded);
                }
            }, cancellationToken);
        }

        _ = Task.Run(() =>
        {
            var displayName = ResolveDisplayName(command.Target);
            _feedbackNotifier?.NotifyStarting(command.Action, displayName);
            var succeeded = false;
            try
            {
                succeeded = ControlDeviceCore(command);
            }
            catch (Exception ex)
            {
                _logger?.Log("Bluetooth device control failed in background.", ex);
            }
            finally
            {
                _feedbackNotifier?.NotifyCompleted(command.Action, displayName, succeeded);
            }
        }, CancellationToken.None);

        return Task.FromResult(true);
    }

    private string ResolveDisplayName(string target)
    {
        try
        {
            var resolved = _resolveDeviceDisplayName(target);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }
        catch (Exception ex)
        {
            _logger?.Log($"Failed to resolve Bluetooth device display name for '{target}'.", ex);
        }

        return target;
    }

    private static string? DefaultResolveDeviceDisplayName(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        try
        {
            var paired = GetPairedDevices();
            if (TerminalCommandParameterRules.TryParseBluetoothMacAddress(target, out var targetAddress))
            {
                var matchByAddress = paired.FirstOrDefault(d => d.Address == targetAddress);
                if (matchByAddress is not null && !string.IsNullOrWhiteSpace(matchByAddress.DisplayName))
                {
                    return matchByAddress.DisplayName;
                }
            }

            var exactMatch = paired.FirstOrDefault(d => string.Equals(d.DisplayName, target, StringComparison.OrdinalIgnoreCase));
            if (exactMatch is not null && !string.IsNullOrWhiteSpace(exactMatch.DisplayName))
            {
                return exactMatch.DisplayName;
            }

            var partialMatch = paired.FirstOrDefault(d => d.DisplayName.Contains(target, StringComparison.OrdinalIgnoreCase));
            if (partialMatch is not null && !string.IsNullOrWhiteSpace(partialMatch.DisplayName))
            {
                return partialMatch.DisplayName;
            }
        }
        catch
        {
            // Ignore pairing enumeration errors in fallback display name resolution
        }

        return null;
    }

    private bool ControlDeviceCore(BluetoothAudioCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Target))
        {
            _logger?.Log("Bluetooth control failed: target is empty.");
            return false;
        }

        if (!TerminalCommandParameterRules.TryParseBluetoothMacAddress(command.Target, out var address))
        {
            var found = _findDeviceAddressByName(command.Target);
            if (found is null)
            {
                _logger?.Log($"Bluetooth control failed: device '{command.Target}' not found.");
                return false;
            }

            address = found.Value;
        }

        if (command.Action == BluetoothAudioActionKind.Disconnect)
        {
            _setServiceState(address, AudioSinkServiceClassGuid, false);
            _setServiceState(address, HandsfreeServiceClassGuid, false);
            return true;
        }

        // Connect
        var installedServices = _getInstalledServices(address);
        var wasAudioSinkInstalled = installedServices.Contains(AudioSinkServiceClassGuid);
        if (wasAudioSinkInstalled)
        {
            _setServiceState(address, AudioSinkServiceClassGuid, false);
        }

        var result = _setServiceState(address, AudioSinkServiceClassGuid, true);
        if (!wasAudioSinkInstalled && result != ErrorSuccess && result != ErrorAlreadyExists)
        {
            // Fallback: if fast-path enable failed, force a 0 -> 1 state transition.
            _setServiceState(address, AudioSinkServiceClassGuid, false);
            result = _setServiceState(address, AudioSinkServiceClassGuid, true);
        }

        if (!installedServices.Contains(HandsfreeServiceClassGuid))
        {
            _setServiceState(address, HandsfreeServiceClassGuid, true);
        }

        if (result != ErrorSuccess && result != ErrorAlreadyExists)
        {
            _logger?.Log($"Bluetooth SetServiceState failed with code {result}.");
            return false;
        }

        return true;
    }

    public static ulong? FindDeviceAddressByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var paired = GetPairedDevices();
        var exact = paired.FirstOrDefault(d => string.Equals(d.DisplayName, name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Address;
        }

        var partial = paired.FirstOrDefault(d => d.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase));
        return partial?.Address;
    }

    public static IReadOnlyList<PairedBluetoothAudioDeviceEntry> GetPairedDevices()
    {
        var results = new List<PairedBluetoothAudioDeviceEntry>();

        var searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
            fReturnRemembered = true,
            fReturnAuthenticated = true,
            fReturnConnected = true,
            fReturnUnknown = false,
            fIssueInquiry = false,
            cTimeoutMultiplier = 0,
            hRadio = IntPtr.Zero
        };

        var deviceInfo = new BLUETOOTH_DEVICE_INFO
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
        };

        var hFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
        if (hFind == IntPtr.Zero)
        {
            return results;
        }

        try
        {
            do
            {
                var name = deviceInfo.szName;
                var mac = TerminalCommandParameterRules.FormatBluetoothMacAddress(deviceInfo.Address);
                results.Add(new PairedBluetoothAudioDeviceEntry(
                    name,
                    mac,
                    deviceInfo.Address,
                    deviceInfo.fConnected));

                deviceInfo = new BLUETOOTH_DEVICE_INFO
                {
                    dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
                };
            }
            while (BluetoothFindNextDevice(hFind, ref deviceInfo));
        }
        finally
        {
            BluetoothFindDeviceClose(hFind);
        }

        return results
            .OrderByDescending(d => d.IsConnected)
            .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlySet<Guid> NativeGetInstalledServices(ulong address)
    {
        var deviceInfo = new BLUETOOTH_DEVICE_INFO
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
            Address = address
        };

        uint numServices = 0;
        _ = BluetoothEnumerateInstalledServices(IntPtr.Zero, ref deviceInfo, ref numServices, null);
        if (numServices == 0)
        {
            return new HashSet<Guid>();
        }

        var guids = new Guid[numServices];
        var ret = BluetoothEnumerateInstalledServices(IntPtr.Zero, ref deviceInfo, ref numServices, guids);
        return ret == ErrorSuccess ? new HashSet<Guid>(guids) : new HashSet<Guid>();
    }

    private static uint NativeSetServiceState(ulong address, Guid serviceGuid, bool enable)
    {
        var deviceInfo = new BLUETOOTH_DEVICE_INFO
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
            Address = address
        };

        const uint BluetoothServiceDisable = 0x00;
        const uint BluetoothServiceEnable = 0x01;
        var flags = enable ? BluetoothServiceEnable : BluetoothServiceDisable;

        return BluetoothSetServiceState(IntPtr.Zero, ref deviceInfo, ref serviceGuid, flags);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnAuthenticated;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnUnknown;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public IntPtr hRadio;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    [DllImport("BluetoothAPIs.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BluetoothFindFirstDevice(
        ref BLUETOOTH_DEVICE_SEARCH_PARAMS pSearchParams,
        ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("BluetoothAPIs.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextDevice(
        IntPtr hFind,
        ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    private static extern uint BluetoothSetServiceState(
        IntPtr hRadio,
        ref BLUETOOTH_DEVICE_INFO pbtdi,
        ref Guid pGuidService,
        uint dwServiceFlags);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    private static extern uint BluetoothEnumerateInstalledServices(
        IntPtr hRadio,
        ref BLUETOOTH_DEVICE_INFO pbtdi,
        ref uint pcServiceInout,
        [Out] Guid[]? pGuidServices);
}
