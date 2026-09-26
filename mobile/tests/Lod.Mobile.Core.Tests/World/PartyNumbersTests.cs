using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>파티원·봇 칸의 게이지 안 숫자(<see cref="PartyNumbers" />) — 0x5E 종류 6·4 끝의 꼬리, 옛 몸도 읽힌다.</summary>
public sealed class PartyNumbersTests
{
    private static readonly byte[] Numbers = [0, 0, 1, 194, 0, 0, 2, 18, 0, 0, 0, 90, 0, 0, 0, 120]; // 450 · 530 · 90 · 120

    [Fact]
    public void Member_numbers_follow_the_name()
    {
        byte[] body = [6, 0, 0, 0, 9, 85, 75, 1, 0, 11, .. LegacyKoreanEncoding.EncodeStringA("동료"), .. Numbers];

        Assert.Equal(new VitalNumbers(450, 530, 90, 120), PartyNumbers.ReadMember(body));

        // 앞부분은 전과 같다 — 옛 읽기(Companion.ReadMember)가 그대로 읽는다.
        PartyMemberStatus member = Companion.ReadMember(body);
        Assert.Equal((85, 75, "동료"), (member.HealthPercent, member.ManaPercent, member.Name));
    }

    [Fact]
    public void Old_member_body_has_no_numbers()
    {
        Assert.Null(PartyNumbers.ReadMember([6, 0, 0, 0, 9, 55, 80, 0, .. LegacyKoreanEncoding.EncodeStringA("동료")]));
        Assert.Null(PartyNumbers.ReadMember([6, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Life_numbers_follow_the_percentages()
    {
        Assert.Equal(new VitalNumbers(450, 530, 90, 120), PartyNumbers.ReadLife([4, 0, 0, 0, 9, 85, 75, .. Numbers]));
        Assert.Equal(new CompanionLife(9, 85, 75), Companion.ReadLife([4, 0, 0, 0, 9, 85, 75, .. Numbers]));
        Assert.Null(PartyNumbers.ReadLife([4, 0, 0, 0, 9, 80, 35]));
    }

    [Theory]
    [InlineData(450, 530, 85, "450/530")]
    [InlineData(99999, 99999, 100, "99.9k/99.9k")]
    [InlineData(null, null, 85, "85%")]
    [InlineData(null, null, null, "")]
    [InlineData(10, 0, 40, "40%")]
    public void Gauge_text_prefers_numbers_then_percent(int? left, int? most, int? percent, string text)
    {
        Assert.Equal(text, PartyNumbers.Text(left, most, percent));
    }

    [Fact]
    public void Wide_gauge_keeps_the_whole_number()
    {
        Assert.Equal("99999/99999", PartyNumbers.Text(99999, 99999, 100, compact: false));
    }
}
