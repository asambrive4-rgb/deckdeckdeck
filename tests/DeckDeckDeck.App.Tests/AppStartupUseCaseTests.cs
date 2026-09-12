// 역할: 프로그램 시작 시 중복 실행 방지 및 초기화 작업이 올바르게 수행되는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Tests;

public sealed class AppStartupUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsRunPrimaryForFirstInstance()
    {
        var coordinator = new RecordingAppInstanceCoordinator
        {
            TryBecomePrimaryResult = true
        };
        var useCase = new AppStartupUseCase(coordinator);

        var decision = await useCase.ExecuteAsync(AppStartupRequest.Default);

        Assert.Equal(AppStartupDecisionKind.RunPrimary, decision.Kind);
        Assert.Equal(1, coordinator.TryBecomePrimaryCount);
        Assert.Equal(0, coordinator.RequestShowPrimaryCount);
    }

    [Fact]
    public async Task ExecuteAsyncForwardsToPrimaryAndExitsForSecondInstance()
    {
        var coordinator = new RecordingAppInstanceCoordinator
        {
            TryBecomePrimaryResult = false,
            RequestShowPrimaryResult = true
        };
        var useCase = new AppStartupUseCase(coordinator);

        var decision = await useCase.ExecuteAsync(AppStartupRequest.Default);

        Assert.Equal(AppStartupDecisionKind.ForwardedToPrimaryAndExit, decision.Kind);
        Assert.Equal(1, coordinator.TryBecomePrimaryCount);
        Assert.Equal(1, coordinator.RequestShowPrimaryCount);
    }

    [Fact]
    public async Task ExecuteAsyncExitsWithoutPrimaryStartupWhenForwardingFails()
    {
        var coordinator = new RecordingAppInstanceCoordinator
        {
            TryBecomePrimaryResult = false,
            RequestShowPrimaryResult = false
        };
        var useCase = new AppStartupUseCase(coordinator);

        var decision = await useCase.ExecuteAsync(AppStartupRequest.Default);

        Assert.Equal(AppStartupDecisionKind.FailedButExit, decision.Kind);
        Assert.Equal(1, coordinator.TryBecomePrimaryCount);
        Assert.Equal(1, coordinator.RequestShowPrimaryCount);
        Assert.False(string.IsNullOrWhiteSpace(decision.Message));
    }

    private sealed class RecordingAppInstanceCoordinator : IAppInstanceCoordinator
    {
        public bool TryBecomePrimaryResult { get; set; }

        public bool RequestShowPrimaryResult { get; set; }

        public int TryBecomePrimaryCount { get; private set; }

        public int RequestShowPrimaryCount { get; private set; }

        public bool TryBecomePrimary()
        {
            TryBecomePrimaryCount++;
            return TryBecomePrimaryResult;
        }

        public Task<bool> RequestShowPrimaryAsync(CancellationToken cancellationToken)
        {
            RequestShowPrimaryCount++;
            return Task.FromResult(RequestShowPrimaryResult);
        }

        public void StartListening(Action onShowRequested)
        {
        }

        public void Dispose()
        {
        }
    }
}
