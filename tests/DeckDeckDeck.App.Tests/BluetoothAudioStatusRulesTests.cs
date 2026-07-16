using DeckDeckDeck.App.Domain;

namespace DeckDeckDeck.App.Tests;

public sealed class BluetoothAudioStatusRulesTests
{
    [Fact]
    public void FormatDisplayText_WhenNoDevice_ReturnsDisconnected()
    {
        Assert.Equal(
            BluetoothAudioStatusRules.DisconnectedText,
            BluetoothAudioStatusRules.FormatDisplayText(null, 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(69)]
    [InlineData(100)]
    public void FormatDisplayText_WithValidBattery_ShowsPercent(int battery)
    {
        Assert.Equal(
            $"Buds3 Pro · {battery}%",
            BluetoothAudioStatusRules.FormatDisplayText("Buds3 Pro", battery));
    }

    [Fact]
    public void FormatToolTip_WithoutBattery_ExplainsUnavailableState()
    {
        Assert.Equal(
            $"WH-1000XM5\n{BluetoothAudioStatusRules.BatteryUnavailableToolTip}",
            BluetoothAudioStatusRules.FormatToolTip("WH-1000XM5", null));
    }

    [Theory]
    [InlineData("Headphones (Galaxy Buds)", "Galaxy Buds")]
    [InlineData("Headset (AirPods Pro)", "AirPods Pro")]
    [InlineData("헤드폰(주상의 Buds3 Pro)", "주상의 Buds3 Pro")]
    [InlineData("WH-1000XM5 Hands-Free AG Audio", "WH-1000XM5")]
    [InlineData("Device Stereo", "Device")]
    public void CleanDeviceName_StripsAudioWrappers(string raw, string expected)
    {
        Assert.Equal(expected, BluetoothAudioStatusRules.CleanDeviceName(raw));
    }

    [Theory]
    [InlineData(@"BTHENUM\DEV_E4928282D59B\9&8AE243B&0&BLUETOOTHDEVICE_E4928282D59B", true)]
    [InlineData(@"BTHLE\DEV_78EEEF7ACB2E\9&2D93A4DA&0&78EEEF7ACB2E", true)]
    [InlineData(@"SWD\MMDEVAPI\{0.0.0.00000000}.{C18AA952-BDF1-48A3-A3A5-FCA71D54E951}", false)]
    [InlineData(@"USB\VID_8087&PID_0032\7&177D1E2B&0&1", false)]
    public void LooksLikeBluetoothPeripheralId_UsesBusIdentity(string instanceId, bool expected)
    {
        Assert.Equal(expected, BluetoothAudioStatusRules.LooksLikeBluetoothPeripheralId(instanceId));
    }

    [Fact]
    public void SelectBestDeviceGroup_ContainerMatchWinsOverName()
    {
        var selectedContainer = Guid.NewGuid();
        var context = Context("Same Name", [selectedContainer]);
        BluetoothDeviceMatchCandidate[] candidates =
        [
            Candidate("other", "Same Name", Guid.NewGuid()),
            Candidate("selected", "Different Name", selectedContainer)
        ];

        var result = BluetoothAudioStatusRules.SelectBestDeviceGroup(context, candidates);

        Assert.Single(result);
        Assert.Equal("selected", result[0].InstanceId);
    }

    [Fact]
    public void SelectBestDeviceGroup_SameContainerGroupsClassicAndLeNodes()
    {
        var container = Guid.NewGuid();
        var context = Context("Buds3 Pro", [container]);
        BluetoothDeviceMatchCandidate[] candidates =
        [
            Candidate("classic", "Buds3 Pro", container, "E4928282D59B"),
            Candidate("le", "Buds3 Pro", container, "78EEEF7ACB2E")
        ];

        var result = BluetoothAudioStatusRules.SelectBestDeviceGroup(context, candidates);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SelectBestDeviceGroup_AddressMatchFindsPhysicalGroup()
    {
        var context = new BluetoothDeviceMatchContext(
            "Wrapped Device",
            [],
            Set("AABBCCDDEEFF"));
        var candidate = Candidate("bt", "Different Name", null, "AABBCCDDEEFF");

        var result = BluetoothAudioStatusRules.SelectBestDeviceGroup(context, [candidate]);

        Assert.Single(result);
        Assert.Equal("bt", result[0].InstanceId);
    }

    [Fact]
    public void SelectBestDeviceGroup_DuplicateExactNamesAcrossDevicesAreAmbiguous()
    {
        var context = Context("Shared Headset", []);
        BluetoothDeviceMatchCandidate[] candidates =
        [
            Candidate("first", "Shared Headset", Guid.NewGuid()),
            Candidate("second", "Shared Headset", Guid.NewGuid())
        ];

        var result = BluetoothAudioStatusRules.SelectBestDeviceGroup(context, candidates);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectBestDeviceGroup_HdmiNameDoesNotPartiallyMatchBluetoothDevice()
    {
        var context = Context("2 - IP2426 (AMD High Definition Audio Device)", []);
        var candidate = Candidate("bt", "IP2426 Headset", Guid.NewGuid());

        Assert.Empty(BluetoothAudioStatusRules.SelectBestDeviceGroup(context, [candidate]));
    }

    private static BluetoothDeviceMatchContext Context(string name, IReadOnlyList<Guid> containers)
    {
        return new BluetoothDeviceMatchContext(name, containers, Set());
    }

    private static BluetoothDeviceMatchCandidate Candidate(
        string instanceId,
        string name,
        Guid? container,
        params string[] addresses)
    {
        return new BluetoothDeviceMatchCandidate(instanceId, name, container, Set(addresses));
    }

    private static IReadOnlySet<string> Set(params string[] values)
    {
        return values.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
