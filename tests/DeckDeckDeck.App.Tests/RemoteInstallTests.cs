// 역할: Android 원격설치의 4단계 기기 분류(태블릿 4 · 폰 5 · 워치 6), 모듈 스캔·매칭, 저장 검증 및 실행 유스케이스를 검증하는 단위 테스트 모음입니다.
using System.IO;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class RemoteInstallTests
{
    [Fact]
    public void GetDefaultDeviceKindForSlot_FollowsTablet4_Phone5_Watch6_Order()
    {
        Assert.Equal(RemoteInstallDeviceKind.Tablet, RemoteInstallRules.GetDefaultDeviceKindForSlot(SlotKey.Numpad4));
        Assert.Equal(RemoteInstallDeviceKind.Phone, RemoteInstallRules.GetDefaultDeviceKindForSlot(SlotKey.Numpad5));
        Assert.Equal(RemoteInstallDeviceKind.Watch, RemoteInstallRules.GetDefaultDeviceKindForSlot(SlotKey.Numpad6));
    }

    [Fact]
    public void ClassifyDevice_DetectsWatchFromCharacteristicsOrFeature()
    {
        var byChar = RemoteInstallRules.ClassifyDevice(
            characteristics: "nosdcard,watch",
            featureList: string.Empty,
            wmSizeOutput: "Physical size: 450x450",
            wmDensityOutput: "Physical density: 320",
            model: "SM-R940");

        Assert.Equal(RemoteInstallDeviceKind.Watch, byChar.DeviceKind);
        Assert.Equal(225, byChar.SmallestWidthDp);

        var byFeature = RemoteInstallRules.ClassifyDevice(
            characteristics: "default",
            featureList: "feature:android.hardware.type.watch",
            wmSizeOutput: null,
            wmDensityOutput: null,
            model: "Pixel Watch");

        Assert.Equal(RemoteInstallDeviceKind.Watch, byFeature.DeviceKind);
    }

    [Fact]
    public void ClassifyDevice_DetectsTabletBySmallestWidthDpEvenWithoutTabletCharacteristic()
    {
        // Many Android tablets report "nosdcard" or "default" in ro.build.characteristics,
        // but have sw >= 600dp (e.g., 1600x2560 at 320dpi -> sw = 800dp).
        var result = RemoteInstallRules.ClassifyDevice(
            characteristics: "nosdcard",
            featureList: string.Empty,
            wmSizeOutput: "Physical size: 1600x2560",
            wmDensityOutput: "Physical density: 320",
            model: "UnknownDevice");

        Assert.Equal(RemoteInstallDeviceKind.Tablet, result.DeviceKind);
        Assert.Equal(800, result.SmallestWidthDp);
    }

    [Fact]
    public void ClassifyDevice_DetectsPhoneWhenSmallestWidthBelow600Dp()
    {
        // 1080x2340 at 450dpi -> sw = 384dp -> Phone
        var result = RemoteInstallRules.ClassifyDevice(
            characteristics: "nosdcard",
            featureList: string.Empty,
            wmSizeOutput: "Physical size: 1080x2340",
            wmDensityOutput: "Physical density: 450",
            model: "SM-S921N");

        Assert.Equal(RemoteInstallDeviceKind.Phone, result.DeviceKind);
        Assert.Equal(384, result.SmallestWidthDp);
    }

    [Fact]
    public void ClassifyDevice_UsesModelCodeFallbackWhenScreenMetricsUnavailable()
    {
        var tablet = RemoteInstallRules.ClassifyDevice(
            characteristics: "default",
            featureList: null,
            wmSizeOutput: null,
            wmDensityOutput: null,
            model: "SM-X710");

        var watch = RemoteInstallRules.ClassifyDevice(
            characteristics: "default",
            featureList: null,
            wmSizeOutput: null,
            wmDensityOutput: null,
            model: "SM-R960");

        Assert.Equal(RemoteInstallDeviceKind.Tablet, tablet.DeviceKind);
        Assert.Equal(RemoteInstallDeviceKind.Watch, watch.DeviceKind);
    }

    [Fact]
    public void RecommendModuleForDeviceKind_MatchesWatchAndPhoneModules()
    {
        string[] modules = [":phone:android", ":wear:android"];

        Assert.Equal(":phone:android", RemoteInstallRules.RecommendModuleForDeviceKind(modules, RemoteInstallDeviceKind.Tablet));
        Assert.Equal(":phone:android", RemoteInstallRules.RecommendModuleForDeviceKind(modules, RemoteInstallDeviceKind.Phone));
        Assert.Equal(":wear:android", RemoteInstallRules.RecommendModuleForDeviceKind(modules, RemoteInstallDeviceKind.Watch));
    }

    [Fact]
    public void BuildAssembleTaskName_FormatsDebugTaskCleanly()
    {
        Assert.Equal(":phone:android:assembleDebug", RemoteInstallRules.BuildAssembleTaskName("phone/android"));
        Assert.Equal(":wear:android:assembleDebug", RemoteInstallRules.BuildAssembleTaskName(":wear:android"));
    }

    [Fact]
    public void TryExtractInstallFailureCode_DetectsUpdateIncompatible()
    {
        var found = RemoteInstallRules.TryExtractInstallFailureCode(
            "adb: failed to install app.apk: Failure [INSTALL_FAILED_UPDATE_INCOMPATIBLE: Package signatures do not match]",
            out var code,
            out var isUpdateIncompatible);

        Assert.True(found);
        Assert.Equal("INSTALL_FAILED_UPDATE_INCOMPATIBLE", code);
        Assert.True(isUpdateIncompatible);
    }

    [Fact]
    public void ValidateForSave_And_Normalize_HandleRemoteInstallFields()
    {
        var missingPath = SnippetRules.ValidateForSave(
            title: "워치 원격설치",
            content: string.Empty,
            actionType: SnippetActionType.RemoteInstall,
            launchPath: null,
            launchUrl: null,
            selectedMediaProvider: SnippetMediaProvider.System,
            selectedMediaCommand: SnippetMediaCommand.PlayPause,
            remoteInstallProjectPath: "",
            remoteInstallModule: ":wear:android",
            remoteInstallDeviceKind: RemoteInstallDeviceKind.Watch);

        Assert.False(missingPath.Succeeded);
        Assert.Equal(RemoteInstallRules.ProjectPathRequiredMessage, missingPath.ErrorMessage);

        var valid = SnippetRules.ValidateForSave(
            title: "워치 원격설치",
            content: string.Empty,
            actionType: SnippetActionType.RemoteInstall,
            launchPath: null,
            launchUrl: null,
            selectedMediaProvider: SnippetMediaProvider.System,
            selectedMediaCommand: SnippetMediaCommand.PlayPause,
            remoteInstallProjectPath: @" C:\Proj\WatchYouDoin ",
            remoteInstallModule: "wear:android",
            remoteInstallDeviceKind: RemoteInstallDeviceKind.Watch,
            remoteInstallTargetDevice: " 192.168.137.204 ",
            remoteInstallLaunchAfterInstall: true);

        Assert.True(valid.Succeeded);
        Assert.Equal(@"C:\Proj\WatchYouDoin", valid.RemoteInstallProjectPath);
        Assert.Equal(":wear:android", valid.RemoteInstallModule);
        Assert.Equal(RemoteInstallDeviceKind.Watch, valid.RemoteInstallDeviceKind);
        Assert.Equal("192.168.137.204", valid.RemoteInstallTargetDevice);

        var normalized = ExecutableActionStorageRules.Normalize(
            title: "워치 원격설치",
            content: "ignored",
            description: null,
            imagePath: null,
            actionType: SnippetActionType.RemoteInstall,
            launchPath: "ignored",
            slotImageMode: SlotImageMode.Auto,
            autoIcon: null,
            launchUrl: null,
            mediaProvider: null,
            mediaCommand: null,
            pasteShortcutMode: PasteShortcutMode.CtrlV,
            terminalCommand: "ignored",
            terminalShell: SnippetTerminalShell.PowerShell,
            runAsAdministrator: false,
            fileActionMode: FileActionMode.Launch,
            remoteInstallProjectPath: valid.RemoteInstallProjectPath,
            remoteInstallModule: valid.RemoteInstallModule,
            remoteInstallDeviceKind: valid.RemoteInstallDeviceKind,
            remoteInstallTargetDevice: valid.RemoteInstallTargetDevice,
            remoteInstallLaunchAfterInstall: true);

        Assert.Equal(SnippetActionType.RemoteInstall, normalized.ActionType);
        Assert.Equal(":wear:android", normalized.RemoteInstallModule);
        Assert.Null(normalized.TerminalCommand);
        Assert.Null(normalized.LaunchPath);
    }

    [Fact]
    public void ScanProjectModules_DetectsMultiModuleAndroidApps()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DeckRemoteInstallTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(
                Path.Combine(tempRoot, "settings.gradle.kts"),
                """
                include(":shared")
                include(":phone:android")
                include(":wear:android")
                """);

            var sharedDir = Path.Combine(tempRoot, "shared");
            Directory.CreateDirectory(sharedDir);
            File.WriteAllText(Path.Combine(sharedDir, "build.gradle.kts"), """plugins { id("com.android.library") }""");

            var phoneDir = Path.Combine(tempRoot, "phone", "android");
            Directory.CreateDirectory(phoneDir);
            File.WriteAllText(
                Path.Combine(phoneDir, "build.gradle.kts"),
                """
                plugins { id("com.android.application") }
                android { applicationId = "com.example.watchyoudoin" }
                """);

            var wearDir = Path.Combine(tempRoot, "wear", "android");
            Directory.CreateDirectory(wearDir);
            File.WriteAllText(
                Path.Combine(wearDir, "build.gradle.kts"),
                """
                plugins { id("com.android.application") }
                android { applicationId = "com.example.watchyoudoin" }
                """);

            var gateway = new AndroidRemoteInstallGateway(logger: null, feedbackNotifier: null, waitForCompletion: true);
            var modules = gateway.ScanProjectModules(tempRoot);

            Assert.Equal(2, modules.Count);
            Assert.Contains(modules, m => m.ModuleName == ":phone:android" && m.ApplicationId == "com.example.watchyoudoin");
            Assert.Contains(modules, m => m.ModuleName == ":wear:android" && m.ApplicationId == "com.example.watchyoudoin");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCase_InvokesAndroidRemoteInstallGateway()
    {
        var fakeGateway = new FakeAndroidRemoteInstallGateway();
        var useCase = new ExecuteSnippetActionUseCase(
            new NoopClipboardPasteGateway(),
            new NoopFileLaunchGateway(),
            new NoopUrlLaunchGateway(),
            new NoopMediaActionGateway(),
            new NoopSpotifyMediaActionGateway(),
            new NoopTerminalCommandGateway(),
            new NoopFilePasteGateway(),
            new NoopDialogAdapter(),
            androidRemoteInstallGateway: fakeGateway);

        var snippet = new Snippet
        {
            Title = "워치 원격설치",
            SlotKey = SlotKey.Numpad6,
            ActionType = SnippetActionType.RemoteInstall,
            RemoteInstallProjectPath = @"C:\Users\joajo\AndroidStudioProjects\WatchYouDoin",
            RemoteInstallModule = ":wear:android",
            RemoteInstallDeviceKind = RemoteInstallDeviceKind.Watch,
            RemoteInstallLaunchAfterInstall = true
        };

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            snippet,
            new AppSettings { AutoHideAfterPaste = true },
            IntPtr.Zero));

        Assert.True(result.Succeeded);
        Assert.True(result.ShouldHideWindow);
        Assert.NotNull(fakeGateway.LastRequest);
        Assert.Equal(":wear:android", fakeGateway.LastRequest!.ModuleName);
        Assert.Equal(RemoteInstallDeviceKind.Watch, fakeGateway.LastRequest.TargetDeviceKind);
    }

    private sealed class FakeAndroidRemoteInstallGateway : IAndroidRemoteInstallGateway
    {
        public AndroidRemoteInstallRequest? LastRequest { get; private set; }

        public IReadOnlyList<AndroidAppModuleInfo> ScanProjectModules(string? projectPath) => [];

        public Task<IReadOnlyList<AndroidConnectedDeviceInfo>> ScanConnectedDevicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AndroidConnectedDeviceInfo>>([]);

        public Task<bool> TryStartRemoteInstallAsync(AndroidRemoteInstallRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(true);
        }
    }

    private sealed class NoopClipboardPasteGateway : IClipboardPasteGateway
    {
        public Task<bool> PasteActionAsync(ExecutableAction action, IntPtr targetWindowHandle, AppSettings settings) =>
            Task.FromResult(true);
    }

    private sealed class NoopFileLaunchGateway : IFileLaunchGateway
    {
        public bool TryLaunch(string path) => true;
    }

    private sealed class NoopUrlLaunchGateway : IUrlLaunchGateway
    {
        public bool TryLaunch(string url) => true;
    }

    private sealed class NoopMediaActionGateway : IMediaActionGateway
    {
        public bool TryExecute(SnippetMediaCommand command) => true;
    }

    private sealed class NoopSpotifyMediaActionGateway : ISpotifyMediaActionGateway
    {
        public Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(SnippetMediaCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpotifyMediaActionGatewayResult(true));
    }

    private sealed class NoopTerminalCommandGateway : ITerminalCommandGateway
    {
        public bool TryExecute(string command, SnippetTerminalShell shell, bool runAsAdministrator, bool openTerminalWindow = false, string? workingDirectory = null) =>
            true;
    }

    private sealed class NoopFilePasteGateway : IFilePasteGateway
    {
        public Task<FilePasteGatewayResult> PasteFileAsync(string filePath, IntPtr targetWindowHandle, AppSettings settings) =>
            Task.FromResult(FilePasteGatewayResult.Success());
    }

    private sealed class NoopDialogAdapter : IDialogAdapter
    {
        public bool Confirm(string title, string message) => true;
        public void ShowInformation(string title, string message) { }
        public string? SelectImageFile() => null;
        public string? SelectLaunchFile() => null;
        public string? SelectPasteFile() => null;
        public string? SelectLaunchFolder() => null;
        public string? SelectBackupFolder() => null;
        public string? SelectBackupZipFile() => null;
        public bool TryPromptTextInputs(string title, string message, IReadOnlyList<string> fieldNames, out IReadOnlyDictionary<string, string> values)
        {
            values = new Dictionary<string, string>();
            return false;
        }
        public bool TryPromptAdbPort(string title, string fixedIp, out string port)
        {
            port = string.Empty;
            return false;
        }
    }
}
