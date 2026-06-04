using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
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
            services.LoggingService)
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
            autoBackup)
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
            services.BackupService)
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
            dialogService: new StubDialogService { BackupFolder = backupFolder });

        viewModel.ChooseBackupFolderCommand.Execute(null);

        Assert.Equal(backupFolder, viewModel.BackupFolderPath);
        Assert.Equal(backupFolder, viewModel.BackupFolderDisplay);
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

        public override string? SelectBackupFolder()
        {
            return BackupFolder;
        }
    }
}
