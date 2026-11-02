// 역할: 소니 헤드폰 전용 통신(MDR)의 패킷을 만들고 해석하며, 배터리 응답에서 잔량(%)을 꺼냅니다.
namespace DeckDeckDeck.App.Infrastructure.Platform;

internal sealed record SonyMdrFrame(byte DataType, byte Sequence, byte[] Payload);

internal static class SonyMdrProtocol
{
    public const byte DataTypeAck = 0x01;
    public const byte DataTypeMdr = 0x0C;

    private const byte FrameStart = 0x3E;
    private const byte FrameEnd = 0x3C;
    private const byte EscapeMarker = 0x3D;
    private const byte PowerGetStatus = 0x22;
    private const byte PowerReturnStatus = 0x23;
    private const byte PowerInquiredBattery = 0x00;

    public static readonly byte[] BatteryRequestPayload = [PowerGetStatus, PowerInquiredBattery];

    public static byte[] BuildFrame(byte dataType, byte sequence, ReadOnlySpan<byte> payload)
    {
        var inner = new List<byte> { dataType, sequence, 0, 0, 0, (byte)payload.Length };
        inner.AddRange(payload.ToArray());
        inner.Add((byte)inner.Sum(value => value));

        var frame = new List<byte> { FrameStart };
        foreach (var value in inner)
        {
            if (value is FrameEnd or EscapeMarker or FrameStart)
            {
                frame.Add(EscapeMarker);
                frame.Add((byte)(value & 0xEF));
                continue;
            }

            frame.Add(value);
        }

        frame.Add(FrameEnd);
        return [.. frame];
    }

    public static byte[] BuildAck(byte receivedSequence) =>
        BuildFrame(DataTypeAck, (byte)(1 - receivedSequence), []);

    public static bool TryParseBattery(byte[] payload, out int batteryPercent)
    {
        batteryPercent = 0;
        if (payload.Length < 3 || payload[0] != PowerReturnStatus || payload[1] != PowerInquiredBattery)
        {
            return false;
        }

        if (payload[2] > 100)
        {
            return false;
        }

        batteryPercent = payload[2];
        return true;
    }

    /// <summary>
    /// 조각나서 들어오는 바이트를 모아 완성된 패킷만 꺼내 줍니다.
    /// </summary>
    public sealed class FrameAccumulator
    {
        private readonly List<byte> _inner = [];
        private bool _inFrame;
        private bool _escaped;

        public IReadOnlyList<SonyMdrFrame> Feed(ReadOnlySpan<byte> bytes)
        {
            var frames = new List<SonyMdrFrame>();
            foreach (var value in bytes)
            {
                switch (value)
                {
                    case FrameStart:
                        _inner.Clear();
                        _inFrame = true;
                        _escaped = false;
                        break;
                    case FrameEnd when _inFrame:
                        _inFrame = false;
                        if (TryDecode(out var frame))
                        {
                            frames.Add(frame);
                        }

                        break;
                    case EscapeMarker when _inFrame:
                        _escaped = true;
                        break;
                    default:
                        if (!_inFrame)
                        {
                            break;
                        }

                        _inner.Add(_escaped ? (byte)(value | 0x10) : value);
                        _escaped = false;
                        break;
                }
            }

            return frames;
        }

        private bool TryDecode(out SonyMdrFrame frame)
        {
            frame = null!;
            if (_inner.Count < 7)
            {
                return false;
            }

            var checksum = (byte)_inner.Take(_inner.Count - 1).Sum(value => value);
            if (checksum != _inner[^1])
            {
                return false;
            }

            var length = (_inner[2] << 24) | (_inner[3] << 16) | (_inner[4] << 8) | _inner[5];
            if (length != _inner.Count - 7)
            {
                return false;
            }

            frame = new SonyMdrFrame(_inner[0], _inner[1], [.. _inner.Skip(6).Take(length)]);
            return true;
        }
    }
}
