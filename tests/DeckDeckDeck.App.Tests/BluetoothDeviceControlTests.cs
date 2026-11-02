// 역할: 블루투스 오디오 기기 고속 연결/해제 명령 파싱, 서비스 상태 제어, 비동기 유스케이스 및 스낵바 피드백 알림을 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class BluetoothDeviceControlTests
{
    [Fact]
    public void TryParseBluetoothAudioCommand_ParsesStandardAndLegacyBtcomScripts()
    {
        // 1. Standard commands
        Assert.True(TerminalCommandParameterRules.TryParseBluetoothAudioCommand("bt-audio connect E4:92:82:82:D5:9B", out var cmd1));
        Assert.Equal(BluetoothAudioActionKind.Connect, cmd1.Action);
        Assert.Equal("E4:92:82:82:D5:9B", cmd1.Target);

        Assert.True(TerminalCommandParameterRules.TryParseBluetoothAudioCommand("bt-audio disconnect ac-80-0a-20-cb-af", out var cmd2));
        Assert.Equal(BluetoothAudioActionKind.Disconnect, cmd2.Action);
        Assert.Equal("AC:80:0A:20:CB:AF", cmd2.Target);

        Assert.True(TerminalCommandParameterRules.TryParseBluetoothAudioCommand("bt-audio on \"Sony WH-1000XM4\"", out var cmd3));
        Assert.Equal(BluetoothAudioActionKind.Connect, cmd3.Action);
        Assert.Equal("Sony WH-1000XM4", cmd3.Target);

        Assert.True(TerminalCommandParameterRules.TryParseBluetoothAudioCommand("bt-audio off E4928282D59B", out var cmd4));
        Assert.Equal(BluetoothAudioActionKind.Disconnect, cmd4.Action);
        Assert.Equal("E4:92:82:82:D5:9B", cmd4.Target);

        // 2. Legacy btcom Buds on script
        const string budsOnScript = """
            @echo off
            set BTADDR=E4:92:82:82:D5:9B
            btcom -b "%BTADDR%" -r -s110b
            btcom -b "%BTADDR%" -c -s110b
            """;
        Assert.True(TerminalCommandParameterRules.TryParseBluetoothAudioCommand(budsOnScript, out var budsOnCmd));
        Assert.Equal(BluetoothAudioActionKind.Connect, budsOnCmd.Action);
        Assert.Equal("E4:92:82:82:D5:9B", budsOnCmd.Target);

        // 3. Legacy btcom Buds off script
        const string budsOffScript = """
            @echo off
            set BTADDR=E4:92:82:82:D5:9B
            btcom -b "%BTADDR%" -r -s110b
            btcom -b "%BTADDR%" -r -s111e
            """;
        Assert.True(TerminalCommandParameterRules.TryParseBluetoothAudioCommand(budsOffScript, out var budsOffCmd));
        Assert.Equal(BluetoothAudioActionKind.Disconnect, budsOffCmd.Action);
        Assert.Equal("E4:92:82:82:D5:9B", budsOffCmd.Target);
    }

    [Fact]
    public void BuildBluetoothAudioCommand_ProducesExpectedCommand()
    {
        var connect = TerminalCommandParameterRules.BuildBluetoothAudioCommand(
            BluetoothAudioActionKind.Connect,
            "e4:92:82:82:d5:9b");
        Assert.Equal("bt-audio connect E4:92:82:82:D5:9B", connect);

        var disconnect = TerminalCommandParameterRules.BuildBluetoothAudioCommand(
            BluetoothAudioActionKind.Disconnect,
            "Galaxy Buds Pro");
        Assert.Equal("bt-audio disconnect Galaxy Buds Pro", disconnect);

        var emptyTarget = TerminalCommandParameterRules.BuildBluetoothAudioCommand(
            BluetoothAudioActionKind.Connect,
            null);
        Assert.Equal("bt-audio connect", emptyTarget);
    }

    [Fact]
    public void ExtractParameterNames_ReturnsEmptyForBluetoothAudioCommand()
    {
        var params1 = TerminalCommandParameterRules.ExtractParameterNames("bt-audio connect {{Device}}");
        Assert.Empty(params1);

        const string btcomScript = """
            set BTADDR=E4:92:82:82:D5:9B
            btcom -b "%BTADDR%" -c -s110b
            """;
        var params2 = TerminalCommandParameterRules.ExtractParameterNames(btcomScript);
        Assert.Empty(params2);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_Connect_SkipsDisableWhenServiceNotInstalled()
    {
        const ulong testAddress = 0xE4928282D59B;
        var serviceStateCalls = new List<(ulong Address, Guid ServiceGuid, bool Enable)>();

        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid>(), // Not installed
            setServiceState: (addr, guid, enable) =>
            {
                serviceStateCalls.Add((addr, guid, enable));
                return 0; // Success
            },
            findDeviceAddressByName: _ => testAddress);

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Connect, "E4:92:82:82:D5:9B");
        var result = await gateway.TryControlAsync(command);

        Assert.True(result);
        Assert.DoesNotContain(serviceStateCalls, call => !call.Enable && call.ServiceGuid == WindowsBluetoothDeviceControlGateway.AudioSinkServiceClassGuid);
        Assert.Contains(serviceStateCalls, call => call.Enable && call.ServiceGuid == WindowsBluetoothDeviceControlGateway.AudioSinkServiceClassGuid);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_Connect_TogglesDisableThenEnableWhenServiceAlreadyInstalled()
    {
        const ulong testAddress = 0xE4928282D59B;
        var serviceStateCalls = new List<(ulong Address, Guid ServiceGuid, bool Enable)>();

        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid> { WindowsBluetoothDeviceControlGateway.AudioSinkServiceClassGuid },
            setServiceState: (addr, guid, enable) =>
            {
                serviceStateCalls.Add((addr, guid, enable));
                return 0;
            },
            findDeviceAddressByName: _ => testAddress);

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Connect, "E4:92:82:82:D5:9B");
        var result = await gateway.TryControlAsync(command);

        Assert.True(result);
        Assert.True(serviceStateCalls.Count >= 2);
        Assert.Equal((testAddress, WindowsBluetoothDeviceControlGateway.AudioSinkServiceClassGuid, false), serviceStateCalls[0]);
        Assert.Equal((testAddress, WindowsBluetoothDeviceControlGateway.AudioSinkServiceClassGuid, true), serviceStateCalls[1]);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_Disconnect_DisablesAudioSinkAndHandsfree()
    {
        const ulong testAddress = 0xE4928282D59B;
        var serviceStateCalls = new List<(ulong Address, Guid ServiceGuid, bool Enable)>();

        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid>
            {
                WindowsBluetoothDeviceControlGateway.AudioSinkServiceClassGuid,
                WindowsBluetoothDeviceControlGateway.HandsfreeServiceClassGuid
            },
            setServiceState: (addr, guid, enable) =>
            {
                serviceStateCalls.Add((addr, guid, enable));
                return 0;
            },
            findDeviceAddressByName: _ => testAddress);

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Disconnect, "E4:92:82:82:D5:9B");
        var result = await gateway.TryControlAsync(command);

        Assert.True(result);
        Assert.Equal(2, serviceStateCalls.Count);
        Assert.Equal((testAddress, WindowsBluetoothDeviceControlGateway.AudioSinkServiceClassGuid, false), serviceStateCalls[0]);
        Assert.Equal((testAddress, WindowsBluetoothDeviceControlGateway.HandsfreeServiceClassGuid, false), serviceStateCalls[1]);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_ProductionMode_ReturnsImmediatelyWithoutBlockingCaller()
    {
        const ulong testAddress = 0xE4928282D59B;
        var setServiceStateStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSetServiceStateToFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serviceStateCalled = false;

        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid>(),
            setServiceState: (addr, guid, enable) =>
            {
                serviceStateCalled = true;
                setServiceStateStarted.TrySetResult(true);
                allowSetServiceStateToFinish.Task.GetAwaiter().GetResult();
                return 0;
            },
            findDeviceAddressByName: _ => testAddress,
            waitForCompletion: false);

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Connect, "E4:92:82:82:D5:9B");
        var controlTask = gateway.TryControlAsync(command);

        var completedTask = await Task.WhenAny(controlTask, Task.Delay(500));
        Assert.Same(controlTask, completedTask);
        Assert.True(await controlTask);

        var startedTask = await Task.WhenAny(setServiceStateStarted.Task, Task.Delay(2000));
        Assert.Same(setServiceStateStarted.Task, startedTask);
        Assert.True(serviceStateCalled);

        allowSetServiceStateToFinish.TrySetResult(true);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_EmptyTarget_ReturnsFalseImmediately()
    {
        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid>(),
            setServiceState: (_, _, _) => 0,
            findDeviceAddressByName: _ => null,
            waitForCompletion: false);

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Connect, "");
        var result = await gateway.TryControlAsync(command);

        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCase_BluetoothCommand_UsesBluetoothGatewayWithoutSpawningTerminal()
    {
        var terminalCalled = false;
        var bluetoothCalled = false;
        BluetoothAudioCommand? capturedCommand = null;

        var terminalGateway = new FakeTerminalCommandGateway(() => terminalCalled = true);
        var bluetoothGateway = new FakeBluetoothDeviceControlGateway(cmd =>
        {
            bluetoothCalled = true;
            capturedCommand = cmd;
            return Task.FromResult(true);
        });

        var useCase = new ExecuteSnippetActionUseCase(
            new FakeClipboardPasteGateway(),
            new FakeFileLaunchGateway(),
            new FakeUrlLaunchGateway(),
            new FakeMediaActionGateway(),
            new FakeSpotifyMediaActionGateway(),
            terminalGateway,
            new FakeFilePasteGateway(),
            new FakeDialogAdapter(),
            bluetoothDeviceControlGateway: bluetoothGateway);

        var action = new ExecutableAction(
            Id: Guid.NewGuid(),
            Title: "Buds 연결",
            Content: string.Empty,
            ActionType: SnippetActionType.TerminalCommand,
            PasteShortcutMode: PasteShortcutMode.CtrlV,
            LaunchPath: null,
            FileActionMode: FileActionMode.Launch,
            LaunchUrl: null,
            MediaProvider: null,
            MediaCommand: null,
            TerminalCommand: "bt-audio connect E4:92:82:82:D5:9B",
            TerminalShell: SnippetTerminalShell.Cmd,
            RunAsAdministrator: false,
            OpenTerminalWindow: false);

        var settings = new AppSettings { AutoHideAfterPaste = true };
        var request = new ExecuteSnippetActionRequest(action, settings, IntPtr.Zero);
        var result = await useCase.ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.True(result.ShouldHideWindow);
        Assert.False(terminalCalled);
        Assert.True(bluetoothCalled);
        Assert.NotNull(capturedCommand);
        Assert.Equal(BluetoothAudioActionKind.Connect, capturedCommand!.Action);
        Assert.Equal("E4:92:82:82:D5:9B", capturedCommand!.Target);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_NotifiesFeedbackSnackbarOnStartAndCompletion()
    {
        const ulong testAddress = 0xE4928282D59B;
        var fakeNotifier = new FakeBluetoothFeedbackNotifier();

        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid>(),
            setServiceState: (_, _, _) => 0,
            findDeviceAddressByName: _ => testAddress,
            waitForCompletion: true,
            feedbackNotifier: fakeNotifier,
            resolveDeviceDisplayName: _ => "Buds3 Pro");

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Connect, "E4:92:82:82:D5:9B");
        var result = await gateway.TryControlAsync(command);

        Assert.True(result);
        Assert.Single(fakeNotifier.StartingCalls);
        Assert.Equal(BluetoothAudioActionKind.Connect, fakeNotifier.StartingCalls[0].Action);
        Assert.Equal("Buds3 Pro", fakeNotifier.StartingCalls[0].DisplayName);

        Assert.Single(fakeNotifier.CompletedCalls);
        Assert.Equal(BluetoothAudioActionKind.Connect, fakeNotifier.CompletedCalls[0].Action);
        Assert.Equal("Buds3 Pro", fakeNotifier.CompletedCalls[0].DisplayName);
        Assert.True(fakeNotifier.CompletedCalls[0].Succeeded);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_NotifiesFeedbackSnackbarOnFailure()
    {
        const ulong testAddress = 0xE4928282D59B;
        var fakeNotifier = new FakeBluetoothFeedbackNotifier();

        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid>(),
            setServiceState: (_, _, _) => 1167, // Error
            findDeviceAddressByName: _ => testAddress,
            waitForCompletion: true,
            feedbackNotifier: fakeNotifier,
            resolveDeviceDisplayName: _ => "Buds3 Pro");

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Connect, "E4:92:82:82:D5:9B");
        var result = await gateway.TryControlAsync(command);

        Assert.False(result);
        Assert.Single(fakeNotifier.StartingCalls);
        Assert.Single(fakeNotifier.CompletedCalls);
        Assert.False(fakeNotifier.CompletedCalls[0].Succeeded);
    }

    [Fact]
    public async Task WindowsBluetoothDeviceControlGateway_ProductionMode_NotifiesFeedbackSnackbarInBackground()
    {
        const ulong testAddress = 0xE4928282D59B;
        var fakeNotifier = new FakeBluetoothFeedbackNotifier();
        var completedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var gateway = new WindowsBluetoothDeviceControlGateway(
            getInstalledServices: _ => new HashSet<Guid>(),
            setServiceState: (_, _, _) => 0,
            findDeviceAddressByName: _ => testAddress,
            waitForCompletion: false,
            feedbackNotifier: new CallbackBluetoothFeedbackNotifier(
                onStarting: (act, name) => fakeNotifier.NotifyStarting(act, name),
                onCompleted: (act, name, succ) =>
                {
                    fakeNotifier.NotifyCompleted(act, name, succ);
                    completedTcs.TrySetResult(succ);
                }),
            resolveDeviceDisplayName: _ => "Buds3 Pro");

        var command = new BluetoothAudioCommand(BluetoothAudioActionKind.Disconnect, "E4:92:82:82:D5:9B");
        var result = await gateway.TryControlAsync(command);

        Assert.True(result);
        var completed = await Task.WhenAny(completedTcs.Task, Task.Delay(2000));
        Assert.Same(completedTcs.Task, completed);
        Assert.True(await completedTcs.Task);

        Assert.Single(fakeNotifier.StartingCalls);
        Assert.Equal(BluetoothAudioActionKind.Disconnect, fakeNotifier.StartingCalls[0].Action);
        Assert.Single(fakeNotifier.CompletedCalls);
        Assert.True(fakeNotifier.CompletedCalls[0].Succeeded);
    }

    [Fact]
    public void WpfBluetoothActionSnackbarNotifier_WithoutDispatcher_NoOpsSafely()
    {
        var notifier = new WpfBluetoothActionSnackbarNotifier(dispatcher: null);
        notifier.NotifyStarting(BluetoothAudioActionKind.Connect, "Test Earbuds");
        notifier.NotifyCompleted(BluetoothAudioActionKind.Connect, "Test Earbuds", succeeded: true);
        notifier.NotifyCompleted(BluetoothAudioActionKind.Disconnect, "Test Earbuds", succeeded: false);
    }

    private sealed class FakeBluetoothFeedbackNotifier : IBluetoothActionFeedbackNotifier
    {
        public List<(BluetoothAudioActionKind Action, string DisplayName)> StartingCalls { get; } = new();
        public List<(BluetoothAudioActionKind Action, string DisplayName, bool Succeeded)> CompletedCalls { get; } = new();

        public void NotifyStarting(BluetoothAudioActionKind action, string deviceDisplayName)
        {
            StartingCalls.Add((action, deviceDisplayName));
        }

        public void NotifyCompleted(BluetoothAudioActionKind action, string deviceDisplayName, bool succeeded)
        {
            CompletedCalls.Add((action, deviceDisplayName, succeeded));
        }
    }

    private sealed class CallbackBluetoothFeedbackNotifier : IBluetoothActionFeedbackNotifier
    {
        private readonly Action<BluetoothAudioActionKind, string> _onStarting;
        private readonly Action<BluetoothAudioActionKind, string, bool> _onCompleted;

        public CallbackBluetoothFeedbackNotifier(
            Action<BluetoothAudioActionKind, string> onStarting,
            Action<BluetoothAudioActionKind, string, bool> onCompleted)
        {
            _onStarting = onStarting;
            _onCompleted = onCompleted;
        }

        public void NotifyStarting(BluetoothAudioActionKind action, string deviceDisplayName) =>
            _onStarting(action, deviceDisplayName);

        public void NotifyCompleted(BluetoothAudioActionKind action, string deviceDisplayName, bool succeeded) =>
            _onCompleted(action, deviceDisplayName, succeeded);
    }

    private sealed class FakeBluetoothDeviceControlGateway : IBluetoothDeviceControlGateway
    {
        private readonly Func<BluetoothAudioCommand, Task<bool>> _callback;

        public FakeBluetoothDeviceControlGateway(Func<BluetoothAudioCommand, Task<bool>> callback)
        {
            _callback = callback;
        }

        public Task<bool> TryControlAsync(BluetoothAudioCommand command, CancellationToken cancellationToken = default) =>
            _callback(command);
    }

    private sealed class FakeTerminalCommandGateway : ITerminalCommandGateway
    {
        private readonly Action _onExecute;

        public FakeTerminalCommandGateway(Action onExecute)
        {
            _onExecute = onExecute;
        }

        public bool TryExecute(
            string command,
            SnippetTerminalShell shell,
            bool runAsAdministrator,
            bool openTerminalWindow = false,
            string? workingDirectory = null)
        {
            _onExecute();
            return true;
        }
    }

    private sealed class FakeClipboardPasteGateway : IClipboardPasteGateway
    {
        public Task<bool> PasteActionAsync(ExecutableAction action, IntPtr targetWindowHandle, AppSettings settings) =>
            Task.FromResult(true);
    }

    private sealed class FakeFileLaunchGateway : IFileLaunchGateway
    {
        public bool TryLaunch(string path) => true;
    }

    private sealed class FakeUrlLaunchGateway : IUrlLaunchGateway
    {
        public bool TryLaunch(string url) => true;
    }

    private sealed class FakeMediaActionGateway : IMediaActionGateway
    {
        public bool TryExecute(SnippetMediaCommand command) => true;
    }

    private sealed class FakeSpotifyMediaActionGateway : ISpotifyMediaActionGateway
    {
        public Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(SnippetMediaCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpotifyMediaActionGatewayResult(true));
    }

    private sealed class FakeFilePasteGateway : IFilePasteGateway
    {
        public Task<FilePasteGatewayResult> PasteFileAsync(string filePath, IntPtr targetWindowHandle, AppSettings settings) =>
            Task.FromResult(FilePasteGatewayResult.Success());
    }

    private sealed class FakeDialogAdapter : IDialogAdapter
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
            return true;
        }
        public bool TryPromptAdbPort(string title, string fixedIp, out string port)
        {
            port = "5555";
            return true;
        }
    }
}
