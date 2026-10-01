using System.Globalization;
using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>금화는 만·억으로 줄이되 올려 적지 않는다 — 9,999 는 그대로, 19,999 는 1.9만(2만이 아니다).</summary>
public sealed class GoldFormatTests
{
    [Theory]
    [InlineData(9_999L, "9,999")]
    [InlineData(10_000L, "1만")]
    [InlineData(19_999L, "1.9만")]
    [InlineData(123_456_789L, "1.2억")]
    public void Shortens_without_rounding_up(long gold, string expected)
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        try
        {
            Assert.Equal(expected, GoldFormat.Short(gold));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
