using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class StartupRegistrationUseCaseTests
{
    [Fact]
    public void GetStateReturnsGatewayState()
    {
        var gateway = new RecordingStartupRegistrationGateway
        {
            State = new StartupRegistrationState(true, true)
        };
        var useCase = new StartupRegistrationUseCase(gateway);

        var state = useCase.GetState();

        Assert.True(state.IsEnabled);
        Assert.True(state.RunAsAdministrator);
    }

    [Fact]
    public void SaveClearsAdministratorFlagWhenStartupIsDisabled()
    {
        var gateway = new RecordingStartupRegistrationGateway();
        var useCase = new StartupRegistrationUseCase(gateway);

        var result = useCase.Save(new StartupRegistrationSettings(false, true));

        Assert.True(result.Succeeded);
        Assert.Equal(new StartupRegistrationSettings(false, false), Assert.Single(gateway.SavedSettings));
    }

    [Fact]
    public void SaveReturnsFailureWhenGatewayFails()
    {
        var gateway = new RecordingStartupRegistrationGateway
        {
            SaveResult = StartupRegistrationResult.Failure("failed")
        };
        var useCase = new StartupRegistrationUseCase(gateway);

        var result = useCase.Save(new StartupRegistrationSettings(true, false));

        Assert.False(result.Succeeded);
        Assert.Equal("failed", result.ErrorMessage);
    }

    [Fact]
    public void SaveReturnsFriendlyFailureWhenGatewayThrows()
    {
        var useCase = new StartupRegistrationUseCase(new ThrowingStartupRegistrationGateway());

        var result = useCase.Save(new StartupRegistrationSettings(true, false));

        Assert.False(result.Succeeded);
        Assert.Equal("시작프로그램 설정을 저장하지 못했습니다.", result.ErrorMessage);
    }

    private sealed class ThrowingStartupRegistrationGateway : IStartupRegistrationGateway
    {
        public StartupRegistrationState GetState()
        {
            throw new InvalidOperationException("failed");
        }

        public StartupRegistrationResult Save(StartupRegistrationSettings settings)
        {
            throw new InvalidOperationException("failed");
        }
    }
}
