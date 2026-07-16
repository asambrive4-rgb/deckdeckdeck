using System.Text;

namespace DeckDeckDeck.App.Domain;

/// <summary>
/// 블루투스 오디오 상태의 표시와 물리 장치 매칭에 사용하는 순수 규칙.
/// </summary>
public static class BluetoothAudioStatusRules
{
    public const string LoadingText = "확인 중…";
    public const string LoadingToolTip = "블루투스 오디오 상태를 확인하고 있습니다.";
    public const string DisconnectedText = "연결 없음";
    public const string DisconnectedToolTip = "기본 출력으로 연결된 블루투스 오디오가 없습니다.";
    public const string BatteryUnavailableToolTip = "배터리 정보를 확인할 수 없습니다.";

    public static string FormatDisplayText(string? deviceName, int? batteryPercent)
    {
        var cleaned = CleanDeviceName(deviceName);
        if (cleaned.Length == 0)
        {
            return DisconnectedText;
        }

        return batteryPercent is >= 0 and <= 100
            ? $"{cleaned} · {batteryPercent.Value}%"
            : cleaned;
    }

    public static string FormatToolTip(string? deviceName, int? batteryPercent)
    {
        var cleaned = CleanDeviceName(deviceName);
        if (cleaned.Length == 0)
        {
            return DisconnectedToolTip;
        }

        return batteryPercent is >= 0 and <= 100
            ? $"{cleaned} · {batteryPercent.Value}%"
            : $"{cleaned}\n{BatteryUnavailableToolTip}";
    }

    public static string CleanDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return string.Empty;
        }

        var name = StripAudioEndpointWrapper(deviceName.Trim());
        string[] suffixes =
        [
            " Hands-Free AG Audio",
            " Hands-Free HF Audio",
            " Hands-Free HF",
            " Hands-Free",
            " A2DP SNK",
            " Stereo",
            " AG Audio",
            " Avrcp Transport",
            " Avrcp 전송",
            " AVRCP",
            " Avrcp"
        ];

        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length].Trim();
            }
        }

        return name;
    }

    public static string NormalizeDeviceName(string? deviceName)
    {
        var cleaned = CleanDeviceName(deviceName);
        if (cleaned.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(cleaned.Length);
        var pendingSpace = false;
        foreach (var character in cleaned)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    public static bool LooksLikeBluetoothPeripheralId(string? instanceIdOrPath)
    {
        if (string.IsNullOrWhiteSpace(instanceIdOrPath))
        {
            return false;
        }

        return instanceIdOrPath.Contains(@"BTHENUM\", StringComparison.OrdinalIgnoreCase)
            || instanceIdOrPath.Contains(@"BTHHFENUM\", StringComparison.OrdinalIgnoreCase)
            || instanceIdOrPath.Contains(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase)
            || instanceIdOrPath.Contains(@"BTHLEDEVICE\", StringComparison.OrdinalIgnoreCase)
            || instanceIdOrPath.Contains("BLUETOOTHDEVICE_", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> ExtractBluetoothAddressTokens(string? instanceIdOrPath)
    {
        if (string.IsNullOrWhiteSpace(instanceIdOrPath))
        {
            return [];
        }

        var text = instanceIdOrPath.ToUpperInvariant();
        var results = new HashSet<string>(StringComparer.Ordinal);
        CollectAddressAfterMarker(text, "DEV_", results);
        CollectAddressAfterMarker(text, "BLUETOOTHDEVICE_", results);

        for (var index = 0; index + 13 <= text.Length; index++)
        {
            if (text[index] is not ('&' or '_'))
            {
                continue;
            }

            var candidate = text.Substring(index + 1, 12);
            if (IsTwelveHex(candidate))
            {
                results.Add(candidate);
            }
        }

        return results.ToList();
    }

    internal static IReadOnlyList<BluetoothDeviceMatchCandidate> SelectBestDeviceGroup(
        BluetoothDeviceMatchContext context,
        IReadOnlyList<BluetoothDeviceMatchCandidate> candidates)
    {
        foreach (var containerId in context.ContainerIds.Where(id => id != Guid.Empty))
        {
            var matched = candidates.Where(candidate => candidate.ContainerId == containerId).ToList();
            if (matched.Count > 0)
            {
                return matched;
            }
        }

        foreach (var address in context.AddressTokens)
        {
            var matched = candidates
                .Where(candidate => candidate.AddressTokens.Contains(address, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (matched.Count > 0)
            {
                return ExpandPhysicalGroup(matched[0], candidates);
            }
        }

        var normalizedEndpointName = NormalizeDeviceName(context.DeviceName);
        if (normalizedEndpointName.Length == 0)
        {
            return [];
        }

        var nameMatches = candidates
            .Where(candidate => string.Equals(
                NormalizeDeviceName(candidate.DeviceName),
                normalizedEndpointName,
                StringComparison.Ordinal))
            .ToList();
        var physicalGroups = nameMatches
            .GroupBy(GetPhysicalGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return physicalGroups.Count == 1 ? physicalGroups[0].ToList() : [];
    }

    private static IReadOnlyList<BluetoothDeviceMatchCandidate> ExpandPhysicalGroup(
        BluetoothDeviceMatchCandidate selected,
        IReadOnlyList<BluetoothDeviceMatchCandidate> candidates)
    {
        var key = GetPhysicalGroupKey(selected);
        return candidates
            .Where(candidate => string.Equals(GetPhysicalGroupKey(candidate), key, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string GetPhysicalGroupKey(BluetoothDeviceMatchCandidate candidate)
    {
        if (candidate.ContainerId is { } containerId && containerId != Guid.Empty)
        {
            return "container:" + containerId.ToString("D");
        }

        var address = candidate.AddressTokens.FirstOrDefault();
        return address is not null ? "address:" + address : "instance:" + candidate.InstanceId;
    }

    private static string StripAudioEndpointWrapper(string name)
    {
        string[] prefixes = ["Headphones", "Headset", "헤드폰", "헤드셋"];
        foreach (var prefix in prefixes)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var open = name.IndexOf('(');
            if (open >= prefix.Length && name.EndsWith(')') && open + 1 < name.Length - 1)
            {
                return name[(open + 1)..^1].Trim();
            }
        }

        return name;
    }

    private static void CollectAddressAfterMarker(
        string text,
        string marker,
        HashSet<string> results)
    {
        var start = 0;
        while (start < text.Length)
        {
            var markerIndex = text.IndexOf(marker, start, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return;
            }

            var addressIndex = markerIndex + marker.Length;
            if (addressIndex + 12 <= text.Length)
            {
                var candidate = text.Substring(addressIndex, 12);
                if (IsTwelveHex(candidate))
                {
                    results.Add(candidate);
                }
            }

            start = markerIndex + marker.Length;
        }
    }

    private static bool IsTwelveHex(string value)
    {
        return value.Length == 12 && value.All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');
    }
}

internal sealed record BluetoothDeviceMatchContext(
    string DeviceName,
    IReadOnlyList<Guid> ContainerIds,
    IReadOnlySet<string> AddressTokens);

internal sealed record BluetoothDeviceMatchCandidate(
    string InstanceId,
    string DeviceName,
    Guid? ContainerId,
    IReadOnlySet<string> AddressTokens);
