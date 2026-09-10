namespace Lod.Mobile.Core.Protocol;

/// <summary>
/// Cuts the byte stream into frames and builds them. A frame is <c>0xAA</c>, then the payload length as two
/// big-endian bytes, then the payload — whose first byte is the command.
/// </summary>
/// <remarks>
/// TCP hands over whatever has arrived, so a read may hold half a frame, one frame, or several. Decoding
/// answers with how many bytes it took rather than assuming it consumed everything.
/// </remarks>
public static class PacketFrameCodec
{
    /// <summary>Every frame starts with this byte. Anything else means the stream is not aligned.</summary>
    public const byte Magic = 0xAA;

    private const int HeaderLength = 3;

    /// <summary>
    /// Longest payload the server can hold. Its receive buffer is 65534 bytes, so a longer length is not a
    /// frame we are waiting for — it is a frame that will never arrive.
    /// </summary>
    public const int MaximumPayloadLength = 65534;

    public static FrameReadStatus TryDecode(ReadOnlySpan<byte> input, out PacketFrame? frame, out int consumed)
    {
        frame = null;
        consumed = 0;

        if (input.Length == 0)
        {
            return FrameReadStatus.Incomplete;
        }

        if (input[0] != Magic)
        {
            return FrameReadStatus.Invalid;
        }

        if (input.Length < HeaderLength)
        {
            return FrameReadStatus.Incomplete;
        }

        int length = (input[1] << 8) | input[2];

        // A frame carries at least its command byte, and cannot be longer than the far side can hold.
        if (length == 0 || length > MaximumPayloadLength)
        {
            return FrameReadStatus.Invalid;
        }

        if (input.Length < HeaderLength + length)
        {
            return FrameReadStatus.Incomplete;
        }

        ReadOnlySpan<byte> payload = input.Slice(HeaderLength, length);

        frame = new PacketFrame(payload[0], payload[1..].ToArray());
        consumed = HeaderLength + length;

        return FrameReadStatus.Complete;
    }

    /// <summary>Wraps a payload in a frame. The payload is copied, so the caller may reuse its buffer.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is 0 or > MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                payload.Length,
                $"프레임 본문은 1바이트 이상 {MaximumPayloadLength}바이트 이하여야 합니다.");
        }

        byte[] frame = new byte[HeaderLength + payload.Length];

        frame[0] = Magic;
        frame[1] = (byte)(payload.Length >> 8);
        frame[2] = (byte)payload.Length;
        payload.CopyTo(frame.AsSpan(HeaderLength));

        return frame;
    }
}
