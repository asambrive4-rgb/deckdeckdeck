// 역할: 블루투스 상태 표시, 기기 선택 드롭다운 메뉴, 수동 기기 고정 및 영속화가 올바르게 동작하는지 검증하는 단위 테스트 모음입니다.
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

    [Fact]
    public async Task ToggleBluetoothMenuCommand_TogglesMenuAndRefreshesDeviceList()
    {
        var services = CreateServices();
        var pairedDevices = new List<BluetoothDeviceItem>
        {
            new("WH-1000XM5", BluetoothDeviceCategory.Audio, true, 90),
            new("MX Master 3S", BluetoothDeviceCategory.Input, true, 80)
        };
        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(true, "WH-1000XM5", 90, BluetoothDeviceCategory.Audio),
            pairedDevices);
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        Assert.False(viewModel.IsBluetoothMenuOpen);

        // 첫 번째 클릭: 메뉴 열림 + 목록 갱신
        viewModel.ToggleBluetoothMenuCommand.Execute(null);
        Assert.True(viewModel.IsBluetoothMenuOpen);
        Assert.Equal(2, viewModel.BluetoothDevices.Count);
        Assert.Equal("WH-1000XM5", viewModel.BluetoothDevices[0].DeviceName);
        Assert.Equal("MX Master 3S", viewModel.BluetoothDevices[1].DeviceName);

        // 두 번째 클릭: 메뉴 닫힘
        viewModel.ToggleBluetoothMenuCommand.Execute(null);
        Assert.False(viewModel.IsBluetoothMenuOpen);
    }

    [Fact]
    public async Task SelectBluetoothDeviceCommand_SetsPreferredDevice_PersistsToSettings_AndRefreshesStatus()
    {
        var services = CreateServices();
        var selectedItem = new BluetoothDeviceItem("Sony WH-1000XM5", BluetoothDeviceCategory.Audio, true, 85);
        var gateway = new StubBluetoothAudioStatusGateway(
            (pref, _) => Task.FromResult(new BluetoothAudioStatusSnapshot(
                true,
                pref ?? "Auto Device",
                85,
                BluetoothDeviceCategory.Audio)));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        viewModel.ToggleBluetoothMenuCommand.Execute(null);
        Assert.True(viewModel.IsBluetoothMenuOpen);

        // 기기 선택
        viewModel.SelectBluetoothDeviceCommand.Execute(selectedItem.DeviceName);

        Assert.False(viewModel.IsBluetoothMenuOpen);
        Assert.Equal("Sony WH-1000XM5", viewModel.SelectedBluetoothDeviceName);
        Assert.False(viewModel.IsAutoDeviceSelected);

        // DB 영속화 확인
        var savedSettings = services.SettingsRepository.Load();
        Assert.Equal("Sony WH-1000XM5", savedSettings.PreferredBluetoothDeviceName);

        // 게이트웨이에 수동 지정 기기명이 전달되어 조회되었는지 확인
        await viewModel.RefreshBluetoothAudioStatusAsync();
        Assert.Equal("Sony WH-1000XM5", gateway.LastRequestedPreferredDeviceName);
        Assert.Equal("Sony WH-1000XM5", viewModel.TopBarDeviceName);
    }

    [Fact]
    public async Task SelectBluetoothDeviceCommand_WithNull_ResetsToAutoDetect_AndPersists()
    {
        var services = CreateServices();
        services.SettingsRepository.SetPreferredBluetoothDeviceName("Previous Pinned Device");

        var gateway = new StubBluetoothAudioStatusGateway(
            (pref, _) => Task.FromResult(new BluetoothAudioStatusSnapshot(
                true,
                pref ?? "Auto Detected Audio",
                95,
                BluetoothDeviceCategory.Audio)));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        Assert.Equal("Previous Pinned Device", viewModel.SelectedBluetoothDeviceName);
        Assert.False(viewModel.IsAutoDeviceSelected);

        // 자동 감지(null) 선택
        viewModel.SelectBluetoothDeviceCommand.Execute(null);

        Assert.Null(viewModel.SelectedBluetoothDeviceName);
        Assert.True(viewModel.IsAutoDeviceSelected);

        // DB 영속화 확인 (null 로 지워졌는지)
        var savedSettings = services.SettingsRepository.Load();
        Assert.Null(savedSettings.PreferredBluetoothDeviceName);

        // 게이트웨이에 null로 조회되었는지 확인
        await viewModel.RefreshBluetoothAudioStatusAsync();
        Assert.Null(gateway.LastRequestedPreferredDeviceName);
        Assert.Equal("Auto Detected Audio", viewModel.TopBarDeviceName);
    }

    [Fact]
    public async Task PinnedDeviceDisconnected_ShowsDisconnectedTagAndKeepsName()
    {
        var services = CreateServices();
        services.SettingsRepository.SetPreferredBluetoothDeviceName("AirPods Pro");

        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(
                false,
                "AirPods Pro",
                null,
                BluetoothDeviceCategory.Audio));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        await viewModel.RefreshBluetoothAudioStatusAsync();

        Assert.Equal("AirPods Pro", viewModel.TopBarDeviceName);
        Assert.False(viewModel.IsTopBarDeviceConnected);
        Assert.True(viewModel.ShowTopBarDisconnectedTag);
        Assert.False(viewModel.HasTopBarBattery);
        Assert.Contains(BluetoothAudioStatusRules.DisconnectedTagText, viewModel.TopBarStatusToolTip);
        Assert.Contains("AirPods Pro", viewModel.TopBarStatusToolTip);
    }

    [Fact]
    public async Task PinnedDeviceConnected_ShowsConnectedStateWithBattery()
    {
        var services = CreateServices();
        services.SettingsRepository.SetPreferredBluetoothDeviceName("AirPods Pro");

        var gateway = new StubBluetoothAudioStatusGateway(
            new BluetoothAudioStatusSnapshot(
                true,
                "AirPods Pro",
                75,
                BluetoothDeviceCategory.Audio));
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        await viewModel.RefreshBluetoothAudioStatusAsync();

        Assert.Equal("AirPods Pro", viewModel.TopBarDeviceName);
        Assert.True(viewModel.IsTopBarDeviceConnected);
        Assert.False(viewModel.ShowTopBarDisconnectedTag);
        Assert.True(viewModel.HasTopBarBattery);
        Assert.Equal(75, viewModel.TopBarBatteryPercent);
        Assert.Equal("75%", viewModel.TopBarBatteryText);
    }

    [Fact]
    public void SavedPreferredDevice_IsLoadedOnInitialization()
    {
        var services = CreateServices();
        services.SettingsRepository.SetPreferredBluetoothDeviceName("Galaxy Buds2 Pro");

        var gateway = new StubBluetoothAudioStatusGateway();
        using var viewModel = CreateMainViewModel(services, bluetoothAudioStatusGateway: gateway);

        Assert.Equal("Galaxy Buds2 Pro", viewModel.SelectedBluetoothDeviceName);
        Assert.False(viewModel.IsAutoDeviceSelected);
    }
}

