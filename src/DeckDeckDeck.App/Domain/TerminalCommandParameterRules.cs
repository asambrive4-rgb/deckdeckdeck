// 역할: 터미널 명령어 내의 동적 입력 변수({변수명}), 무선 ADB 연결, 화면 끄기 및 블루투스 오디오 연결/해제 명령을 추출·판별·치환하는 규칙을 담당합니다.
using System.Text.RegularExpressions;

namespace DeckDeckDeck.App.Domain;

public enum BluetoothAudioActionKind
{
    Connect,
    Disconnect
}

public sealed record BluetoothAudioCommand(
    BluetoothAudioActionKind Action,
    string Target);

/// <summary>
/// Terminal command placeholders use <c>{{Name}}</c> syntax.
/// Names are taken as written (trimmed). First occurrence order is preserved.
/// </summary>
public static partial class TerminalCommandParameterRules
{
    public const string EmptyParameterNameMessage = "값 이름을 입력해 주세요.";
    public const string InvalidParameterNameMessage = "값 이름에는 { 또는 }를 넣을 수 없습니다.";
    public const string DuplicateParameterNameMessage = "이미 추가된 값 이름입니다.";

    public const string BluetoothAudioCommandPrefix = "bt-audio";

    public const string AdbIpParameterName = "IP";
    public const string AdbPortParameterName = "Port";

    public const string AdbWirelessPowerShellExample =
        @"& ""$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"" connect {{IP}}:{{Port}}";

    public const string TurnOffDisplayCommand =
        @"(Add-Type '[DllImport(\""user32.dll\"")]public static extern int PostMessage(int hWnd,int hMsg,int wParam,int lParam);' -Name a -Passthru)::PostMessage(-1,0x0112,0xF170,2)";

    public const string EmptyAdbIpMessage = "ADB IP 주소를 입력해 주세요.";
    public const string InvalidAdbIpMessage = "IP는 숫자와 점(.)만 입력해 주세요. 예: 10.42.17.83";
    public const string EmptyAdbPortMessage = "포트를 입력해 주세요.";
    public const string InvalidAdbPortMessage = "포트는 1~65535 사이 숫자만 입력해 주세요.";

    public static string BuildBluetoothAudioCommand(BluetoothAudioActionKind action, string? target)
    {
        var verb = action == BluetoothAudioActionKind.Disconnect ? "disconnect" : "connect";
        var normalizedTarget = NormalizeBluetoothTarget(target);
        return string.IsNullOrWhiteSpace(normalizedTarget)
            ? $"{BluetoothAudioCommandPrefix} {verb}"
            : $"{BluetoothAudioCommandPrefix} {verb} {normalizedTarget}";
    }

    public static string NormalizeBluetoothTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        var trimmed = target.Trim().Trim('"').Trim();
        if (TryParseBluetoothMacAddress(trimmed, out var mac))
        {
            return FormatBluetoothMacAddress(mac);
        }

        return trimmed;
    }

    public static bool TryParseBluetoothMacAddress(string? text, out ulong address)
    {
        address = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().Trim('"');
        var clean = trimmed.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (clean.Length != 12)
        {
            return false;
        }

        return ulong.TryParse(
            clean,
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out address);
    }

    public static string FormatBluetoothMacAddress(ulong address) =>
        $"{(address >> 40) & 0xFF:X2}:{(address >> 32) & 0xFF:X2}:{(address >> 24) & 0xFF:X2}:{(address >> 16) & 0xFF:X2}:{(address >> 8) & 0xFF:X2}:{address & 0xFF:X2}";

    public static bool IsBluetoothAudioCommand(string? command) =>
        TryParseBluetoothAudioCommand(command, out _);

    public static bool TryParseBluetoothAudioCommand(string? command, out BluetoothAudioCommand parsed) =>
        TryParseBluetoothAudioCommand(command, out parsed, allowEmptyTarget: false);

    public static bool TryParseBluetoothAudioCommand(
        string? command,
        out BluetoothAudioCommand parsed,
        bool allowEmptyTarget)
    {
        parsed = new BluetoothAudioCommand(BluetoothAudioActionKind.Connect, string.Empty);
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();

        // 1) 신규 표준 명령 (bt-audio [connect|on|disconnect|off] [target])
        if (trimmed.StartsWith(BluetoothAudioCommandPrefix, StringComparison.OrdinalIgnoreCase)
            && (trimmed.Length == BluetoothAudioCommandPrefix.Length || char.IsWhiteSpace(trimmed[BluetoothAudioCommandPrefix.Length])))
        {
            var rest = trimmed[BluetoothAudioCommandPrefix.Length..].TrimStart();
            if (rest.Length == 0)
            {
                return false;
            }

            var firstSpace = rest.IndexOfAny([' ', '\t']);
            string actionToken;
            string targetPart;
            if (firstSpace < 0)
            {
                actionToken = rest;
                targetPart = string.Empty;
            }
            else
            {
                actionToken = rest[..firstSpace].Trim();
                targetPart = rest[firstSpace..].Trim();
            }

            BluetoothAudioActionKind action;
            if (string.Equals(actionToken, "connect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionToken, "on", StringComparison.OrdinalIgnoreCase))
            {
                action = BluetoothAudioActionKind.Connect;
            }
            else if (string.Equals(actionToken, "disconnect", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(actionToken, "off", StringComparison.OrdinalIgnoreCase))
            {
                action = BluetoothAudioActionKind.Disconnect;
            }
            else
            {
                return false;
            }

            var normalizedTarget = NormalizeBluetoothTarget(targetPart);
            if (!allowEmptyTarget && string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return false;
            }

            parsed = new BluetoothAudioCommand(action, normalizedTarget);
            return true;
        }

        // 2) 레거시 btcom 스크립트 파싱
        if (trimmed.Contains("btcom", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("110b", StringComparison.OrdinalIgnoreCase))
        {
            var macMatch = BluetoothMacAddressRegex().Match(trimmed);
            if (!macMatch.Success || !TryParseBluetoothMacAddress(macMatch.Value, out var macAddress))
            {
                return false;
            }

            var lines = trimmed.Split(['\r', '\n', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            BluetoothAudioActionKind? detectedAction = null;

            foreach (var line in lines)
            {
                if (!line.Contains("110b", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (BtcomConnectFlagRegex().IsMatch(line))
                {
                    detectedAction = BluetoothAudioActionKind.Connect;
                }
                else if (BtcomDisconnectFlagRegex().IsMatch(line))
                {
                    detectedAction = BluetoothAudioActionKind.Disconnect;
                }
            }

            if (detectedAction is null)
            {
                return false;
            }

            parsed = new BluetoothAudioCommand(detectedAction.Value, FormatBluetoothMacAddress(macAddress));
            return true;
        }

        return false;
    }

    public static bool IsTurnOffDisplayCommand(string? command) =>
        !string.IsNullOrWhiteSpace(command)
        && command.Contains("0xF170", StringComparison.OrdinalIgnoreCase)
        && command.Contains("user32", StringComparison.OrdinalIgnoreCase);

    public static bool IsAdbWirelessConnectCommand(string? command) =>
        !string.IsNullOrWhiteSpace(command)
        && ExtractParameterNames(command).Contains(AdbPortParameterName, StringComparer.Ordinal)
        && command.Contains("adb", StringComparison.OrdinalIgnoreCase)
        && command.Contains("connect", StringComparison.OrdinalIgnoreCase);

    public static bool TryNormalizeAdbIp(
        string? ip,
        out string normalizedIp,
        out string? errorMessage)
    {
        normalizedIp = (ip ?? string.Empty).Trim();
        if (normalizedIp.Length == 0)
        {
            errorMessage = EmptyAdbIpMessage;
            return false;
        }

        if (!Ipv4LikeRegex().IsMatch(normalizedIp))
        {
            errorMessage = InvalidAdbIpMessage;
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static bool TryNormalizeAdbPort(
        string? port,
        out string normalizedPort,
        out string? errorMessage)
    {
        normalizedPort = (port ?? string.Empty).Trim();
        if (normalizedPort.Length == 0)
        {
            errorMessage = EmptyAdbPortMessage;
            return false;
        }

        if (!int.TryParse(normalizedPort, out var portNumber)
            || portNumber is < 1 or > 65535)
        {
            errorMessage = InvalidAdbPortMessage;
            return false;
        }

        normalizedPort = portNumber.ToString();
        errorMessage = null;
        return true;
    }

    public static bool TryNormalizeAdbEndpoint(
        string? ip,
        string? port,
        out string normalizedIp,
        out string normalizedPort,
        out string? errorMessage)
    {
        if (!TryNormalizeAdbIp(ip, out normalizedIp, out errorMessage))
        {
            normalizedPort = string.Empty;
            return false;
        }

        return TryNormalizeAdbPort(port, out normalizedPort, out errorMessage);
    }

    public static IReadOnlyList<string> ExtractParameterNames(string? command) =>
        string.IsNullOrWhiteSpace(command) || IsBluetoothAudioCommand(command)
            ? []
            : PlaceholderRegex().Matches(command)
                .Select(m => m.Groups[1].Value.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    public static string ApplyParameters(
        string command,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(values);

        return PlaceholderRegex().Replace(command, match =>
        {
            var name = match.Groups[1].Value.Trim();
            if (name.Length == 0)
            {
                return match.Value;
            }

            return values.TryGetValue(name, out var value)
                ? value
                : match.Value;
        });
    }

    public static string FormatParameterToken(string name) => "{{" + name + "}}";

    public static bool TryNormalizeParameterName(
        string? name,
        out string normalized,
        out string? errorMessage)
    {
        normalized = (name ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            errorMessage = EmptyParameterNameMessage;
            return false;
        }

        if (normalized.Contains('{', StringComparison.Ordinal)
            || normalized.Contains('}', StringComparison.Ordinal))
        {
            errorMessage = InvalidParameterNameMessage;
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static bool TryAddParameter(
        string? command,
        string? name,
        out string updatedCommand,
        out string? errorMessage)
    {
        if (!TryNormalizeParameterName(name, out var normalized, out errorMessage))
        {
            updatedCommand = command ?? string.Empty;
            return false;
        }

        var current = command ?? string.Empty;
        if (ExtractParameterNames(current).Contains(normalized, StringComparer.Ordinal))
        {
            updatedCommand = current;
            errorMessage = DuplicateParameterNameMessage;
            return false;
        }

        var token = FormatParameterToken(normalized);
        updatedCommand = string.IsNullOrWhiteSpace(current)
            ? token
            : current.TrimEnd() + " " + token;
        errorMessage = null;
        return true;
    }

    public static string RemoveParameter(string? command, string? name)
    {
        if (string.IsNullOrEmpty(command)
            || !TryNormalizeParameterName(name, out var normalized, out _))
        {
            return command ?? string.Empty;
        }

        var removed = PlaceholderRegex().Replace(command, match =>
        {
            var matchName = match.Groups[1].Value.Trim();
            return string.Equals(matchName, normalized, StringComparison.Ordinal)
                ? string.Empty
                : match.Value;
        });

        return CollapseExtraWhitespace(removed);
    }

    private static string CollapseExtraWhitespace(string value) =>
        WhitespaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"\{\{([^{}]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    // Digits and dots only, at least one digit. Keeps input simple for wireless debugging.
    [GeneratedRegex(@"^\d+(?:\.\d+){0,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4LikeRegex();

    [GeneratedRegex(@"\b[0-9A-Fa-f]{2}(?:[:-][0-9A-Fa-f]{2}){5}\b|\b[0-9A-Fa-f]{12}\b", RegexOptions.CultureInvariant)]
    private static partial Regex BluetoothMacAddressRegex();

    [GeneratedRegex(@"(?i)(?:^|\s)-c(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex BtcomConnectFlagRegex();

    [GeneratedRegex(@"(?i)(?:^|\s)-r(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex BtcomDisconnectFlagRegex();
}
