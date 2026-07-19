using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.UseCases;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class UseCaseTests
{
    [Fact]
    public void SaveSnippetUseCaseSavesSnippetAndRequestsBackup()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var autoBackup = new RecordingAutoBackupCoordinator();
        var useCase = new SaveSnippetUseCase(
            services.SnippetRepository,
            services.SettingsRepository,
            autoBackup);

        var result = useCase.Execute(new SaveSnippetRequest(
            category.Id,
            SlotKey.Numpad3,
            SnippetId: null,
            IsSlotEnabled: true,
            OriginalIsSlotEnabled: true,
            Data: new SnippetSaveData(
                "Paste",
                "Hello",
                Description: null,
                ImagePath: null,
                ThumbnailPath: null,
                SnippetActionType.PasteText,
                LaunchPath: string.Empty,
                SlotImageMode.Auto,
                AutoIcon: null,
                LaunchUrl: null,
                SnippetMediaProvider.System,
                SnippetMediaCommand.PlayPause)));

        Assert.True(result.Succeeded);
        Assert.Equal("Paste", result.Snippet!.Title);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void ResolveCategoryHotkeyUseCaseOpensExistingCategory()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var useCase = new ResolveCategoryHotkeyUseCase(
            services.CategoryRepository,
            services.SettingsRepository);

        var result = useCase.Execute(SlotKey.Numpad1);

        Assert.Equal(CategoryHotkeyResolutionKind.OpenExisting, result.Kind);
        Assert.Equal(category.Id, result.Category!.Id);
    }

    [Fact]
    public void ResolveCategoryHotkeyUseCaseCreatesForEmptySlot()
    {
        var services = CreateServices();
        var useCase = new ResolveCategoryHotkeyUseCase(
            services.CategoryRepository,
            services.SettingsRepository);

        var result = useCase.Execute(SlotKey.Numpad2);

        Assert.Equal(CategoryHotkeyResolutionKind.CreateNew, result.Kind);
        Assert.Equal(SlotKey.Numpad2, result.SlotKey);
    }

    [Fact]
    public void ResolveCategoryHotkeyUseCaseBlocksDisabledSlot()
    {
        var services = CreateServices();
        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad1, false);
        var useCase = new ResolveCategoryHotkeyUseCase(
            services.CategoryRepository,
            services.SettingsRepository);

        var result = useCase.Execute(SlotKey.Numpad1);

        Assert.Equal(CategoryHotkeyResolutionKind.Blocked, result.Kind);
        Assert.Equal("슬롯 1은 사용 안 함 상태입니다.", result.StatusMessage);
    }

    [Fact]
    public void LoadCategoryEditorStateExcludesCurrentSlotAndShowsOccupiedTargets()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Writing", null);
        services.CategoryRepository.Create(SlotKey.Numpad5, "Old", null);
        services.SettingsRepository.SetCategorySlotEnabled(SlotKey.Numpad4, false);
        var useCase = new LoadCategoryEditorStateUseCase(
            services.CategoryRepository,
            services.SettingsRepository);

        var state = useCase.Execute(new LoadCategoryEditorStateRequest(SlotKey.Numpad4, source.Id));

        Assert.False(state.IsSlotEnabled);
        Assert.DoesNotContain(state.TransferTargets, target => target.SlotKey == SlotKey.Numpad4);
        Assert.Contains(
            state.TransferTargets,
            target => target.SlotKey == SlotKey.Numpad5 && target.ExistingTitle == "Old");
    }

    [Fact]
    public void LoadSnippetEditorStateReturnsSlotAndSpotifyState()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var source = services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Paste", "Hello", null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad5, "Old", "Bye", null);
        services.SettingsRepository.SetSnippetSlotEnabled(SlotKey.Numpad3, false);
        var settings = services.SettingsRepository.Load();
        settings.SpotifyClientId = "client-id";
        settings.SpotifyAccessToken = "access-token";
        settings.SpotifyRefreshToken = "refresh-token";
        settings.SpotifyConnectedUserDisplayName = "Spotify 계정";
        services.SettingsRepository.Save(settings);
        var useCase = new LoadSnippetEditorStateUseCase(
            services.SnippetRepository,
            services.SettingsRepository);

        var state = useCase.Execute(new LoadSnippetEditorStateRequest(category.Id, SlotKey.Numpad3, source.Id));

        Assert.False(state.IsSlotEnabled);
        Assert.True(state.SpotifyConnection.IsConnected);
        Assert.Equal("Spotify 계정", state.SpotifyConnection.DisplayName);
        Assert.DoesNotContain(state.TransferTargets, target => target.SlotKey == SlotKey.Numpad3);
        Assert.Contains(
            state.TransferTargets,
            target => target.SlotKey == SlotKey.Numpad5 && target.ExistingTitle == "Old");
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCaseReturnsLaunchFailureWithoutCallingPaste()
    {
        var pasteService = new RecordingClipboardPasteGateway();
        var launchService = new RecordingFileLaunchGatewayAdapter { Result = false };
        var useCase = new ExecuteSnippetActionUseCase(
            pasteService,
            launchService,
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new TestSpotifyMediaActionGateway(),
            new RecordingTerminalCommandGatewayAdapter(),
            new RecordingFilePasteGateway(),
            new RecordingDialogAdapter());

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "Missing file",
                ActionType = SnippetActionType.LaunchFile,
                LaunchPath = @"C:\missing.exe"
            },
            new AppSettings(),
            new IntPtr(123)));

        Assert.False(result.Succeeded);
        Assert.Contains("실행 실패", result.StatusMessage);
        Assert.Empty(pasteService.Calls);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCaseRoutesFilePasteWithoutLaunchingFile()
    {
        var filePasteGateway = new RecordingFilePasteGateway();
        var launchService = new RecordingFileLaunchGatewayAdapter();
        var useCase = new ExecuteSnippetActionUseCase(
            new RecordingClipboardPasteGateway(),
            launchService,
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new TestSpotifyMediaActionGateway(),
            new RecordingTerminalCommandGatewayAdapter(),
            filePasteGateway,
            new RecordingDialogAdapter());

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "Paste notes",
                ActionType = SnippetActionType.LaunchFile,
                FileActionMode = FileActionMode.Paste,
                LaunchPath = @"C:\notes\memo.md"
            },
            new AppSettings(),
            new IntPtr(123)));

        Assert.True(result.Succeeded);
        Assert.Contains("파일 붙여넣기 요청됨", result.StatusMessage);
        Assert.Empty(launchService.Paths);
        Assert.Equal(@"C:\notes\memo.md", Assert.Single(filePasteGateway.Calls).FilePath);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCaseRunsTerminalCommandWithoutCallingPaste()
    {
        var pasteService = new RecordingClipboardPasteGateway();
        var terminalService = new RecordingTerminalCommandGatewayAdapter();
        var useCase = new ExecuteSnippetActionUseCase(
            pasteService,
            new RecordingFileLaunchGatewayAdapter(),
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new TestSpotifyMediaActionGateway(),
            terminalService,
            new RecordingFilePasteGateway(),
            new RecordingDialogAdapter());

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "Reconnect Bluetooth",
                ActionType = SnippetActionType.TerminalCommand,
                TerminalCommand = "echo reconnect",
                TerminalShell = SnippetTerminalShell.PowerShell,
                RunAsAdministrator = true,
                OpenTerminalWindow = true,
                TerminalWorkingDirectory = @"C:\repos\demo"
            },
            new AppSettings(),
            new IntPtr(123)));

        Assert.True(result.Succeeded);
        Assert.Contains("터미널 명령 실행됨", result.StatusMessage);
        Assert.Empty(pasteService.Calls);
        var call = Assert.Single(terminalService.Calls);
        Assert.Equal("echo reconnect", call.Command);
        Assert.Equal(SnippetTerminalShell.PowerShell, call.Shell);
        Assert.True(call.RunAsAdministrator);
        Assert.True(call.OpenTerminalWindow);
        Assert.Equal(@"C:\repos\demo", call.WorkingDirectory);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCasePromptsAdbPortAndRunsCommand()
    {
        var terminalService = new RecordingTerminalCommandGatewayAdapter();
        var dialog = new RecordingDialogAdapter
        {
            PromptConfirmed = true,
            AdbPort = "41827"
        };
        var useCase = new ExecuteSnippetActionUseCase(
            new RecordingClipboardPasteGateway(),
            new RecordingFileLaunchGatewayAdapter(),
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new TestSpotifyMediaActionGateway(),
            terminalService,
            new RecordingFilePasteGateway(),
            dialog);

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "ADB Wireless",
                ActionType = SnippetActionType.TerminalCommand,
                TerminalCommand =
                    @"& ""$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"" connect {{IP}}:{{Port}}",
                TerminalShell = SnippetTerminalShell.PowerShell,
                OpenTerminalWindow = true,
                AdbDeviceIp = "10.42.17.83"
            },
            new AppSettings(),
            IntPtr.Zero));

        Assert.True(result.Succeeded);
        Assert.Equal(1, dialog.AdbPromptCount);
        Assert.Equal("10.42.17.83", dialog.LastAdbPromptIp);
        Assert.Contains("ADB 연결 실행됨", result.StatusMessage);
        var call = Assert.Single(terminalService.Calls);
        Assert.Equal(
            @"& ""$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"" connect 10.42.17.83:41827",
            call.Command);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCaseFailsAdbWhenIpNotConfigured()
    {
        var terminalService = new RecordingTerminalCommandGatewayAdapter();
        var dialog = new RecordingDialogAdapter();
        var useCase = new ExecuteSnippetActionUseCase(
            new RecordingClipboardPasteGateway(),
            new RecordingFileLaunchGatewayAdapter(),
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new TestSpotifyMediaActionGateway(),
            terminalService,
            new RecordingFilePasteGateway(),
            dialog);

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "ADB Wireless",
                ActionType = SnippetActionType.TerminalCommand,
                TerminalCommand = "adb connect {{IP}}:{{Port}}",
                AdbDeviceIp = string.Empty
            },
            new AppSettings(),
            IntPtr.Zero));

        Assert.False(result.Succeeded);
        Assert.Equal(0, dialog.AdbPromptCount);
        Assert.Contains("ADB IP 주소", result.StatusMessage);
        Assert.Empty(terminalService.Calls);
    }

    [Fact]
    public async Task ExecuteSnippetActionUseCaseCancelsAdbConnectWhenPromptCancelled()
    {
        var terminalService = new RecordingTerminalCommandGatewayAdapter();
        var dialog = new RecordingDialogAdapter { PromptConfirmed = false };
        var useCase = new ExecuteSnippetActionUseCase(
            new RecordingClipboardPasteGateway(),
            new RecordingFileLaunchGatewayAdapter(),
            new RecordingUrlLaunchGatewayAdapter(),
            new RecordingSystemMediaActionGatewayAdapter(),
            new TestSpotifyMediaActionGateway(),
            terminalService,
            new RecordingFilePasteGateway(),
            dialog);

        var result = await useCase.ExecuteAsync(new ExecuteSnippetActionRequest(
            new Snippet
            {
                Id = Guid.NewGuid(),
                Title = "ADB Wireless",
                ActionType = SnippetActionType.TerminalCommand,
                TerminalCommand = "adb connect {{IP}}:{{Port}}",
                AdbDeviceIp = "172.16.88.201"
            },
            new AppSettings(),
            IntPtr.Zero));

        Assert.False(result.Succeeded);
        Assert.Equal(1, dialog.AdbPromptCount);
        Assert.Contains("입력이 취소되었습니다", result.StatusMessage);
        Assert.Empty(terminalService.Calls);
    }

    [Fact]
    public void CreateManualBackupUseCaseStopsWhenBackupFolderIsInvalid()
    {
        var gateway = new RecordingBackupGateway
        {
            ValidationError = "invalid folder"
        };
        var useCase = new CreateManualBackupUseCase(gateway);

        var result = useCase.Execute(@"C:\app-data");

        Assert.False(result.Succeeded);
        Assert.Equal("invalid folder", result.ErrorMessage);
        Assert.Equal(0, gateway.CreateManualBackupCount);
    }

    [Fact]
    public void RestoreBackupUseCaseReturnsSafetyBackupPathFromGateway()
    {
        var gateway = new RecordingBackupGateway
        {
            RestoreResult = new RestoreBackupGatewayResult(true, @"C:\backups\safety.zip")
        };
        var useCase = new RestoreBackupUseCase(gateway);

        var result = useCase.Execute(@"C:\backups\deck.zip");

        Assert.True(result.Succeeded);
        Assert.Equal(@"C:\backups\deck.zip", gateway.RestoreBackupPath);
        Assert.Equal(@"C:\backups\safety.zip", result.SafetyBackupPath);
    }

    [Fact]
    public void SpotifyConnectionUseCaseReportsDisconnectedState()
    {
        var services = CreateServices();
        var useCase = new SpotifyConnectionUseCase(
            services.SettingsRepository,
            new RecordingSpotifyConnectionGateway(),
            new RecordingUrlLaunchGatewayAdapter());

        var state = useCase.GetState();

        Assert.False(state.IsConnected);
        Assert.Null(state.DisplayName);
    }

    [Fact]
    public async Task SpotifyConnectionUseCaseRequiresClientIdBeforeGatewayCall()
    {
        var services = CreateServices();
        var gateway = new RecordingSpotifyConnectionGateway();
        var useCase = new SpotifyConnectionUseCase(
            services.SettingsRepository,
            gateway,
            new RecordingUrlLaunchGatewayAdapter());

        var result = await useCase.ConnectAsync(" ");

        Assert.False(result.Succeeded);
        Assert.Equal("Spotify Client ID를 입력해 주세요.", result.ErrorMessage);
        Assert.Empty(gateway.ClientIds);
    }

    [Fact]
    public async Task SpotifyConnectionUseCaseConnectAndDisconnectReflectStoredState()
    {
        var services = CreateServices();
        var gateway = new RecordingSpotifyConnectionGateway
        {
            ConnectResult = new SpotifyConnectionGatewayResult(
                true,
                AccessToken: "access-token",
                RefreshToken: "refresh-token",
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
                DisplayName: "Spotify 계정")
        };
        var useCase = new SpotifyConnectionUseCase(
            services.SettingsRepository,
            gateway,
            new RecordingUrlLaunchGatewayAdapter());

        var connectResult = await useCase.ConnectAsync("client-id");
        var disconnectState = useCase.Disconnect();

        Assert.True(connectResult.Succeeded);
        Assert.True(connectResult.State!.IsConnected);
        Assert.Equal("Spotify 계정", connectResult.State.DisplayName);
        Assert.False(disconnectState.IsConnected);
    }

    private sealed class TestSpotifyMediaActionGateway : ISpotifyMediaActionGateway
    {
        public Task<SpotifyMediaActionGatewayResult> TryExecuteAsync(
            SnippetMediaCommand command,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpotifyMediaActionGatewayResult(true));
        }
    }

    private sealed class RecordingSpotifyConnectionGateway : ISpotifyConnectionGateway
    {
        public string DashboardUrl => "https://developer.spotify.com/dashboard";

        public string RedirectUri => "http://127.0.0.1:53682/spotify-callback/";

        public List<string> ClientIds { get; } = [];

        public SpotifyConnectionGatewayResult ConnectResult { get; init; } = new(true);

        public Task<SpotifyConnectionGatewayResult> ConnectAsync(
            string clientId,
            CancellationToken cancellationToken = default)
        {
            ClientIds.Add(clientId);
            return Task.FromResult(ConnectResult);
        }
    }

    private sealed class RecordingBackupGateway : IBackupGateway
    {
        public string? ValidationError { get; init; }

        public int CreateManualBackupCount { get; private set; }

        public string? RestoreBackupPath { get; private set; }

        public BackupGatewayResult CreateResult { get; init; } =
            new(true, @"C:\backups\manual.zip");

        public RestoreBackupGatewayResult RestoreResult { get; init; } =
            new(false, ErrorMessage: "restore failed");

        public string? ValidateBackupFolder(string? backupFolderPath)
        {
            return ValidationError;
        }

        public BackupGatewayResult CreateManualBackup(string backupFolderPath)
        {
            CreateManualBackupCount++;
            return CreateResult;
        }

        public RestoreBackupGatewayResult RestoreBackup(string backupZipPath)
        {
            RestoreBackupPath = backupZipPath;
            return RestoreResult;
        }
    }
}

