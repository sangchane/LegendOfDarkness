using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Tests.Protocol;

public sealed class PacketFrameCodecTests
{
    [Fact]
    public void Decode_returns_incomplete_until_the_whole_frame_arrives()
    {
        byte[] bytes = Convert.FromHexString("AA00047E1B4F4B");

        FrameReadStatus headerStatus = PacketFrameCodec.TryDecode(bytes.AsSpan(0, 2), out _, out _);
        FrameReadStatus bodyStatus = PacketFrameCodec.TryDecode(bytes.AsSpan(0, 6), out _, out _);
        FrameReadStatus completeStatus = PacketFrameCodec.TryDecode(bytes, out PacketFrame? frame, out int consumed);

        Assert.Equal(FrameReadStatus.Incomplete, headerStatus);
        Assert.Equal(FrameReadStatus.Incomplete, bodyStatus);
        Assert.Equal(FrameReadStatus.Complete, completeStatus);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal((byte)0x7E, frame!.Command);
        Assert.Equal(Convert.FromHexString("1B4F4B"), frame.Data.ToArray());
    }

    [Fact]
    public void Decode_consumes_one_frame_from_coalesced_input()
    {
        byte[] first = Convert.FromHexString("AA00027E1B");
        byte[] second = Convert.FromHexString("AA00020000");
        byte[] combined = [.. first, .. second];

        FrameReadStatus status = PacketFrameCodec.TryDecode(combined, out PacketFrame? frame, out int consumed);

        Assert.Equal(FrameReadStatus.Complete, status);
        Assert.Equal(first.Length, consumed);
        Assert.Equal((byte)0x7E, frame!.Command);
    }

    [Theory]
    [InlineData("AB000100")]
    [InlineData("AA0000")]
    [InlineData("AAFFFF")]
    public void Decode_rejects_invalid_headers(string hex)
    {
        FrameReadStatus status = PacketFrameCodec.TryDecode(Convert.FromHexString(hex), out _, out _);

        Assert.Equal(FrameReadStatus.Invalid, status);
    }

    [Fact]
    public void Encode_copies_payload_into_a_framed_packet()
    {
        byte[] payload = Convert.FromHexString("0002CE4C4B");

        byte[] encoded = PacketFrameCodec.Encode(payload);
        payload[0] = 0xFF;

        Assert.Equal(Convert.FromHexString("AA00050002CE4C4B"), encoded);
    }
}
