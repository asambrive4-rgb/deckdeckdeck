using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class SaveAppPreferencesUseCaseTests
{
    [Fact]
    public void ExecuteSavesSettingsThenStartupRegistration()
    {
        var saveSettings = new RecordingSaveSettingsUseCase();
        var startup = new RecordingStartupRegistrationUseCase();
        var useCase = new SaveAppPreferencesUseCase(saveSettings, startup);
        var request = CreateRequest(launchAtStartup: true, runAsAdministrator: true);

        var result = useCase.Execute(request);

        Assert.True(result.Succeeded);
        Assert.Equal(SaveAppPreferencesFailureKind.None, result.FailureKind);
        Assert.Equal(request.Settings, Assert.Single(saveSettings.Requests));
        Assert.Equal(request.Startup, Assert.Single(startup.SavedSettings));
    }

    [Fact]
    public void ExecuteDoesNotSaveStartupWhenSettingsFail()
    {
        var saveSettings = new RecordingSaveSettingsUseCase
        {
            Result = SaveSettingsResult.Failure("백업 폴더를 선택해 주세요.")
        };
        var startup = new RecordingStartupRegistrationUseCase();
        var useCase = new SaveAppPreferencesUseCase(saveSettings, startup);

        var result = useCase.Execute(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(SaveAppPreferencesFailureKind.Settings, result.FailureKind);
        Assert.Equal("백업 폴더를 선택해 주세요.", result.ErrorMessage);
        Assert.Empty(startup.SavedSettings);
    }

    [Fact]
    public void ExecuteReturnsStartupFailureAfterSuccessfulSettingsSave()
    {
        var saveSettings = new RecordingSaveSettingsUseCase();
        var startup = new RecordingStartupRegistrationUseCase
        {
            SaveResult = StartupRegistrationResult.Failure("시작프로그램 등록 실패")
        };
        var useCase = new SaveAppPreferencesUseCase(saveSettings, startup);

        var result = useCase.Execute(CreateRequest(launchAtStartup: true));

        Assert.False(result.Succeeded);
        Assert.Equal(SaveAppPreferencesFailureKind.StartupRegistration, result.FailureKind);
        Assert.Equal("시작프로그램 등록 실패", result.ErrorMessage);
        Assert.Single(saveSettings.Requests);
        Assert.Single(startup.SavedSettings);
    }

    [Fact]
    public void ExecuteUsesDefaultStartupFailureMessageWhenGatewayMessageIsMissing()
    {
        var saveSettings = new RecordingSaveSettingsUseCase();
        var startup = new RecordingStartupRegistrationUseCase
        {
            SaveResult = new StartupRegistrationResult(false, ErrorMessage: null)
        };
        var useCase = new SaveAppPreferencesUseCase(saveSettings, startup);

        var result = useCase.Execute(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(
            SaveAppPreferencesUseCase.StartupRegistrationSaveFailedMessage,
            result.ErrorMessage);
    }

    private static SaveAppPreferencesRequest CreateRequest(
        bool launchAtStartup = false,
        bool runAsAdministrator = false)
    {
        return new SaveAppPreferencesRequest(
            new SaveSettingsRequest(
                BringWindowToFrontOnHotkey: true,
                AutoHideAfterPaste: true,
                RestoreClipboardAfterPaste: true,
                AutoBackupEnabled: false,
                BackupFolderPath: string.Empty),
            new StartupRegistrationSettings(launchAtStartup, runAsAdministrator));
    }

    private sealed class RecordingSaveSettingsUseCase : ISaveSettingsUseCase
    {
        public List<SaveSettingsRequest> Requests { get; } = [];

        public SaveSettingsResult Result { get; init; } = SaveSettingsResult.Success();

        public SaveSettingsResult Execute(SaveSettingsRequest request)
        {
            Requests.Add(request);
            return Result;
        }
    }
}
