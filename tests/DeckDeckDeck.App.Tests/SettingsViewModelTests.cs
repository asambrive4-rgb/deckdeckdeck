using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
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
        var viewModel = CreateSettingsViewModel(
            services,
            afterSave: () => returned = true,
            showStatus: message => status = message);
        viewModel.BringWindowToFrontOnHotkey = false;
        viewModel.AutoHideAfterPaste = false;
        viewModel.RestoreClipboardAfterPaste = false;

        viewModel.SaveCommand.Execute(null);

        var reloaded = services.SettingsRepository.Load();
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
        var viewModel = CreateSettingsViewModel(
            services,
            autoBackupRequester: autoBackup);
        viewModel.AutoBackupEnabled = true;
        viewModel.BackupFolderPath = backupFolder;

        viewModel.SaveCommand.Execute(null);

        var reloaded = services.SettingsRepository.Load();
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
        var viewModel = CreateSettingsViewModel(
            services,
            showStatus: message => status = message);
        viewModel.AutoBackupEnabled = false;
        viewModel.BackupFolderPath = backupFolder;

        viewModel.CreateManualBackupCommand.Execute(null);

        var backupPath = Assert.Single(Directory.EnumerateFiles(backupFolder, "DeckDeckDeck-manual-*.zip"));
        Assert.Contains(Path.GetFileName(backupPath), status);
        Assert.NotNull(services.SettingsRepository.Load().LastBackupCreatedAt);
    }

    [Fact]
    public void ChooseBackupFolderCommandUpdatesFolderPath()
    {
        var services = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        var viewModel = CreateSettingsViewModel(
            services,
            dialogService: new StubDialogAdapter { BackupFolder = backupFolder });

        viewModel.ChooseBackupFolderCommand.Execute(null);

        Assert.Equal(backupFolder, viewModel.BackupFolderPath);
        Assert.Equal(backupFolder, viewModel.BackupFolderDisplay);
    }

    [Fact]
    public void RestoreBackupCommandRestoresSelectedZipAfterConfirm()
    {
        var backupSource = CreateServices();
        var backupFolder = CreateTempBackupFolder();
        backupSource.CategoryRepository.Create(SlotKey.Numpad4, "Restored", null);
        var backup = backupSource.BackupGateway.CreateManualBackup(backupFolder);
        Assert.True(backup.Succeeded);

        var services = CreateServices();
        var settings = services.SettingsRepository.Load();
        settings.BackupFolderPath = CreateTempBackupFolder();
        services.SettingsRepository.Save(settings);
        services.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);
        var status = string.Empty;
        var dialogService = new StubDialogAdapter { BackupZip = backup.BackupPath };
        var viewModel = CreateSettingsViewModel(
            services,
            showStatus: message => status = message,
            dialogService: dialogService);

        viewModel.RestoreBackupCommand.Execute(null);

        Assert.Equal(1, dialogService.ConfirmCount);
        Assert.Equal(1, dialogService.InformationCount);
        Assert.Equal("Restored", services.CategoryRepository.GetBySlotKey(SlotKey.Numpad4)!.Name);
        Assert.Null(services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5));
        Assert.Contains("앱을 다시 시작", status);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public void RestoreBackupCommandDoesNotRestoreWhenConfirmIsCancelled()
    {
        var backupSource = CreateServices();
        backupSource.CategoryRepository.Create(SlotKey.Numpad4, "Restored", null);
        var backup = backupSource.BackupGateway.CreateManualBackup(CreateTempBackupFolder());
        Assert.True(backup.Succeeded);

        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);
        var dialogService = new StubDialogAdapter
        {
            BackupZip = backup.BackupPath,
            ConfirmResult = false
        };
        var viewModel = CreateSettingsViewModel(
            services,
            dialogService: dialogService);

        viewModel.RestoreBackupCommand.Execute(null);

        Assert.Equal(1, dialogService.ConfirmCount);
        Assert.Equal(0, dialogService.InformationCount);
        Assert.Null(services.CategoryRepository.GetBySlotKey(SlotKey.Numpad4));
        Assert.Equal("Current", services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void RestoreBackupCommandDoesNothingWhenZipSelectionIsCancelled()
    {
        var services = CreateServices();
        services.CategoryRepository.Create(SlotKey.Numpad5, "Current", null);
        var dialogService = new StubDialogAdapter { BackupZip = null };
        var viewModel = CreateSettingsViewModel(
            services,
            dialogService: dialogService);

        viewModel.RestoreBackupCommand.Execute(null);

        Assert.Equal(0, dialogService.ConfirmCount);
        Assert.Equal(0, dialogService.InformationCount);
        Assert.Equal("Current", services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5)!.Name);
    }

    [Fact]
    public void RestoreBackupCommandDelegatesConfirmedZipToUseCase()
    {
        var services = CreateServices();
        var status = string.Empty;
        var dialogService = new StubDialogAdapter { BackupZip = @"C:\backups\deck.zip" };
        var restoreUseCase = new RecordingRestoreBackupUseCase
        {
            Result = RestoreBackupUseCaseResult.Success(@"C:\backups\safety.zip")
        };
        var viewModel = CreateSettingsViewModel(
            services,
            showStatus: message => status = message,
            backupGateway: null,
            dialogService: dialogService,
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
        var spotifyConnectionService = new StubSpotifyConnectionGatewayAdapter();
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
        var urlLaunchService = new RecordingUrlLaunchGatewayAdapter();
        var spotifyConnectionService = new StubSpotifyConnectionGatewayAdapter();
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
        var clipboard = new FakeClipboardAdapter(null);
        var spotifyConnectionService = new StubSpotifyConnectionGatewayAdapter();
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
        var clipboard = new FakeClipboardAdapter(null);
        var spotifyConnectionService = new StubSpotifyConnectionGatewayAdapter();
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
        var spotifyConnectionService = new StubSpotifyConnectionGatewayAdapter();
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
        var spotifyConnectionService = new StubSpotifyConnectionGatewayAdapter();
        var viewModel = CreateSettingsViewModel(
            services,
            spotifyConnectionService,
            showStatus: message => status = message);
        var command = Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.StartSpotifyConnectionCommand);
        viewModel.ShowSpotifyConnectionFieldsCommand.Execute(null);
        viewModel.SpotifyClientIdInput = "client-id";

        await command.ExecuteAsync(null);

        var settings = services.SettingsRepository.Load();
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
        var settings = services.SettingsRepository.Load();
        settings.SpotifyClientId = "client-id";
        settings.SpotifyAccessToken = "access-token";
        settings.SpotifyRefreshToken = "refresh-token";
        settings.SpotifyTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        settings.SpotifyConnectedUserDisplayName = "Spotify 계정";
        services.SettingsRepository.Save(settings);
        var status = string.Empty;
        var spotifyConnectionService = new StubSpotifyConnectionGatewayAdapter();
        var viewModel = CreateSettingsViewModel(
            services,
            spotifyConnectionService,
            showStatus: message => status = message);

        viewModel.DisconnectSpotifyCommand.Execute(null);

        var reloaded = services.SettingsRepository.Load();
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
        ISpotifyConnectionGateway? spotifyConnectionService = null,
        IUrlLaunchGateway? urlLaunchService = null,
        Action? cancel = null,
        Action? afterSave = null,
        Action<string>? showStatus = null,
        IClipboardTextWriter? clipboardService = null,
        IBackupGateway? backupGateway = null,
        IAutoBackupRequester? autoBackupRequester = null,
        IDialogAdapter? dialogService = null,
        ISaveSettingsUseCase? saveSettingsUseCase = null,
        ICreateManualBackupUseCase? createManualBackupUseCase = null,
        IRestoreBackupUseCase? restoreBackupUseCase = null)
    {
        return TestAppFactory.CreateSettingsViewModel(
            services,
            cancel,
            afterSave,
            showStatus,
            services.FileLogger,
            backupGateway,
            autoBackupRequester,
            dialogService ?? new StubDialogAdapter(),
            CreateSpotifyConnectionUseCase(
                services,
                spotifyConnectionService ?? new StubSpotifyConnectionGatewayAdapter(),
                urlLaunchService ?? new RecordingUrlLaunchGatewayAdapter()),
            clipboardService ?? new FakeClipboardAdapter(null),
            saveSettingsUseCase,
            createManualBackupUseCase,
            restoreBackupUseCase);
    }

    private static ISpotifyConnectionUseCase CreateSpotifyConnectionUseCase(
        TestServices services,
        ISpotifyConnectionGateway? spotifyConnectionService = null,
        IUrlLaunchGateway? urlLaunchService = null)
    {
        var effectiveUrlLaunchGatewayAdapter = urlLaunchService ?? new RecordingUrlLaunchGatewayAdapter();
        var effectiveSpotifyConnectionGatewayAdapter = spotifyConnectionService
            ?? new StubSpotifyConnectionGatewayAdapter();

        return new SpotifyConnectionUseCase(
            services.SettingsRepository,
            effectiveSpotifyConnectionGatewayAdapter,
            effectiveUrlLaunchGatewayAdapter);
    }

    private static string CreateTempBackupFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deckdeckdeck-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }

    private sealed class StubDialogAdapter : DialogAdapter
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

    private sealed class StubSpotifyConnectionGatewayAdapter : ISpotifyConnectionGateway
    {
        public string DashboardUrl => "https://developer.spotify.com/dashboard";

        public string RedirectUri => "http://127.0.0.1:53682/spotify-callback/";

        public List<string> ClientIds { get; } = [];

        public SpotifyConnectionGatewayResult ConnectResult { get; set; } = new(
            true,
            AccessToken: "access-token",
            RefreshToken: "refresh-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            DisplayName: "Spotify 계정");

        public Task<SpotifyConnectionGatewayResult> ConnectAsync(
            string clientId,
            CancellationToken cancellationToken = default)
        {
            ClientIds.Add(clientId);
            return Task.FromResult(ConnectResult);
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



