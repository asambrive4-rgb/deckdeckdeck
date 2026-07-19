using System.Text.RegularExpressions;

namespace DeckDeckDeck.App.Domain;

/// <summary>
/// Terminal command placeholders use <c>{{Name}}</c> syntax.
/// Names are taken as written (trimmed). First occurrence order is preserved.
/// </summary>
public static partial class TerminalCommandParameterRules
{
    public const string EmptyParameterNameMessage = "값 이름을 입력해 주세요.";
    public const string InvalidParameterNameMessage = "값 이름에는 { 또는 }를 넣을 수 없습니다.";
    public const string DuplicateParameterNameMessage = "이미 추가된 값 이름입니다.";

    public const string AdbIpParameterName = "IP";
    public const string AdbPortParameterName = "Port";

    public const string AdbWirelessPowerShellExample =
        @"& ""$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"" connect {{IP}}:{{Port}}";

    public const string EmptyAdbIpMessage = "ADB IP 주소를 입력해 주세요.";
    public const string InvalidAdbIpMessage = "IP는 숫자와 점(.)만 입력해 주세요. 예: 10.42.17.83";
    public const string EmptyAdbPortMessage = "포트를 입력해 주세요.";
    public const string InvalidAdbPortMessage = "포트는 1~65535 사이 숫자만 입력해 주세요.";

    public static bool IsAdbWirelessConnectCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var names = ExtractParameterNames(command);
        if (!names.Contains(AdbPortParameterName, StringComparer.Ordinal))
        {
            return false;
        }

        return command.Contains("adb", StringComparison.OrdinalIgnoreCase)
            && command.Contains("connect", StringComparison.OrdinalIgnoreCase);
    }

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

    public static IReadOnlyList<string> ExtractParameterNames(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in PlaceholderRegex().Matches(command))
        {
            var name = match.Groups[1].Value.Trim();
            if (name.Length == 0 || !seen.Add(name))
            {
                continue;
            }

            names.Add(name);
        }

        return names;
    }

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

    public static string FormatParameterToken(string name)
    {
        return "{{" + name + "}}";
    }

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

    private static string CollapseExtraWhitespace(string value)
    {
        var collapsed = WhitespaceRegex().Replace(value, " ");
        return collapsed.Trim();
    }

    [GeneratedRegex(@"\{\{([^{}]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    // Digits and dots only, at least one digit. Keeps input simple for wireless debugging.
    [GeneratedRegex(@"^\d+(?:\.\d+){0,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4LikeRegex();
}
