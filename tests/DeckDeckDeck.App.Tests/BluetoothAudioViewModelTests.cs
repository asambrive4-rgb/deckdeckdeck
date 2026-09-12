// 역할: 블루투스 장치 목록 뷰모델이 기기 상태 변경을 화면에 올바르게 반영하는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;

public sealed class BluetoothAudioViewModelTests
{
    [Fact]
    public void InitialPendingQuery_ShowsLoadingState()
    {
        var services = CreateServices();
        var pending = new TaskCompletionSource<BluetoothAudioStatusSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubBluetoothAudioStatusGateway(_ => pending.Task);
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        Assert.Equal(BluetoothAudioStatusRules.LoadingText, viewModel.TopBarStatusMessage);
        Assert.Equal(BluetoothAudioStatusRules.LoadingToolTip, viewModel.TopBarStatusToolTip);

        pending.TrySetCanceled();
    }

    [Fact]
    public async Task ConnectedDeviceWithoutBattery_ShowsNameAndUnavailableTooltip()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(true, "Buds3 Pro", null));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        await viewModel.RefreshBluetoothAudioStatusAsync();

        Assert.Equal("Buds3 Pro", viewModel.TopBarStatusMessage);
        Assert.Equal(
            $"Buds3 Pro\n{BluetoothAudioStatusRules.BatteryUnavailableToolTip}",
            viewModel.TopBarStatusToolTip);
        Assert.Equal("Buds3 Pro", viewModel.TopBarDeviceName);
        Assert.False(viewModel.HasTopBarBattery);
        Assert.Empty(viewModel.TopBarBatteryText);
        Assert.False(viewModel.IsTopBarBatteryLow);
    }

    [Fact]
    public async Task ConnectedKeyboardWithBattery_PopulatesBatteryProperties()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(true, "Smart KBD Trio 500", 100, BluetoothDeviceCategory.Input));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        await viewModel.RefreshBluetoothAudioStatusAsync();

        Assert.Equal("Smart KBD Trio 500", viewModel.TopBarDeviceName);
        Assert.Equal(100, viewModel.TopBarBatteryPercent);
        Assert.Equal("100%", viewModel.TopBarBatteryText);
        Assert.True(viewModel.HasTopBarBattery);
        Assert.False(viewModel.IsTopBarBatteryLow);
        Assert.Equal(BluetoothDeviceCategory.Input, viewModel.TopBarDeviceCategory);
    }

    [Fact]
    public async Task LowBatteryDevice_SetsIsTopBarBatteryLow()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(true, "Buds3 Pro", 15, BluetoothDeviceCategory.Audio));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        await viewModel.RefreshBluetoothAudioStatusAsync();

        Assert.True(viewModel.HasTopBarBattery);
        Assert.True(viewModel.IsTopBarBatteryLow);
        Assert.Equal("15%", viewModel.TopBarBatteryText);
    }

    [Fact]
    public async Task LateResultFromCanceledRequest_DoesNotOverwriteNewDevice()
    {
        var services = CreateServices();
        var firstResult = new TaskCompletionSource<BluetoothAudioStatusSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var query = 0;
        var gateway = new StubBluetoothAudioStatusGateway(_ =>
            Interlocked.Increment(ref query) == 1
                ? firstResult.Task
                : Task.FromResult(new BluetoothAudioStatusSnapshot(true, "New Device", 80)));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        await viewModel.RefreshBluetoothAudioStatusAsync();
        firstResult.SetResult(new BluetoothAudioStatusSnapshot(true, "Old Device", 10));
        await Task.Yield();

        Assert.Equal("New Device · 80%", viewModel.TopBarStatusMessage);
    }

    [Fact]
    public void Dispose_StopsStatusGateway()
    {
        var services = CreateServices();
        var gateway = new StubBluetoothAudioStatusGateway();
        var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        viewModel.Dispose();

        Assert.True(gateway.IsMonitoring);
        Assert.True(gateway.IsDisposed);
    }
}
