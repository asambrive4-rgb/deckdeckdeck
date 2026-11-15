// 역할: Android 프로젝트 모듈 탐색, ADB 연결 기기 4단계 분류, Gradle 증분 빌드, APK 설치 및 앱 자동 실행을 비동기 백그라운드로 수행합니다.
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed partial class AndroidRemoteInstallGateway : IAndroidRemoteInstallGateway
{
    private const string ProbeSeparator = "__DDD_PROBE_SEP__";

    private readonly IAppLogger? _logger;
    private readonly IRemoteInstallFeedbackNotifier? _feedbackNotifier;
    private readonly bool _waitForCompletion;
    private readonly SemaphoreSlim _buildQueueLock = new(1, 1);
    private int _queuedJobsCount;

    public AndroidRemoteInstallGateway(IAppLogger? logger = null)
        : this(
            logger,
            feedbackNotifier: new WpfRemoteInstallSnackbarNotifier(),
            waitForCompletion: false)
    {
    }

    internal AndroidRemoteInstallGateway(
        IAppLogger? logger,
        IRemoteInstallFeedbackNotifier? feedbackNotifier,
        bool waitForCompletion)
    {
        _logger = logger;
        _feedbackNotifier = feedbackNotifier;
        _waitForCompletion = waitForCompletion;
    }

    public IReadOnlyList<AndroidAppModuleInfo> ScanProjectModules(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return [];
        }

        string rootPath;
        try
        {
            rootPath = Path.GetFullPath(projectPath.Trim());
        }
        catch
        {
            return [];
        }

        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        var modules = new List<AndroidAppModuleInfo>();
        var settingsPath = FindFirstExistingFile(
            Path.Combine(rootPath, "settings.gradle.kts"),
            Path.Combine(rootPath, "settings.gradle"));

        if (settingsPath is not null)
        {
            try
            {
                var lines = File.ReadAllLines(settingsPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)
                        || !trimmed.Contains("include", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match match in ModuleIncludeRegex().Matches(trimmed))
                    {
                        var rawModule = match.Groups[1].Value;
                        var normalizedModule = RemoteInstallRules.NormalizeModuleName(rawModule);
                        if (string.IsNullOrWhiteSpace(normalizedModule))
                        {
                            continue;
                        }

                        var relativeDir = normalizedModule.TrimStart(':')
                            .Replace(':', Path.DirectorySeparatorChar);
                        var moduleDir = Path.Combine(rootPath, relativeDir);
                        if (TryInspectAndroidAppModule(normalizedModule, relativeDir, moduleDir, out var moduleInfo)
                            && modules.All(existing => !existing.ModuleName.Equals(moduleInfo.ModuleName, StringComparison.OrdinalIgnoreCase)))
                        {
                            modules.Add(moduleInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"Failed to scan settings.gradle at {settingsPath}.", ex);
            }
        }

        if (modules.Count == 0)
        {
            var fallbackAppDir = Path.Combine(rootPath, "app");
            if (TryInspectAndroidAppModule(":app", "app", fallbackAppDir, out var appModule))
            {
                modules.Add(appModule);
            }
        }

        return modules;
    }

    public async Task<IReadOnlyList<AndroidConnectedDeviceInfo>> ScanConnectedDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var adbPath = ResolveAdbExecutablePath();
        var devicesResult = await RunProcessCaptureAsync(
            adbPath,
            ["devices"],
            workingDirectory: null,
            onOutputLine: null,
            cancellationToken);

        if (devicesResult.ExitCode != 0 && string.IsNullOrWhiteSpace(devicesResult.StandardOutput))
        {
            return [];
        }

        var serials = new List<string>();
        foreach (var rawLine in devicesResult.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = AdbDeviceLineRegex().Match(rawLine);
            if (match.Success)
            {
                serials.Add(match.Groups[1].Value.Trim());
            }
        }

        if (serials.Count == 0)
        {
            return [];
        }

        var probeTasks = serials.Select(serial => ProbeDeviceAsync(adbPath, serial, cancellationToken));
        var probedDevices = await Task.WhenAll(probeTasks);
        return probedDevices.Where(static d => d is not null).Select(static d => d!).ToList();
    }

    public Task<bool> TryStartRemoteInstallAsync(
        AndroidRemoteInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.ProjectPath) || string.IsNullOrWhiteSpace(request.ModuleName))
        {
            return Task.FromResult(false);
        }

        if (_waitForCompletion)
        {
            return ExecuteDeploymentPipelineAsync(request, skipBuild: false, uninstallFirst: false, cancellationToken);
        }

        _ = Task.Run(
            () => ExecuteDeploymentPipelineAsync(request, skipBuild: false, uninstallFirst: false, CancellationToken.None),
            CancellationToken.None);

        return Task.FromResult(true);
    }

    private async Task<bool> ExecuteDeploymentPipelineAsync(
        AndroidRemoteInstallRequest request,
        bool skipBuild,
        bool uninstallFirst,
        CancellationToken externalCancellationToken,
        IReadOnlySet<string>? uninstallSerials = null)
    {
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
        var cancellationToken = jobCts.Token;
        var kindLabel = RemoteInstallRules.GetDeviceKindKoreanLabel(request.TargetDeviceKind);
        var normalizedModule = RemoteInstallRules.NormalizeModuleName(request.ModuleName) ?? request.ModuleName;
        var headerTitle = $"{request.Title} ({kindLabel} · {normalizedModule})";
        var logBuilder = new StringBuilder();
        var logLock = new object();

        void AppendLog(string line)
        {
            lock (logLock)
            {
                logBuilder.AppendLine(line);
            }
        }

        void CancelJob()
        {
            try
            {
                jobCts.Cancel();
            }
            catch
            {
                // Ignore disposal race
            }
        }

        var queuedOrder = Interlocked.Increment(ref _queuedJobsCount);
        var acquiredLock = false;
        try
        {
            if (queuedOrder > 1 && _buildQueueLock.CurrentCount == 0)
            {
                _feedbackNotifier?.NotifyProgress(
                    "대기",
                    headerTitle,
                    "이전 원격설치 작업 완료 대기 중...",
                    CancelJob);
            }

            await _buildQueueLock.WaitAsync(cancellationToken);
            acquiredLock = true;

            var totalStopwatch = Stopwatch.StartNew();

            // 1. Scan & Classify Connected Devices
            _feedbackNotifier?.NotifyProgress(
                "스캔",
                headerTitle,
                $"연결된 {kindLabel} 기기 탐색 중...",
                CancelJob);

            var allDevices = await ScanConnectedDevicesAsync(cancellationToken);
            logBuilder.AppendLine($"[스캔] 연결된 전체 ADB 기기: {allDevices.Count}대");
            foreach (var d in allDevices)
            {
                logBuilder.AppendLine($"  - {d.DisplaySummary} ({d.ClassificationSource})");
            }

            var resolution = ResolveTargetDevicesForRequest(
                allDevices,
                request.TargetDeviceKind,
                request.TargetDeviceFilter,
                selectDevicePrompt: _feedbackNotifier is not null
                    ? (kind, candidates) => _feedbackNotifier.PromptSelectDevice(request.Title, kind, candidates)
                    : null);

            if (resolution.WasCancelled)
            {
                return false;
            }

            if (resolution.SelectedDevices.Count == 0)
            {
                var connectedSummary = allDevices.Count == 0
                    ? "현재 연결된 ADB 기기가 없습니다 (1·2·3 슬롯으로 먼저 연결 후 확인)"
                    : $"현재 연결: {string.Join(", ", allDevices.Select(static d => $"{d.Model}({RemoteInstallRules.GetDeviceKindKoreanLabel(d.DeviceKind)})"))}";

                _feedbackNotifier?.NotifyFailed(
                    $"{headerTitle} - 기기 미연결",
                    $"연결된 {kindLabel} 기기가 없습니다. {connectedSummary}",
                    logBuilder.ToString(),
                    onRetry: () => _ = Task.Run(() => ExecuteDeploymentPipelineAsync(request, skipBuild: false, uninstallFirst: false, CancellationToken.None)));
                return false;
            }

            var targetDevices = resolution.SelectedDevices;
            var missingKinds = resolution.MissingKinds;
            var targetSummary = string.Join(" · ", targetDevices.Select(static d => d.Model));

            foreach (var device in targetDevices)
            {
                AppendLog($"[선택된 기기] {device.DisplaySummary}");
            }

            if (missingKinds.Count > 0)
            {
                var missingLabel = string.Join(", ", missingKinds.Select(RemoteInstallRules.GetDeviceKindKoreanLabel));
                AppendLog($"[미연결 건너뜀] {missingLabel} 기기가 연결되지 않아 연결된 기기에만 설치합니다.");
            }

            // 2. Resolve Project & Module Metadata
            var projectRoot = Path.GetFullPath(request.ProjectPath.Trim());
            if (!Directory.Exists(projectRoot))
            {
                _feedbackNotifier?.NotifyFailed(
                    $"{headerTitle} - 경로 오류",
                    $"프로젝트 폴더를 찾을 수 없습니다: {projectRoot}",
                    logBuilder.ToString());
                return false;
            }

            var modules = ScanProjectModules(projectRoot);
            var matchedModule = modules.FirstOrDefault(m =>
                    m.ModuleName.Equals(normalizedModule, StringComparison.OrdinalIgnoreCase))
                ?? new AndroidAppModuleInfo(
                    normalizedModule,
                    normalizedModule.TrimStart(':').Replace(':', Path.DirectorySeparatorChar),
                    ApplicationId: null);

            // 3. Incremental Gradle Build (unless skipBuild is true for quick reinstall)
            if (!skipBuild)
            {
                // NOTE(release-build): 현재는 Debug 빌드("debug" -> assembleDebug)만 실행합니다.
                // 추후 Release 빌드를 지원할 경우 request에 BuildVariant 필드를 추가하여 BuildAssembleTaskName에 전달합니다.
                var assembleTask = RemoteInstallRules.BuildAssembleTaskName(matchedModule.ModuleName, variant: "debug");
                var gradlewPath = ResolveGradleWrapperPath(projectRoot);

                _feedbackNotifier?.NotifyProgress(
                    "빌드",
                    headerTitle,
                    $"{targetSummary} 대상 {assembleTask} 준비 중...",
                    CancelJob);

                AppendLog($"[빌드 시작] {gradlewPath} {assembleTask} --parallel");

                var buildResult = await RunProcessCaptureAsync(
                    gradlewPath,
                    [assembleTask, "--parallel"],
                    workingDirectory: projectRoot,
                    onOutputLine: line =>
                    {
                        AppendLog(line);
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("> Task ", StringComparison.Ordinal)
                            || trimmed.StartsWith("FAILURE:", StringComparison.Ordinal)
                            || trimmed.StartsWith("BUILD ", StringComparison.Ordinal))
                        {
                            _feedbackNotifier?.NotifyProgress(
                                "빌드",
                                headerTitle,
                                $"{targetSummary} · {trimmed}",
                                CancelJob);
                        }
                    },
                    cancellationToken);

                if (buildResult.ExitCode != 0)
                {
                    _feedbackNotifier?.NotifyFailed(
                        $"{headerTitle} - 빌드 실패",
                        $"Gradle 빌드 실패 (종료 코드 {buildResult.ExitCode}). 로그 보기를 눌러 원인을 확인하세요.",
                        logBuilder.ToString(),
                        onRetry: () => _ = Task.Run(() => ExecuteDeploymentPipelineAsync(request, skipBuild: false, uninstallFirst: false, CancellationToken.None)));
                    return false;
                }
            }

            // 4. Locate Built APK & ApplicationId
            // NOTE(release-build): 추후 Release 빌드 지원 시 variant명을 "release"로 전달하여
            // build/outputs/apk/release/output-metadata.json 및 서명된 APK를 탐색하도록 확장합니다.
            if (!TryResolveBuiltApk(projectRoot, matchedModule, variant: "debug", out var apkPath, out var resolvedAppId))
            {
                AppendLog("[오류] 빌드된 APK 파일을 찾지 못했습니다.");
                _feedbackNotifier?.NotifyFailed(
                    $"{headerTitle} - APK 탐색 실패",
                    $"{matchedModule.ModuleName} 모듈의 Debug APK 파일을 찾지 못했습니다.",
                    logBuilder.ToString(),
                    onRetry: () => _ = Task.Run(() => ExecuteDeploymentPipelineAsync(request, skipBuild: false, uninstallFirst: false, CancellationToken.None)));
                return false;
            }

            var appId = !string.IsNullOrWhiteSpace(resolvedAppId)
                ? resolvedAppId
                : matchedModule.ApplicationId;
            AppendLog($"[APK 확인] {apkPath} (appId: {appId ?? "미확인"})");

            var adbPath = ResolveAdbExecutablePath();

            // 5. Optional Uninstall + Parallel ADB Streaming Install across all selected devices
            var installStageText = targetDevices.Count == 1
                ? $"{targetDevices[0].Model} ({RemoteInstallRules.GetDeviceKindKoreanLabel(targetDevices[0].DeviceKind)})에 APK 설치 중..."
                : $"{targetDevices.Count}대 기기({targetSummary})에 APK 동시 설치 중...";

            _feedbackNotifier?.NotifyProgress(
                uninstallFirst ? "삭제/설치" : "설치",
                headerTitle,
                installStageText,
                CancelJob);

            var installTasks = targetDevices.Select(async device =>
            {
                var devicePrefix = targetDevices.Count > 1 ? $"[{device.Model}] " : string.Empty;

                if (uninstallFirst
                    && !string.IsNullOrWhiteSpace(appId)
                    && (uninstallSerials is null || uninstallSerials.Contains(device.Serial)))
                {
                    AppendLog($"{devicePrefix}[기존 앱 삭제] {appId}");
                    var uninstallResult = await RunProcessCaptureAsync(
                        adbPath,
                        ["-s", device.Serial, "uninstall", appId],
                        workingDirectory: projectRoot,
                        onOutputLine: line => AppendLog($"{devicePrefix}{line}"),
                        cancellationToken);
                    AppendLog($"{devicePrefix}[기존 앱 삭제] ExitCode={uninstallResult.ExitCode}");
                }

                var installResult = await RunProcessCaptureAsync(
                    adbPath,
                    ["-s", device.Serial, "install", "-r", "-d", apkPath],
                    workingDirectory: projectRoot,
                    onOutputLine: line => AppendLog($"{devicePrefix}{line}"),
                    cancellationToken);

                var installCombined = $"{installResult.StandardOutput}\n{installResult.StandardError}".Trim();
                var installSucceeded = (installResult.ExitCode == 0
                        || installCombined.Contains("Success", StringComparison.OrdinalIgnoreCase))
                    && !installCombined.Contains("Failure", StringComparison.OrdinalIgnoreCase);

                if (installSucceeded)
                {
                    return (Device: device, Succeeded: true, FailureCode: (string?)null, IsIncompatible: false);
                }

                RemoteInstallRules.TryExtractInstallFailureCode(installCombined, out var failureCode, out var isIncompatible);
                return (Device: device, Succeeded: false, FailureCode: failureCode, IsIncompatible: isIncompatible);
            });

            var installOutcomes = await Task.WhenAll(installTasks);
            var succeededDevices = installOutcomes.Where(static o => o.Succeeded).Select(static o => o.Device).ToList();
            var failedOutcomes = installOutcomes.Where(static o => !o.Succeeded).ToList();

            // 6. Auto-Launch Installed App on all succeeded devices in parallel
            if (request.LaunchAfterInstall && !string.IsNullOrWhiteSpace(appId) && succeededDevices.Count > 0)
            {
                _feedbackNotifier?.NotifyProgress(
                    "실행",
                    headerTitle,
                    succeededDevices.Count == 1
                        ? $"{succeededDevices[0].Model}에서 앱 실행 중..."
                        : $"{succeededDevices.Count}대 기기에서 앱 동시 실행 중...",
                    CancelJob);

                var launchTasks = succeededDevices.Select(async device =>
                {
                    var deviceLog = new StringBuilder();
                    await LaunchInstalledAppAsync(adbPath, device.Serial, appId, deviceLog, cancellationToken);
                    var text = deviceLog.ToString().TrimEnd();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        AppendLog(text);
                    }
                });

                await Task.WhenAll(launchTasks);
            }

            if (failedOutcomes.Count > 0)
            {
                var firstFailure = failedOutcomes[0];
                var failureLabel = string.IsNullOrWhiteSpace(firstFailure.FailureCode)
                    ? "ADB 설치 실패"
                    : firstFailure.FailureCode!;
                var incompatibleSerials = failedOutcomes
                    .Where(static o => o.IsIncompatible)
                    .Select(static o => o.Device.Serial)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                Action? uninstallAndReinstallCallback = null;
                if (incompatibleSerials.Count > 0 && !string.IsNullOrWhiteSpace(appId))
                {
                    uninstallAndReinstallCallback = () => _ = Task.Run(
                        () => ExecuteDeploymentPipelineAsync(
                            request,
                            skipBuild: true,
                            uninstallFirst: true,
                            CancellationToken.None,
                            uninstallSerials: incompatibleSerials));
                }

                var failedSummary = string.Join(
                    ", ",
                    failedOutcomes.Select(static o => $"{o.Device.Model}({(string.IsNullOrWhiteSpace(o.FailureCode) ? "실패" : o.FailureCode)})"));
                var succeededPrefix = succeededDevices.Count > 0
                    ? $"성공: {string.Join(", ", succeededDevices.Select(static d => d.Model))} · "
                    : string.Empty;
                var detailMessage = incompatibleSerials.Count > 0
                    ? $"{succeededPrefix}실패: {failedSummary}. 기존 앱과 서명이 다른 기기는 [기존 앱 삭제 후 재설치]를 누르세요."
                    : $"{succeededPrefix}실패: {failedSummary}";

                _feedbackNotifier?.NotifyFailed(
                    $"{headerTitle} - {failureLabel}",
                    detailMessage,
                    logBuilder.ToString(),
                    onRetry: () => _ = Task.Run(() => ExecuteDeploymentPipelineAsync(request, skipBuild: true, uninstallFirst: false, CancellationToken.None)),
                    onUninstallAndReinstall: uninstallAndReinstallCallback);
                return false;
            }

            totalStopwatch.Stop();
            var completedTitle = succeededDevices.Count == 1
                ? $"✓ {succeededDevices[0].Model} ({RemoteInstallRules.GetDeviceKindKoreanLabel(succeededDevices[0].DeviceKind)}) 설치 완료"
                : $"✓ {succeededDevices.Count}대 기기 ({string.Join("·", succeededDevices.Select(static d => RemoteInstallRules.GetDeviceKindKoreanLabel(d.DeviceKind)).Distinct())}) 동시 설치 완료";

            var baseDetail = request.LaunchAfterInstall
                ? $"{matchedModule.ModuleName} 빌드·설치 및 앱 실행 완료"
                : $"{matchedModule.ModuleName} 빌드·설치 완료";
            var detailWithDevices = succeededDevices.Count > 1
                ? $"{string.Join(", ", succeededDevices.Select(static d => $"{d.Model}({RemoteInstallRules.GetDeviceKindKoreanLabel(d.DeviceKind)})"))} · {baseDetail}"
                : baseDetail;
            if (missingKinds.Count > 0)
            {
                var skippedLabel = string.Join(", ", missingKinds.Select(RemoteInstallRules.GetDeviceKindKoreanLabel));
                detailWithDevices = $"{detailWithDevices} ({skippedLabel} 미연결 건너뜀)";
            }

            _feedbackNotifier?.NotifyCompleted(
                completedTitle,
                detailWithDevices,
                totalStopwatch.Elapsed);

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger?.Log($"Remote install cancelled: {headerTitle}");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.Log($"Remote install failed: {headerTitle}", ex);
            logBuilder.AppendLine(ex.ToString());
            _feedbackNotifier?.NotifyFailed(
                $"{headerTitle} - 실행 오류",
                ex.Message,
                logBuilder.ToString(),
                onRetry: () => _ = Task.Run(() => ExecuteDeploymentPipelineAsync(request, skipBuild: false, uninstallFirst: false, CancellationToken.None)));
            return false;
        }
        finally
        {
            Interlocked.Decrement(ref _queuedJobsCount);
            if (acquiredLock)
            {
                _buildQueueLock.Release();
            }
        }
    }

    internal static RemoteInstallTargetResolution ResolveTargetDevicesForRequest(
        IReadOnlyList<AndroidConnectedDeviceInfo> allDevices,
        RemoteInstallDeviceKind targetDeviceKind,
        string? targetDeviceFilter,
        Func<RemoteInstallDeviceKind, IReadOnlyList<AndroidConnectedDeviceInfo>, AndroidConnectedDeviceInfo?>? selectDevicePrompt = null)
    {
        var selectedKinds = RemoteInstallRules.SplitSelectedDeviceKinds(targetDeviceKind);
        if (selectedKinds.Count == 0)
        {
            return new RemoteInstallTargetResolution([], [], WasCancelled: false);
        }

        var selectedDevices = new List<AndroidConnectedDeviceInfo>();
        var missingKinds = new List<RemoteInstallDeviceKind>();

        foreach (var kind in selectedKinds)
        {
            var candidates = allDevices
                .Where(d => d.DeviceKind == kind
                    && RemoteInstallRules.MatchesTargetFilter(d.Serial, d.Model, d.IpAddress, targetDeviceFilter))
                .ToList();

            if (candidates.Count == 0)
            {
                missingKinds.Add(kind);
                continue;
            }

            if (candidates.Count == 1)
            {
                selectedDevices.Add(candidates[0]);
                continue;
            }

            var picked = selectDevicePrompt is not null
                ? selectDevicePrompt(kind, candidates)
                : candidates[0];

            if (picked is null)
            {
                return new RemoteInstallTargetResolution([], missingKinds, WasCancelled: true);
            }

            selectedDevices.Add(picked);
        }

        return new RemoteInstallTargetResolution(selectedDevices, missingKinds, WasCancelled: false);
    }

    internal sealed record RemoteInstallTargetResolution(
        IReadOnlyList<AndroidConnectedDeviceInfo> SelectedDevices,
        IReadOnlyList<RemoteInstallDeviceKind> MissingKinds,
        bool WasCancelled);

    private async Task<AndroidConnectedDeviceInfo?> ProbeDeviceAsync(
        string adbPath,
        string serial,
        CancellationToken cancellationToken)
    {
        try
        {
            var probeScript =
                $"getprop ro.product.model; echo {ProbeSeparator}; " +
                $"getprop ro.product.manufacturer; echo {ProbeSeparator}; " +
                $"getprop ro.build.characteristics; echo {ProbeSeparator}; " +
                $"wm size; echo {ProbeSeparator}; " +
                $"wm density; echo {ProbeSeparator}; " +
                "pm list features";

            var probeResult = await RunProcessCaptureAsync(
                adbPath,
                ["-s", serial, "shell", probeScript],
                workingDirectory: null,
                onOutputLine: null,
                cancellationToken);

            var sections = probeResult.StandardOutput.Split(ProbeSeparator, StringSplitOptions.None);
            var model = sections.Length > 0 ? FirstNonEmptyLine(sections[0]) : string.Empty;
            var manufacturer = sections.Length > 1 ? FirstNonEmptyLine(sections[1]) : string.Empty;
            var characteristics = sections.Length > 2 ? FirstNonEmptyLine(sections[2]) : string.Empty;
            var wmSize = sections.Length > 3 ? sections[3].Trim() : string.Empty;
            var wmDensity = sections.Length > 4 ? sections[4].Trim() : string.Empty;
            var features = sections.Length > 5 ? sections[5] : string.Empty;

            if (string.IsNullOrWhiteSpace(model))
            {
                model = serial;
            }

            var classification = RemoteInstallRules.ClassifyDevice(
                characteristics,
                features,
                wmSize,
                wmDensity,
                model,
                manufacturer);

            return new AndroidConnectedDeviceInfo(
                serial,
                model,
                manufacturer,
                classification.DeviceKind,
                classification.SmallestWidthDp,
                classification.ClassificationSource,
                RemoteInstallRules.ExtractIpFromSerial(serial));
        }
        catch (Exception ex)
        {
            _logger?.Log($"Failed to probe Android device {serial}.", ex);
            return new AndroidConnectedDeviceInfo(
                serial,
                serial,
                string.Empty,
                RemoteInstallDeviceKind.Phone,
                SmallestWidthDp: null,
                "기본 분류(폰)",
                RemoteInstallRules.ExtractIpFromSerial(serial));
        }
    }

    private async Task LaunchInstalledAppAsync(
        string adbPath,
        string serial,
        string appId,
        StringBuilder logBuilder,
        CancellationToken cancellationToken)
    {
        var resolveResult = await RunProcessCaptureAsync(
            adbPath,
            ["-s", serial, "shell", "cmd", "package", "resolve-activity", "--brief", appId],
            workingDirectory: null,
            onOutputLine: null,
            cancellationToken);

        string? component = null;
        foreach (var rawLine in resolveResult.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = rawLine.Trim();
            if (ComponentNameRegex().IsMatch(trimmed))
            {
                component = trimmed;
                break;
            }

            if (trimmed.Contains('/', StringComparison.Ordinal)
                && !trimmed.Contains("priority=", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Contains("No activity found", StringComparison.OrdinalIgnoreCase))
            {
                component = trimmed;
            }
        }

        if (!string.IsNullOrWhiteSpace(component))
        {
            logBuilder.AppendLine($"[앱 실행] am start -n {component}");
            await RunProcessCaptureAsync(
                adbPath,
                ["-s", serial, "shell", "am", "start", "-n", component],
                workingDirectory: null,
                onOutputLine: null,
                cancellationToken);
            return;
        }

        logBuilder.AppendLine($"[앱 실행 Fallback] monkey -p {appId}");
        await RunProcessCaptureAsync(
            adbPath,
            ["-s", serial, "shell", "monkey", "-p", appId, "-c", "android.intent.category.LAUNCHER", "1"],
            workingDirectory: null,
            onOutputLine: null,
            cancellationToken);
    }

    private static bool TryResolveBuiltApk(
        string projectRoot,
        AndroidAppModuleInfo moduleInfo,
        string variant,
        out string apkPath,
        out string? applicationId)
    {
        apkPath = string.Empty;
        applicationId = moduleInfo.ApplicationId;

        var moduleDir = Path.Combine(projectRoot, moduleInfo.RelativeDirectory);
        var variantLower = variant.ToLowerInvariant();
        var metadataPath = Path.Combine(moduleDir, "build", "outputs", "apk", variantLower, "output-metadata.json");

        if (File.Exists(metadataPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(metadataPath, Encoding.UTF8));
                var root = doc.RootElement;
                if (root.TryGetProperty("applicationId", out var appIdProp)
                    && appIdProp.ValueKind == JsonValueKind.String)
                {
                    applicationId = appIdProp.GetString();
                }

                if (root.TryGetProperty("elements", out var elementsProp)
                    && elementsProp.ValueKind == JsonValueKind.Array
                    && elementsProp.GetArrayLength() > 0)
                {
                    var firstElement = elementsProp[0];
                    if (firstElement.TryGetProperty("outputFile", out var outputFileProp)
                        && outputFileProp.ValueKind == JsonValueKind.String
                        && outputFileProp.GetString() is { Length: > 0 } outputFileName)
                    {
                        var candidatePath = Path.Combine(Path.GetDirectoryName(metadataPath)!, outputFileName);
                        if (File.Exists(candidatePath))
                        {
                            apkPath = candidatePath;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Fall back to directory search below
            }
        }

        var apkRootDir = Path.Combine(moduleDir, "build", "outputs", "apk");
        if (Directory.Exists(apkRootDir))
        {
            var candidateApk = Directory.EnumerateFiles(apkRootDir, "*.apk", SearchOption.AllDirectories)
                .Where(path => !path.Contains("androidTest", StringComparison.OrdinalIgnoreCase)
                    && path.Contains(variantLower, StringComparison.OrdinalIgnoreCase))
                .Select(static path => new FileInfo(path))
                .OrderByDescending(static info => info.LastWriteTimeUtc)
                .FirstOrDefault();

            if (candidateApk is not null)
            {
                apkPath = candidateApk.FullName;
                return true;
            }
        }

        return false;
    }

    private static bool TryInspectAndroidAppModule(
        string normalizedModule,
        string relativeDir,
        string moduleDir,
        out AndroidAppModuleInfo moduleInfo)
    {
        moduleInfo = default!;
        var buildFile = FindFirstExistingFile(
            Path.Combine(moduleDir, "build.gradle.kts"),
            Path.Combine(moduleDir, "build.gradle"));

        if (buildFile is null)
        {
            return false;
        }

        var content = File.ReadAllText(buildFile);
        var hasAppPlugin = content.Contains("com.android.application", StringComparison.Ordinal)
            || content.Contains("android.application", StringComparison.Ordinal);
        if (!hasAppPlugin || ApplyFalseRegex().IsMatch(content))
        {
            return false;
        }

        string? appId = null;
        var appIdMatch = ApplicationIdRegex().Match(content);
        if (appIdMatch.Success)
        {
            appId = appIdMatch.Groups[1].Value;
        }
        else
        {
            var namespaceMatch = NamespaceRegex().Match(content);
            if (namespaceMatch.Success)
            {
                appId = namespaceMatch.Groups[1].Value;
            }
        }

        moduleInfo = new AndroidAppModuleInfo(normalizedModule, relativeDir, appId);
        return true;
    }

    private static string ResolveAdbExecutablePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var defaultSdkAdb = Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");
            if (File.Exists(defaultSdkAdb))
            {
                return defaultSdkAdb;
            }
        }

        foreach (var envVar in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var sdkRoot = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(sdkRoot))
            {
                var candidate = Path.Combine(sdkRoot, "platform-tools", "adb.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return "adb.exe";
    }

    private static string ResolveGradleWrapperPath(string projectRoot)
    {
        var batPath = Path.Combine(projectRoot, "gradlew.bat");
        if (File.Exists(batPath))
        {
            return batPath;
        }

        var shPath = Path.Combine(projectRoot, "gradlew");
        return File.Exists(shPath) ? shPath : "gradle";
    }

    private static async Task<ProcessCaptureResult> RunProcessCaptureAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stdOut)
            {
                stdOut.AppendLine(e.Data);
            }
            onOutputLine?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stdErr)
            {
                stdErr.AppendLine(e.Data);
            }
            onOutputLine?.Invoke(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill race
            }
        });

        await process.WaitForExitAsync(cancellationToken);
        return new ProcessCaptureResult(process.ExitCode, stdOut.ToString(), stdErr.ToString());
    }

    private static string? FindFirstExistingFile(params string[] paths) =>
        paths.FirstOrDefault(File.Exists);

    private static string FirstNonEmptyLine(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static l => l.Trim())
            .FirstOrDefault(static l => l.Length > 0) ?? string.Empty;

    private sealed record ProcessCaptureResult(int ExitCode, string StandardOutput, string StandardError);

    [GeneratedRegex(@"['""](:[^'""]+)['""]")]
    private static partial Regex ModuleIncludeRegex();

    [GeneratedRegex(@"apply\s+false", RegexOptions.IgnoreCase)]
    private static partial Regex ApplyFalseRegex();

    [GeneratedRegex(@"applicationId\s*=?\s*[""']([^""']+)[""']")]
    private static partial Regex ApplicationIdRegex();

    [GeneratedRegex(@"namespace\s*=?\s*[""']([^""']+)[""']")]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(@"^\s*([^\s]+)\s+device(?:\s+|$)")]
    private static partial Regex AdbDeviceLineRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_\.]+\/[a-zA-Z0-9_\.\$]+$")]
    private static partial Regex ComponentNameRegex();
}
