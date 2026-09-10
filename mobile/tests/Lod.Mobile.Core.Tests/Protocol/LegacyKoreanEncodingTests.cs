using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Tests.Protocol;

public sealed class LegacyKoreanEncodingTests
{
    [Fact]
    public void StringA_uses_CP949_byte_length_for_Korean_text()
    {
        byte[] encoded = LegacyKoreanEncoding.EncodeStringA("가A");

        Assert.Equal(Convert.FromHexString("03B0A141"), encoded);
        Assert.Equal("가A", LegacyKoreanEncoding.DecodeStringA(encoded, out int consumed));
        Assert.Equal(encoded.Length, consumed);
    }

    [Fact]
    public void StringA_rejects_values_larger_than_one_byte_length()
    {
        string oversized = new('가', 128);

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
            () => LegacyKoreanEncoding.EncodeStringA(oversized));

        Assert.DoesNotContain(oversized, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringA_rejects_truncated_input()
    {
        Assert.Throws<ProtocolException>(
            () => LegacyKoreanEncoding.DecodeStringA(Convert.FromHexString("0341"), out _));
    }
}
