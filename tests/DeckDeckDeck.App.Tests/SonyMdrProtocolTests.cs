// 역할: 소니 헤드폰 전용 통신 패킷 생성·해석·배터리 추출 및 배터리 리더 캐시 정책을 검증합니다.
using DeckDeckDeck.App.Infrastructure.Platform;

namespace DeckDeckDeck.App.Tests;

public sealed class SonyMdrProtocolTests
{
    [Fact]
    public async Task ReadBatteryAsync_WhenSuccessful_CachesFor60Seconds()
    {
        var currentTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var queryCount = 0;
        var reader = new WindowsSonyBatteryReader(
            (addr, ct) =>
            {
                queryCount++;
                return Task.FromResult<int?>(85);
            },
            () => currentTime);

        var first = await reader.ReadBatteryAsync(0x112233445566, CancellationToken.None);
        Assert.Equal(85, first);
        Assert.Equal(1, queryCount);

        currentTime = currentTime.AddSeconds(59);
        var cached = await reader.ReadBatteryAsync(0x112233445566, CancellationToken.None);
        Assert.Equal(85, cached);
        Assert.Equal(1, queryCount);

        currentTime = currentTime.AddSeconds(2); // total 61s
        var refreshed = await reader.ReadBatteryAsync(0x112233445566, CancellationToken.None);
        Assert.Equal(85, refreshed);
        Assert.Equal(2, queryCount);
    }

    [Fact]
    public async Task ReadBatteryAsync_WhenFailed_CachesFor5Minutes()
    {
        var currentTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var queryCount = 0;
        var reader = new WindowsSonyBatteryReader(
            (addr, ct) =>
            {
                queryCount++;
                return Task.FromResult<int?>(null);
            },
            () => currentTime);

        var first = await reader.ReadBatteryAsync(0x112233445566, CancellationToken.None);
        Assert.Null(first);
        Assert.Equal(1, queryCount);

        currentTime = currentTime.AddMinutes(4).AddSeconds(59);
        var cached = await reader.ReadBatteryAsync(0x112233445566, CancellationToken.None);
        Assert.Null(cached);
        Assert.Equal(1, queryCount);

        currentTime = currentTime.AddSeconds(2); // total 5m 1s
        var refreshed = await reader.ReadBatteryAsync(0x112233445566, CancellationToken.None);
        Assert.Null(refreshed);
        Assert.Equal(2, queryCount);
    }
    [Fact]
    public void BuildFrame_BatteryRequest_HasExpectedBytes()
    {
        var frame = SonyMdrProtocol.BuildFrame(0x0C, 0, SonyMdrProtocol.BatteryRequestPayload);

        Assert.Equal(Convert.FromHexString("3E0C0000000002220030" + "3C"), frame);
    }

    [Fact]
    public void Feed_RealBatteryResponseSplitAcrossReads_ReturnsBattery95()
    {
        var bytes = Convert.FromHexString("3E0C010000000423005F00933C");
        var accumulator = new SonyMdrProtocol.FrameAccumulator();

        var first = accumulator.Feed(bytes.AsSpan(0, 5));
        var second = accumulator.Feed(bytes.AsSpan(5));

        Assert.Empty(first);
        var frame = Assert.Single(second);
        Assert.Equal(0x0C, frame.DataType);
        Assert.True(SonyMdrProtocol.TryParseBattery(frame.Payload, out var battery));
        Assert.Equal(95, battery);
    }

    [Fact]
    public void Feed_CorruptChecksum_IsIgnored()
    {
        var accumulator = new SonyMdrProtocol.FrameAccumulator();

        var frames = accumulator.Feed(Convert.FromHexString("3E0C010000000423005F00943C"));

        Assert.Empty(frames);
    }

    [Fact]
    public void BuildFrame_EscapesControlBytesAndRoundTrips()
    {
        var frame = SonyMdrProtocol.BuildFrame(0x0C, 1, [0x3C, 0x3D, 0x3E]);
        var accumulator = new SonyMdrProtocol.FrameAccumulator();

        var decoded = Assert.Single(accumulator.Feed(frame));

        Assert.Equal<byte>([0x3C, 0x3D, 0x3E], decoded.Payload);
    }

    [Fact]
    public void BuildAck_FlipsSequence()
    {
        Assert.Equal(Convert.FromHexString("3E010100000000023C"), SonyMdrProtocol.BuildAck(0));
    }

    [Theory]
    [InlineData(new byte[] { 0x23, 0x00, 101, 0 })]
    [InlineData(new byte[] { 0x23, 0x01, 50, 0 })]
    [InlineData(new byte[] { 0x23, 0x00 })]
    public void TryParseBattery_InvalidPayload_ReturnsFalse(byte[] payload)
    {
        Assert.False(SonyMdrProtocol.TryParseBattery(payload, out _));
    }
}
