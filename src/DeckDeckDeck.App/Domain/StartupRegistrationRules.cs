// 역할: 윈도우 시작 프로그램 등록 시 필요한 작업 스케줄러 이름과 실행 인자 규칙을 정의합니다.
namespace DeckDeckDeck.App.Domain;

/// <summary>
/// Domain rules governing startup registration and administrator elevation invariants.
/// </summary>
public static class StartupRegistrationRules
{
    /// <summary>
    /// Administrator elevation at startup is only allowed when launch-at-startup is enabled.
    /// </summary>
    public static bool CanRunAsAdministrator(bool isLaunchAtStartup)
    {
        return isLaunchAtStartup;
    }

    /// <summary>
    /// Normalizes startup registration preferences so that administrator elevation
    /// cannot be active if launch-at-startup is disabled.
    /// </summary>
    public static (bool LaunchAtStartup, bool RunAsAdministrator) Normalize(
        bool isLaunchAtStartup,
        bool runAsAdministrator)
    {
        if (!isLaunchAtStartup)
        {
            return (false, false);
        }

        return (true, runAsAdministrator);
    }
}
