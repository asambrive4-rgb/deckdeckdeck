using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using CommunityToolkit.Mvvm.Input;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class SettingsViewModelTests
{
    [Fact]
    public void SettingsViewModelSavesSettingsAndReturns()
    {
        var services = CreateServices();
        var returned = false;
        var status = string.Empty;
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => returned = true,
            message => status = message,
            services.LoggingService,
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null))
        {
            BringWindowToFrontOnHotkey = false,
            AutoHideAfterPaste = false,
            RestoreClipboardAfterPaste = false
        };

        viewModel.SaveCommand.Execute(null);

        var reloaded = services.SettingsService.Load();
        Assert.False(reloaded.BringWindowToFrontOnHotkey);
        Assert.False(reloaded.AutoHideAfterPaste);
        Assert.False(reloaded.RestoreClipboardAfterPaste);
        Assert.True(returned);
        Assert.Equal("설정을 저장했습니다.", status);
    }

    [Fact]
    public void SettingsViewModelSaveRequestsAutoBackup()
    {
        var services = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        var autoBackup = new RecordingAutoBackupCoordinator();
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            _ => { },
            services.LoggingService,
            services.BackupService,
            autoBackup,
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null))
        {
            AutoBackupEnabled = true,
            BackupFolderPath = backupFolder
        };

        viewModel.SaveCommand.Execute(null);

        var reloaded = services.SettingsService.Load();
        Assert.True(reloaded.AutoBackupEnabled);
        Assert.Equal(backupFolder, reloaded.BackupFolderPath);
        Assert.Equal(1, autoBackup.RequestCount);
    }

    [Fact]
    public void ManualBackupCommandRunsEvenWhenAutoBackupIsDisabled()
    {
        var services = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        var status = string.Empty;
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            message => status = message,
            services.LoggingService,
            services.BackupService,
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null))
        {
            AutoBackupEnabled = false,
            BackupFolderPath = backupFolder
        };

        viewModel.CreateManualBackupCommand.Execute(null);

        var backupPath = Assert.Single(Directory.EnumerateFiles(backupFolder, "DeckDeckDeck-manual-*.zip"));
        Assert.Contains(Path.GetFileName(backupPath), status);
        Assert.NotNull(services.SettingsService.Load().LastBackupCreatedAt);
    }

    [Fact]
    public void ChooseBackupFolderCommandUpdatesFolderPath()
    {
        var services = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            _ => { },
            services.LoggingService,
            services.BackupService,
            dialogService: new StubDialogService { BackupFolder = backupFolder },
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null));

        viewModel.ChooseBackupFolderCommand.Execute(null);

        Assert.Equal(backupFolder, viewModel.BackupFolderPath);
        Assert.Equal(backupFolder, viewModel.BackupFolderDisplay);
    }

    [Fact]
    public void RestoreBackupCommandRestoresSelectedZipAfterConfirm()
    {
        var backupSource = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        backupSource.CategoryService.Create(SlotKey.Numpad4, "Restored", null);
        var backup = backupSource.BackupService.CreateManualBackup(backupFolder);
        Assert.True(backup.Succeeded);

        var services = CreateServices();
        var settings = services.SettingsService.Load();
        settings.BackupFolderPath = CreateTempBackupFolder();
        services.SettingsService.Save(settings);
        services.CategoryService.Create(SlotKey.Numpad5, "Current", null);
        var status = string.Empty;
        var dialogService = new StubDialogService { BackupZip = backup.BackupPath };
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            message => status = message,
            services.LoggingService,
            services.BackupService,
            dialogService: dialogService,
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null));

        viewModel.RestoreBackupCommand.Execute(null);

        Assert.Equal(1, dialogService.ConfirmCount);
        Assert.Equal(1, dialogService.InformationCount);
        Assert.Equal("Restored", services.CategoryService.GetBySlotKey(SlotKey.Numpad4)!.Name);
        Assert.Null(services.CategoryService.GetBySlotKey(SlotKey.Numpad5));
        Assert.Contains("앱을 다시 시작", status);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public void RestoreBackupCommandDoesNotRestoreWhenConfirmIsCancelled()
    {
        var backupSource = CreateServices();
        backupSource.CategoryService.Create(SlotKey.Numpad4, "Restored", null);
        var backup = backupSource.BackupService.CreateManualBackup(CreateTempBackupFolder());
        Assert.True(backup.Succeeded);

        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad5, "Current", null);
        var dialogService = new StubDialogService
        {
            BackupZip = backup.BackupPath,
            ConfirmResult = false
        };
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            _ => { },
            services.LoggingService,
            services.BackupService,
            dialogService: dialogService,
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null));

        viewModel.RestoreBackupCommand.Execute(null);

        Assert.Equal(1, dialogService.ConfirmCount);
        Assert.Equal(0, dialogService.InformationCount);
        Assert.Null(services.CategoryService.GetBySlotKey(SlotKey.Numpad4));
        Assert.Equal("Current", services.CategoryService.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void RestoreBackupCommandDoesNothingWhenZipSelectionIsCancelled()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad5, "Current", null);
        var dialogService = new StubDialogService { BackupZip = null };
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            _ => { },
            services.LoggingService,
            services.BackupService,
            dialogService: dialogService,
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null));

        viewModel.RestoreBackupCommand.Execute(null);

        Assert.Equal(0, dialogService.ConfirmCount);
        Assert.Equal(0, dialogService.InformationCount);
        Assert.Equal("Current", services.CategoryService.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void RestoreBackupCommandDelegatesConfirmedZipToUseCase()
    {
        var services = CreateServices();
        var status = string.Empty;
        var dialogService = new StubDialogService { BackupZip = @"C:\backups\deck.zip" };
        var restoreUseCase = new RecordingRestoreBackupUseCase
        {
            Result = RestoreBackupUseCaseResult.Success(@"C:\backups\safety.zip")
        };
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            message => status = message,
            services.LoggingService,
            backupGateway: null,
            dialogService: dialogService,
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(services),
            clipboardService: new FakeClipboardService(null),
            restoreBackupUseCase: restoreUseCase);

        viewModel.RestoreBackupCommand.Execute(null);

        Assert.Equal(@"C:\backups\deck.zip", restoreUseCase.BackupZipPath);
        Assert.Equal(1, dialogService.ConfirmCount);
        Assert.Equal(1, dialogService.InformationCount);
        Assert.Contains("safety.zip", status);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public void SpotifyConnectionStatusStartsDisconnected()
    {
        var services = CreateServices();
        var spotifyConnectionService = new StubSpotifyConnectionService(services.SettingsService);
        var viewModel = CreateSettingsViewModel(services, spotifyConnectionService);

        Assert.False(viewModel.IsSpotifyConnected);
        Assert.True(viewModel.ShowSpotifyConnectButton);
        Assert.False(viewModel.ShowSpotifyConnectionFields);
        Assert.Equal("Spotify 연결되어 있지 않음", viewModel.SpotifyConnectionStatusText);
    }

    [Fact]
    public void SpotifyConnectCommandShowsDashboardAndClientIdFields()
    {
        var services = CreateServices();
        var urlLaunchService = new RecordingUrlLaunchService();
        var spotifyConnectionService = new StubSpotifyConnectionService(services.SettingsService);
        var viewModel = CreateSettingsViewModel(services, spotifyConnectionService, urlLaunchService);

        viewModel.ShowSpotifyConnectionFieldsCommand.Execute(null);
        viewModel.OpenSpotifyDeveloperDashboardCommand.Execute(null);

        Assert.True(viewModel.ShowSpotifyConnectionFields);
        Assert.False(viewModel.ShowSpotifyConnectButton);
        Assert.Equal([spotifyConnectionService.DashboardUrl], urlLaunchService.Urls);
    }

    [Fact]
    public void SpotifyRedirectUriCopyCommandCopiesCallbackAddress()
    {
        var services = CreateServices();
        var status = string.Empty;
        var clipboard = new FakeClipboardService(null);
        var spotifyConnectionService = new StubSpotifyConnectionService(services.SettingsService);
        var viewModel = CreateSettingsViewModel(
            services,
            spotifyConnectionService,
            clipboardService: clipboard,
            showStatus: message => status = message);

        viewModel.CopySpotifyRedirectUriCommand.Execute(null);

        Assert.Equal([spotifyConnectionService.RedirectUri], clipboard.SetTexts);
        Assert.Equal("Spotify Redirect URI를 복사했습니다.", status);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public void SpotifyAppExampleCopyCommandsCopySuggestedValues()
    {
        var services = CreateServices();
        var statusMessages = new List<string>();
        var clipboard = new FakeClipboardService(null);
        var spotifyConnectionService = new StubSpotifyConnectionService(services.SettingsService);
        var viewModel = CreateSettingsViewModel(
            services,
            spotifyConnectionService,
            clipboardService: clipboard,
            showStatus: statusMessages.Add);

        viewModel.CopySpotifyAppNameExampleCommand.Execute(null);
        viewModel.CopySpotifyAppDescriptionExampleCommand.Execute(null);

        Assert.Equal(
            [viewModel.SpotifyAppNameExample, viewModel.SpotifyAppDescriptionExample],
            clipboard.SetTexts);
        Assert.Equal(
            ["Spotify App name 예시를 복사했습니다.", "Spotify App description 예시를 복사했습니다."],
            statusMessages);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SpotifyStartConnectionRequiresClientId()
    {
        var services = CreateServices();
        var spotifyConnectionService = new StubSpotifyConnectionService(services.SettingsService);
        var viewModel = CreateSettingsViewModel(services, spotifyConnectionService);
        var command = Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.StartSpotifyConnectionCommand);

        await command.ExecuteAsync(null);

        Assert.Equal("Spotify Client ID를 입력해 주세요.", viewModel.ErrorMessage);
        Assert.Empty(spotifyConnectionService.ClientIds);
    }

    [Fact]
    public async Task SpotifyStartConnectionSavesConnectionAndHidesFields()
    {
        var services = CreateServices();
        var status = string.Empty;
        var spotifyConnectionService = new StubSpotifyConnectionService(services.SettingsService);
        var viewModel = CreateSettingsViewModel(
            services,
            spotifyConnectionService,
            showStatus: message => status = message);
        var command = Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.StartSpotifyConnectionCommand);
        viewModel.ShowSpotifyConnectionFieldsCommand.Execute(null);
        viewModel.SpotifyClientIdInput = "client-id";

        await command.ExecuteAsync(null);

        var settings = services.SettingsService.Load();
        Assert.True(viewModel.IsSpotifyConnected);
        Assert.False(viewModel.ShowSpotifyConnectionFields);
        Assert.Equal(["client-id"], spotifyConnectionService.ClientIds);
        Assert.Equal("client-id", settings.SpotifyClientId);
        Assert.Equal("Spotify 연결됨.", status);
    }

    [Fact]
    public void SpotifyDisconnectClearsStoredConnection()
    {
        var services = CreateServices();
        var settings = services.SettingsService.Load();
        settings.SpotifyClientId = "client-id";
        settings.SpotifyAccessToken = "access-token";
        settings.SpotifyRefreshToken = "refresh-token";
        settings.SpotifyTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        settings.SpotifyConnectedUserDisplayName = "Spotify 계정";
        services.SettingsService.Save(settings);
        var status = string.Empty;
        var spotifyConnectionService = new StubSpotifyConnectionService(services.SettingsService);
        var viewModel = CreateSettingsViewModel(
            services,
            spotifyConnectionService,
            showStatus: message => status = message);

        viewModel.DisconnectSpotifyCommand.Execute(null);

        var reloaded = services.SettingsService.Load();
        Assert.False(viewModel.IsSpotifyConnected);
        Assert.Equal("Spotify 연결되어 있지 않음", viewModel.SpotifyConnectionStatusText);
        Assert.Equal(string.Empty, reloaded.SpotifyClientId);
        Assert.Equal(string.Empty, reloaded.SpotifyAccessToken);
        Assert.Equal(string.Empty, reloaded.SpotifyRefreshToken);
        Assert.Null(reloaded.SpotifyTokenExpiresAt);
        Assert.Equal("Spotify 연결을 해제했습니다.", status);
    }

    private static SettingsViewModel CreateSettingsViewModel(
        TestServices services,
        ISpotifyConnectionService spotifyConnectionService,
        IUrlLaunchService? urlLaunchService = null,
        Action<string>? showStatus = null,
        IClipboardService? clipboardService = null)
    {
        return new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => { },
            showStatus ?? (_ => { }),
            services.LoggingService,
            services.BackupService,
            dialogService: new StubDialogService(),
            spotifyConnectionUseCase: CreateSpotifyConnectionUseCase(
                services,
                spotifyConnectionService,
                urlLaunchService ?? new RecordingUrlLaunchService()),
            clipboardService: clipboardService ?? new FakeClipboardService(null));
    }

    private static ISpotifyConnectionUseCase CreateSpotifyConnectionUseCase(
        TestServices services,
        ISpotifyConnectionService? spotifyConnectionService = null,
        IUrlLaunchService? urlLaunchService = null)
    {
        var effectiveUrlLaunchService = urlLaunchService ?? new RecordingUrlLaunchService();
        var effectiveSpotifyConnectionService = spotifyConnectionService
            ?? new StubSpotifyConnectionService(services.SettingsService);

        return new SpotifyConnectionUseCase(
            services.SettingsService,
            new SpotifyConnectionGatewayAdapter(effectiveSpotifyConnectionService),
            effectiveUrlLaunchService);
    }

    private static string CreateTempBackupFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deckdeckdeck-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }

    private sealed class StubDialogService : DialogService
    {
        public string? BackupFolder { get; init; }

        public string? BackupZip { get; init; }

        public bool ConfirmResult { get; init; } = true;

        public int ConfirmCount { get; private set; }

        public int InformationCount { get; private set; }

        public override string? SelectBackupFolder()
        {
            return BackupFolder;
        }

        public override string? SelectBackupZipFile()
        {
            return BackupZip;
        }

        public override bool Confirm(string title, string message)
        {
            ConfirmCount++;
            return ConfirmResult;
        }

        public override void ShowInformation(string title, string message)
        {
            InformationCount++;
        }
    }

    private sealed class StubSpotifyConnectionService : ISpotifyConnectionService
    {
        private readonly SettingsService _settingsService;

        public StubSpotifyConnectionService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public string DashboardUrl => "https://developer.spotify.com/dashboard";

        public string RedirectUri => "http://127.0.0.1:53682/spotify-callback/";

        public List<string> ClientIds { get; } = [];

        public SpotifyConnectionResult ConnectResult { get; set; } = new(true);

        public Task<SpotifyConnectionResult> ConnectAsync(
            string clientId,
            CancellationToken cancellationToken = default)
        {
            ClientIds.Add(clientId);
            if (ConnectResult.Succeeded)
            {
                var settings = _settingsService.Load();
                settings.SpotifyClientId = clientId;
                settings.SpotifyAccessToken = "access-token";
                settings.SpotifyRefreshToken = "refresh-token";
                settings.SpotifyTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
                settings.SpotifyConnectedUserDisplayName = "Spotify 계정";
                _settingsService.Save(settings);
            }

            return Task.FromResult(ConnectResult);
        }

        public Task<SpotifyConnectionCheckResult> CheckConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpotifyConnectionCheckResult(SpotifyConnectionCheckState.Connected));
        }

        public void Disconnect()
        {
            var settings = _settingsService.Load();
            settings.SpotifyClientId = string.Empty;
            settings.SpotifyAccessToken = string.Empty;
            settings.SpotifyRefreshToken = string.Empty;
            settings.SpotifyTokenExpiresAt = null;
            settings.SpotifyConnectedUserDisplayName = string.Empty;
            _settingsService.Save(settings);
        }
    }

    private sealed class RecordingRestoreBackupUseCase : IRestoreBackupUseCase
    {
        public string? BackupZipPath { get; private set; }

        public RestoreBackupUseCaseResult Result { get; init; } =
            RestoreBackupUseCaseResult.Failure("failed");

        public RestoreBackupUseCaseResult Execute(string backupZipPath)
        {
            BackupZipPath = backupZipPath;
            return Result;
        }
    }
}
