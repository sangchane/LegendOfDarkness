using System.Text;

namespace Lod.Mobile.Core.Protocol;

/// <summary>
/// Text as the 2007 client writes it. Korean goes over the wire in CP949, two bytes a syllable, and a
/// StringA is one length byte followed by that many bytes — so the length counts bytes, never characters.
/// </summary>
public static class LegacyKoreanEncoding
{
    private const int KoreanCodePage = 949;

    private static readonly Encoding Cp949 = Create();

    /// <summary>The codepage itself, for callers that write longer runs of text.</summary>
    public static Encoding Encoding => Cp949;

    public static byte[] EncodeStringA(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        byte[] bytes = Cp949.GetBytes(value);

        if (bytes.Length > byte.MaxValue)
        {
            // The length, not the text: this path carries names and passwords.
            throw new ArgumentOutOfRangeException(
                nameof(value),
                bytes.Length,
                $"StringA 는 CP949 로 {byte.MaxValue}바이트를 넘을 수 없습니다.");
        }

        byte[] encoded = new byte[1 + bytes.Length];

        encoded[0] = (byte)bytes.Length;
        bytes.CopyTo(encoded, 1);

        return encoded;
    }

    public static string DecodeStringA(ReadOnlySpan<byte> input, out int consumed)
    {
        if (input.Length == 0)
        {
            throw new ProtocolException("StringA 의 길이 바이트가 없습니다.");
        }

        int length = input[0];

        if (input.Length < 1 + length)
        {
            throw new ProtocolException(
                $"StringA 가 {length}바이트를 예고했는데 {input.Length - 1}바이트만 있습니다.");
        }

        consumed = 1 + length;

        return Cp949.GetString(input.Slice(1, length));
    }

    private static Encoding Create()
    {
        // .NET Core ships only Unicode codepages; CP949 comes from this provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return Encoding.GetEncoding(KoreanCodePage);
    }
}
