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
