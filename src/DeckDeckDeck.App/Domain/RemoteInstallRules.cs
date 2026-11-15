// 역할: Android 원격설치 시 기기 특성·화면 크기(sw dp)·하드웨어 기능·모델명을 조합해 태블릿·폰·워치를 분류하고 모듈 매칭과 빌드 규칙을 정의합니다.
using System.Text.RegularExpressions;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

public sealed record AndroidDeviceClassificationResult(
    RemoteInstallDeviceKind DeviceKind,
    int? SmallestWidthDp,
    string ClassificationSource);

public static partial class RemoteInstallRules
{
    public const int TabletSmallestWidthThresholdDp = 600;
    public const int WatchMaxSmallestWidthDp = 280;

    public const string ProjectPathRequiredMessage = "Android 프로젝트 폴더를 선택해 주세요.";
    public const string ModuleRequiredMessage = "빌드할 Android 앱 모듈을 선택하거나 입력해 주세요.";
    public const string DeviceKindRequiredMessage = "설치할 기기 종류(태블릿/폰/워치)를 하나 이상 선택해 주세요.";

    /// <summary>
    /// Maps Numpad 4 -> Tablet, Numpad 5 -> Phone, Numpad 6 -> Watch as the default device order.
    /// </summary>
    public static RemoteInstallDeviceKind GetDefaultDeviceKindForSlot(SlotKey slotKey) => slotKey switch
    {
        SlotKey.Numpad4 => RemoteInstallDeviceKind.Tablet,
        SlotKey.Numpad5 => RemoteInstallDeviceKind.Phone,
        SlotKey.Numpad6 => RemoteInstallDeviceKind.Watch,
        _ => RemoteInstallDeviceKind.Phone
    };

    public static IReadOnlyList<RemoteInstallDeviceKind> SplitSelectedDeviceKinds(RemoteInstallDeviceKind kinds)
    {
        var list = new List<RemoteInstallDeviceKind>(3);
        if (kinds.HasFlag(RemoteInstallDeviceKind.Tablet))
        {
            list.Add(RemoteInstallDeviceKind.Tablet);
        }

        if (kinds.HasFlag(RemoteInstallDeviceKind.Phone))
        {
            list.Add(RemoteInstallDeviceKind.Phone);
        }

        if (kinds.HasFlag(RemoteInstallDeviceKind.Watch))
        {
            list.Add(RemoteInstallDeviceKind.Watch);
        }

        return list;
    }

    public static bool MatchesDeviceKind(
        RemoteInstallDeviceKind targetKinds,
        RemoteInstallDeviceKind actualDeviceKind) =>
        actualDeviceKind != RemoteInstallDeviceKind.None
        && (targetKinds & actualDeviceKind) != RemoteInstallDeviceKind.None;

    public static string GetDeviceKindKoreanLabel(RemoteInstallDeviceKind kind)
    {
        var selected = SplitSelectedDeviceKinds(kind);
        if (selected.Count == 0)
        {
            return "폰";
        }

        return string.Join("·", selected.Select(static single => single switch
        {
            RemoteInstallDeviceKind.Tablet => "태블릿",
            RemoteInstallDeviceKind.Phone => "폰",
            RemoteInstallDeviceKind.Watch => "워치",
            _ => "폰"
        }));
    }

    public static string GetDeviceKindBadgeText(RemoteInstallDeviceKind kind)
    {
        var selected = SplitSelectedDeviceKinds(kind);
        if (selected.Count == 1)
        {
            return selected[0] switch
            {
                RemoteInstallDeviceKind.Tablet => "태블릿 (4)",
                RemoteInstallDeviceKind.Phone => "폰 (5)",
                RemoteInstallDeviceKind.Watch => "워치 (6)",
                _ => "폰"
            };
        }

        return GetDeviceKindKoreanLabel(kind);
    }

    /// <summary>
    /// Classifies an Android device into Tablet / Phone / Watch using a 4-stage pipeline:
    /// 1) ro.build.characteristics + pm list features (watch / tablet flags)
    /// 2) Screen smallest width dp (wm size + wm density -> sw >= 600dp => Tablet)
    /// 3) Product model / manufacturer keywords (SM-R/Watch -> Watch, SM-X/SM-T/Tab/Pad -> Tablet)
    /// 4) Fallback to Phone
    /// </summary>
    public static AndroidDeviceClassificationResult ClassifyDevice(
        string? characteristics,
        string? featureList,
        string? wmSizeOutput,
        string? wmDensityOutput,
        string? model,
        string? manufacturer = null)
    {
        var hasSw = TryCalculateSmallestWidthDp(wmSizeOutput, wmDensityOutput, out var swDp);
        int? nullableSwDp = hasSw ? swDp : null;

        var charText = (characteristics ?? string.Empty).Trim();
        var features = (featureList ?? string.Empty).Trim();
        var modelText = $"{manufacturer} {model}".Trim();

        // Stage 1: Explicit hardware/system characteristics & feature flags
        if (ContainsTokenIgnoreCase(charText, "watch")
            || features.Contains("android.hardware.type.watch", StringComparison.OrdinalIgnoreCase))
        {
            return new AndroidDeviceClassificationResult(
                RemoteInstallDeviceKind.Watch,
                nullableSwDp,
                "시스템 특성(watch)");
        }

        if (ContainsTokenIgnoreCase(charText, "tablet"))
        {
            return new AndroidDeviceClassificationResult(
                RemoteInstallDeviceKind.Tablet,
                nullableSwDp,
                "시스템 특성(tablet)");
        }

        // Stage 2: Logical screen smallest-width dp (sw >= 600dp is official Android tablet breakpoint)
        if (hasSw)
        {
            if (swDp >= TabletSmallestWidthThresholdDp)
            {
                return new AndroidDeviceClassificationResult(
                    RemoteInstallDeviceKind.Tablet,
                    swDp,
                    $"화면 크기({swDp}dp ≥ 600dp)");
            }

            if (swDp <= WatchMaxSmallestWidthDp && IsLikelyWatchModel(modelText))
            {
                return new AndroidDeviceClassificationResult(
                    RemoteInstallDeviceKind.Watch,
                    swDp,
                    $"화면 크기({swDp}dp) + 모델명");
            }
        }

        // Stage 3: Model / product naming heuristics
        if (IsLikelyWatchModel(modelText))
        {
            return new AndroidDeviceClassificationResult(
                RemoteInstallDeviceKind.Watch,
                nullableSwDp,
                "기기 모델명(워치)");
        }

        if (IsLikelyTabletModel(modelText))
        {
            return new AndroidDeviceClassificationResult(
                RemoteInstallDeviceKind.Tablet,
                nullableSwDp,
                "기기 모델명(태블릿)");
        }

        // Stage 4: Default to Phone
        return new AndroidDeviceClassificationResult(
            RemoteInstallDeviceKind.Phone,
            nullableSwDp,
            hasSw ? $"화면 크기({swDp}dp)" : "기본 분류(폰)");
    }

    public static bool TryCalculateSmallestWidthDp(
        string? wmSizeOutput,
        string? wmDensityOutput,
        out int smallestWidthDp)
    {
        smallestWidthDp = 0;
        if (!TryParseWmSize(wmSizeOutput, out var widthPx, out var heightPx)
            || !TryParseWmDensity(wmDensityOutput, out var densityDpi)
            || densityDpi <= 0)
        {
            return false;
        }

        var minPx = Math.Min(widthPx, heightPx);
        var calculated = (int)Math.Round(minPx * 160.0 / densityDpi);
        if (calculated <= 0)
        {
            return false;
        }

        smallestWidthDp = calculated;
        return true;
    }

    public static bool TryParseWmSize(string? wmSizeOutput, out int widthPx, out int heightPx)
    {
        widthPx = 0;
        heightPx = 0;
        if (string.IsNullOrWhiteSpace(wmSizeOutput))
        {
            return false;
        }

        var overrideMatch = OverrideSizeRegex().Match(wmSizeOutput);
        if (overrideMatch.Success
            && int.TryParse(overrideMatch.Groups[1].Value, out widthPx)
            && int.TryParse(overrideMatch.Groups[2].Value, out heightPx)
            && widthPx > 0 && heightPx > 0)
        {
            return true;
        }

        var physicalMatch = PhysicalOrAnySizeRegex().Match(wmSizeOutput);
        if (physicalMatch.Success
            && int.TryParse(physicalMatch.Groups[1].Value, out widthPx)
            && int.TryParse(physicalMatch.Groups[2].Value, out heightPx)
            && widthPx > 0 && heightPx > 0)
        {
            return true;
        }

        return false;
    }

    public static bool TryParseWmDensity(string? wmDensityOutput, out int densityDpi)
    {
        densityDpi = 0;
        if (string.IsNullOrWhiteSpace(wmDensityOutput))
        {
            return false;
        }

        var overrideMatch = OverrideDensityRegex().Match(wmDensityOutput);
        if (overrideMatch.Success
            && int.TryParse(overrideMatch.Groups[1].Value, out densityDpi)
            && densityDpi > 0)
        {
            return true;
        }

        var physicalMatch = PhysicalOrAnyDensityRegex().Match(wmDensityOutput);
        if (physicalMatch.Success
            && int.TryParse(physicalMatch.Groups[1].Value, out densityDpi)
            && densityDpi > 0)
        {
            return true;
        }

        return false;
    }

    public static string? NormalizeModuleName(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return null;
        }

        var trimmed = moduleName.Trim().Replace('/', ':').Replace('\\', ':');
        while (trimmed.Contains("::", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("::", ":", StringComparison.Ordinal);
        }

        trimmed = trimmed.Trim(':');
        return string.IsNullOrWhiteSpace(trimmed) ? null : $":{trimmed}";
    }

    public static string? RecommendModuleForDeviceKind(
        IReadOnlyList<string> moduleNames,
        RemoteInstallDeviceKind deviceKind)
    {
        if (moduleNames.Count == 0)
        {
            return null;
        }

        if (moduleNames.Count == 1)
        {
            return moduleNames[0];
        }

        var hasWatch = deviceKind.HasFlag(RemoteInstallDeviceKind.Watch);
        var hasTablet = deviceKind.HasFlag(RemoteInstallDeviceKind.Tablet);
        var hasPhone = deviceKind.HasFlag(RemoteInstallDeviceKind.Phone);

        if (hasWatch && !hasTablet && !hasPhone)
        {
            return moduleNames.FirstOrDefault(static m => ContainsSegment(m, "wear") || ContainsSegment(m, "watch"))
                ?? moduleNames[0];
        }

        if (hasTablet && !hasPhone)
        {
            return moduleNames.FirstOrDefault(static m => ContainsSegment(m, "tablet") || ContainsSegment(m, "tab"))
                ?? moduleNames.FirstOrDefault(static m => ContainsSegment(m, "phone"))
                ?? moduleNames.FirstOrDefault(static m => !ContainsSegment(m, "wear") && !ContainsSegment(m, "watch"))
                ?? moduleNames[0];
        }

        return moduleNames.FirstOrDefault(static m => ContainsSegment(m, "phone"))
            ?? moduleNames.FirstOrDefault(static m => !ContainsSegment(m, "wear") && !ContainsSegment(m, "watch"))
            ?? moduleNames[0];
    }

    public static string RecommendDefaultSlotTitle(string? projectPath, RemoteInstallDeviceKind deviceKind)
    {
        var kindLabel = GetDeviceKindKoreanLabel(deviceKind);
        return $"{kindLabel} 원격설치";
    }

    public static bool IsAutoGeneratedSlotTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return true;
        }

        var trimmed = title.Trim();
        return trimmed is "새 실행 항목"
            or "태블릿 원격설치"
            or "폰 원격설치"
            or "워치 원격설치"
            or "태블릿·폰 원격설치"
            or "태블릿·워치 원격설치"
            or "폰·워치 원격설치"
            or "태블릿·폰·워치 원격설치"
            or "태블릿"
            or "폰"
            or "워치";
    }

    /// <summary>
    /// Builds the Gradle assemble task for the target module.
    /// </summary>
    public static string BuildAssembleTaskName(string moduleName, string variant = "debug")
    {
        var normalizedModule = NormalizeModuleName(moduleName) ?? ":app";

        // NOTE(release-build): 현재는 Debug 빌드(assembleDebug)만 지원합니다.
        // 추후 Release 빌드를 지원할 때는 variant에 "release"를 전달하여 ":module:assembleRelease" 태스크를 실행하도록 확장하고,
        // APK 출력 탐색 경로(build/outputs/apk/release/output-metadata.json) 및 서명되지 않은(*-unsigned.apk) 파일에 대한
        // 예외 안내를 추가하면 동일 파이프라인에서 Release 원격설치를 그대로 사용할 수 있습니다.
        var normalizedVariant = string.IsNullOrWhiteSpace(variant) ? "debug" : variant.Trim();
        var capitalizedVariant = char.ToUpperInvariant(normalizedVariant[0]) + normalizedVariant[1..].ToLowerInvariant();
        return $"{normalizedModule}:assemble{capitalizedVariant}";
    }

    public static string? ExtractIpFromSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        var trimmed = serial.Trim();
        var colonIndex = trimmed.IndexOf(':');
        var candidate = colonIndex > 0 ? trimmed[..colonIndex] : trimmed;
        return TerminalCommandParameterRules.TryNormalizeAdbIp(candidate, out var ip, out _)
            ? ip
            : null;
    }

    public static bool MatchesTargetFilter(
        string serial,
        string model,
        string? ipAddress,
        string? targetFilter)
    {
        if (string.IsNullOrWhiteSpace(targetFilter))
        {
            return true;
        }

        var filter = targetFilter.Trim();
        if (serial.Equals(filter, StringComparison.OrdinalIgnoreCase)
            || serial.StartsWith(filter + ":", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(ipAddress)
            && ipAddress.Equals(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(model)
            && model.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryExtractInstallFailureCode(
        string? combinedOutput,
        out string failureCode,
        out bool isUpdateIncompatible)
    {
        failureCode = string.Empty;
        isUpdateIncompatible = false;

        if (string.IsNullOrWhiteSpace(combinedOutput))
        {
            return false;
        }

        var match = InstallFailedRegex().Match(combinedOutput);
        if (!match.Success)
        {
            return false;
        }

        failureCode = match.Groups[1].Value;
        isUpdateIncompatible = failureCode.Equals(
            "INSTALL_FAILED_UPDATE_INCOMPATIBLE",
            StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool ContainsSegment(string moduleName, string keyword)
    {
        var segments = moduleName.Split(':', '/', '\\', '-', '_');
        return segments.Any(s => s.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTokenIgnoreCase(string text, string token) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool IsLikelyWatchModel(string modelText)
    {
        if (string.IsNullOrWhiteSpace(modelText))
        {
            return false;
        }

        return modelText.Contains("watch", StringComparison.OrdinalIgnoreCase)
            || modelText.Contains("wear", StringComparison.OrdinalIgnoreCase)
            || WatchModelCodeRegex().IsMatch(modelText);
    }

    private static bool IsLikelyTabletModel(string modelText)
    {
        if (string.IsNullOrWhiteSpace(modelText))
        {
            return false;
        }

        return modelText.Contains("tablet", StringComparison.OrdinalIgnoreCase)
            || modelText.Contains("galaxy tab", StringComparison.OrdinalIgnoreCase)
            || modelText.Contains("ipad", StringComparison.OrdinalIgnoreCase)
            || modelText.Contains("pad", StringComparison.OrdinalIgnoreCase)
            || TabletModelCodeRegex().IsMatch(modelText);
    }

    [GeneratedRegex(@"Override size:\s*(\d+)x(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex OverrideSizeRegex();

    [GeneratedRegex(@"(?:Physical size:\s*)?(\d+)x(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PhysicalOrAnySizeRegex();

    [GeneratedRegex(@"Override density:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex OverrideDensityRegex();

    [GeneratedRegex(@"(?:Physical density|Density):\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PhysicalOrAnyDensityRegex();

    [GeneratedRegex(@"\b(INSTALL_FAILED_[A-Z0-9_]+)\b")]
    private static partial Regex InstallFailedRegex();

    [GeneratedRegex(@"\bSM-R\d{3}[A-Z0-9]*\b", RegexOptions.IgnoreCase)]
    private static partial Regex WatchModelCodeRegex();

    [GeneratedRegex(@"\bSM-[XT]\d{3}[A-Z0-9]*\b", RegexOptions.IgnoreCase)]
    private static partial Regex TabletModelCodeRegex();
}
