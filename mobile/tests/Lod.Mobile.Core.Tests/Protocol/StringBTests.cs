using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Tests.Protocol;

/// <summary>
/// What the server says in words comes with two bytes of length rather than one, so a line longer than a
/// couple of hundred characters still fits. Reading it as the shorter kind loses the text and everything
/// after it.
/// </summary>
public sealed class StringBTests
{
    [Fact]
    public void A_line_is_read_with_its_two_byte_length()
    {
        byte[] said = [0x00, 0x05, .. "Hello"u8];

        Assert.Equal("Hello", LegacyKoreanEncoding.DecodeStringB(said, out int consumed));
        Assert.Equal(7, consumed);
    }

    /// <summary>The server writes Korean in the old code page, the same as everywhere else it speaks.</summary>
    [Fact]
    public void Korean_comes_back_as_Korean()
    {
        byte[] letters = LegacyKoreanEncoding.Encoding.GetBytes("갈 수 없습니다");
        byte[] said = [(byte)(letters.Length >> 8), (byte)letters.Length, .. letters];

        Assert.Equal("갈 수 없습니다", LegacyKoreanEncoding.DecodeStringB(said, out _));
    }

    [Fact]
    public void A_line_that_stops_short_is_refused()
    {
        Assert.Throws<ProtocolException>(() => LegacyKoreanEncoding.DecodeStringB([0x00], out _));
        Assert.Throws<ProtocolException>(() => LegacyKoreanEncoding.DecodeStringB([0x00, 0x09, 0x41], out _));
    }
}
