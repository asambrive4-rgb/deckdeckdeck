// 역할: 윈도우 부팅 시 자동 시작 등록에 필요한 작업 스케줄러 이름 및 인수 규칙을 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Domain;
using Xunit;

namespace DeckDeckDeck.App.Tests;

public sealed class StartupRegistrationRulesTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CanRunAsAdministrator_RequiresLaunchAtStartup(bool launchAtStartup, bool expected)
    {
        var result = StartupRegistrationRules.CanRunAsAdministrator(launchAtStartup);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, false, false)]
    public void Normalize_DisablesAdministratorElevation_WhenLaunchAtStartupIsDisabled(
        bool launchAtStartup,
        bool runAsAdministrator,
        bool expectedLaunch,
        bool expectedAdmin)
    {
        var (actualLaunch, actualAdmin) = StartupRegistrationRules.Normalize(
            launchAtStartup,
            runAsAdministrator);

        Assert.Equal(expectedLaunch, actualLaunch);
        Assert.Equal(expectedAdmin, actualAdmin);
    }
}
