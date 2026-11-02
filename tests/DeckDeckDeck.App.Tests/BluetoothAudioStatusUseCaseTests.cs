// 역할: 블루투스 오디오 상태 조회 및 프레젠테이션 변환, 선호 기기 저장 유스케이스를 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class BluetoothAudioStatusUseCaseTests
{
    [Fact]
    public async Task GetPresentationAsync_WhenConnected_ReturnsConnectedPresentation()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(true, "Buds3 Pro", 85, BluetoothDeviceCategory.Audio));
        using var useCase = new BluetoothAudioStatusUseCase(gateway, services.SettingsRepository);

        var presentation = await useCase.GetPresentationAsync(null);

        Assert.True(presentation.IsConnected);
        Assert.Equal("Buds3 Pro", presentation.DeviceName);
        Assert.Equal(85, presentation.BatteryPercent);
        Assert.Equal("85%", presentation.BatteryText);
        Assert.True(presentation.HasBattery);
        Assert.False(presentation.IsBatteryLow);
        Assert.Equal(BluetoothDeviceCategory.Audio, presentation.DeviceCategory);
    }

    [Fact]
    public async Task GetPresentationAsync_WhenDisconnected_ReturnsDisconnectedPresentation()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(false, null, null));
        using var useCase = new BluetoothAudioStatusUseCase(gateway, services.SettingsRepository);

        var presentation = await useCase.GetPresentationAsync(null);

        Assert.False(presentation.IsConnected);
        Assert.Equal(BluetoothAudioStatusRules.DisconnectedText, presentation.DeviceName);
        Assert.Null(presentation.BatteryPercent);
        Assert.False(presentation.HasBattery);
    }

    [Fact]
    public async Task GetPresentationAsync_WhenDisconnectedWithPreferredDevice_ReturnsPinnedPresentation()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(false, "My Speaker", null, BluetoothDeviceCategory.Audio));
        using var useCase = new BluetoothAudioStatusUseCase(gateway, services.SettingsRepository);

        var presentation = await useCase.GetPresentationAsync("My Speaker");

        Assert.False(presentation.IsConnected);
        Assert.Equal("My Speaker", presentation.DeviceName);
        Assert.Contains(BluetoothAudioStatusRules.DisconnectedTagText, presentation.StatusMessage);
    }

    [Fact]
    public void SetPreferredDeviceName_SavesToSettingsRepository()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway();
        using var useCase = new BluetoothAudioStatusUseCase(gateway, services.SettingsRepository);

        useCase.SetPreferredDeviceName("Galaxy Buds");

        Assert.Equal("Galaxy Buds", useCase.GetPreferredDeviceName());
        Assert.Equal("Galaxy Buds", services.SettingsRepository.Load().PreferredBluetoothDeviceName);

        useCase.SetPreferredDeviceName(null);
        Assert.Null(useCase.GetPreferredDeviceName());
    }

    [Fact]
    public void StatusInvalidated_RelaysFromGateway()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway();
        using var useCase = new BluetoothAudioStatusUseCase(gateway, services.SettingsRepository);

        var eventRaised = false;
        useCase.StatusInvalidated += (_, _) => eventRaised = true;

        gateway.Invalidate();

        Assert.True(eventRaised);
    }
}
