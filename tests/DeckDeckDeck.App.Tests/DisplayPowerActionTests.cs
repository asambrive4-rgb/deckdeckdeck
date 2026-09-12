// 역할: 화면 끄기/켜기 동작 명령이 올바르게 전달되고 실행되는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class DisplayPowerActionTests
{
    [Fact]
    public void IsTurnOffDisplayCommand_DetectsCommandsCorrectly()
    {
        Assert.True(TerminalCommandParameterRules.IsTurnOffDisplayCommand(
            TerminalCommandParameterRules.TurnOffDisplayCommand));
        Assert.True(TerminalCommandParameterRules.IsTurnOffDisplayCommand(
            "powershell (Add-Type '[DllImport(\"user32.dll\")]public static extern int PostMessage(int hWnd,int hMsg,int wParam,int lParam);' -Name a -Passthru)::PostMessage(-1,0x0112,0xF170,2)"));
        Assert.False(TerminalCommandParameterRules.IsTurnOffDisplayCommand(null));
        Assert.False(TerminalCommandParameterRules.IsTurnOffDisplayCommand(string.Empty));
        Assert.False(TerminalCommandParameterRules.IsTurnOffDisplayCommand("notepad.exe"));
        Assert.False(TerminalCommandParameterRules.IsTurnOffDisplayCommand("adb connect 192.168.1.1:5555"));
    }

    [Fact]
    public void DisplayPowerGatewayAdapter_CallsPostMessageWithCorrectConstants()
    {
        IntPtr capturedHwnd = IntPtr.Zero;
        uint capturedMsg = 0;
        IntPtr capturedWParam = IntPtr.Zero;
        IntPtr capturedLParam = IntPtr.Zero;

        var adapter = new DisplayPowerGatewayAdapter((hWnd, msg, wParam, lParam) =>
        {
            capturedHwnd = hWnd;
            capturedMsg = msg;
            capturedWParam = wParam;
            capturedLParam = lParam;
            return true;
        });

        var result = adapter.TryTurnOffDisplay();

        Assert.True(result);
        Assert.Equal(Win32Constants.HwndBroadcast, capturedHwnd);
        Assert.Equal(Win32Constants.WmSyscommand, capturedMsg);
        Assert.Equal(Win32Constants.ScMonitorpower, capturedWParam);
        Assert.Equal(Win32Constants.MonitorPowerOff, capturedLParam);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCase_DisplayOffAction_AlwaysHidesWindowAndCallsGateway()
    {
        var displayGatewayCalled = false;
        var displayGateway = new FakeDisplayPowerGateway(() =>
        {
            displayGatewayCalled = true;
            return true;
        });

        var useCase = new ExecuteSnippetActionUseCase(
            new FakeClipboardPasteGateway(),
            new FakeFileLaunchGateway(),
            new FakeUrlLaunchGateway(),
            new FakeMediaActionGateway(),
            new FakeSpotifyMediaActionGateway(),
            new FakeTerminalCommandGateway(),
            new FakeFilePasteGateway(),
            new FakeDialogAdapter(),
            displayPowerGateway: displayGateway);

        var action = new ExecutableAction(
            Id: Guid.NewGuid(),
            Title: "화면 끄기",
            Content: string.Empty,
            ActionType: SnippetActionType.TerminalCommand,
            PasteShortcutMode: PasteShortcutMode.CtrlV,
            LaunchPath: null,
            FileActionMode: FileActionMode.Launch,
            LaunchUrl: null,
            MediaProvider: null,
            MediaCommand: null,
            TerminalCommand: TerminalCommandParameterRules.TurnOffDisplayCommand,
            TerminalShell: SnippetTerminalShell.Cmd,
            RunAsAdministrator: false,
            OpenTerminalWindow: false);

        var settings = new AppSettings
        {
            AutoHideAfterPaste = false // Even if AutoHide is false, display off should hide window
        };

        var request = new ExecuteSnippetActionRequest(action, settings, IntPtr.Zero);
        var result = await useCase.ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.True(result.ShouldHideWindow);
        Assert.True(displayGatewayCalled);
    }

    private sealed class FakeDisplayPowerGateway : IDisplayPowerGateway
    {
        private readonly Func<bool> _callback;

        public FakeDisplayPowerGateway(Func<bool> callback)
        {
            _callback = callback;
        }

        public bool TryTurnOffDisplay() => _callback();
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

    private sealed class FakeTerminalCommandGateway : ITerminalCommandGateway
    {
        public bool TryExecute(string command, SnippetTerminalShell shell, bool runAsAdministrator, bool openTerminalWindow = false, string? workingDirectory = null) =>
            true;
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
