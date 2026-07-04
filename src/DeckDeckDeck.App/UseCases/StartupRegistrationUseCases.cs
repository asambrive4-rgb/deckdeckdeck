using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public interface IStartupRegistrationUseCase
{
    StartupRegistrationState GetState();

    StartupRegistrationResult Save(StartupRegistrationSettings settings);
}

public sealed class StartupRegistrationUseCase : IStartupRegistrationUseCase
{
    private readonly IStartupRegistrationGateway _startupRegistrationGateway;

    public StartupRegistrationUseCase(IStartupRegistrationGateway startupRegistrationGateway)
    {
        _startupRegistrationGateway = startupRegistrationGateway;
    }

    public StartupRegistrationState GetState()
    {
        try
        {
            var state = _startupRegistrationGateway.GetState();
            return state.IsEnabled
                ? new StartupRegistrationState(true, state.RunAsAdministrator)
                : StartupRegistrationState.Disabled;
        }
        catch
        {
            return StartupRegistrationState.Disabled;
        }
    }

    public StartupRegistrationResult Save(StartupRegistrationSettings settings)
    {
        try
        {
            var normalizedSettings = settings.IsEnabled
                ? settings
                : new StartupRegistrationSettings(false, false);

            return _startupRegistrationGateway.Save(normalizedSettings);
        }
        catch
        {
            return StartupRegistrationResult.Failure(
                "시작프로그램 설정을 저장하지 못했습니다.");
        }
    }
}
