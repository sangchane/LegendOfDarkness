using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 경험치 게이지(사용자 요청 2026-09-26: "게이지로 하고 필요한 경험치 표기") — 서버는 다음 레벨까지 남은 양만 보내므로,
/// 그 레벨에 드는 양은 서버와 같은 원작 표(<see cref="ExperienceGauge" />)에서 읽는다.
/// </summary>
public sealed class ExperienceGaugeTests
{
    [Theory]
    [InlineData(1, 600)]
    [InlineData(2, 2_400)]
    [InlineData(9, 50_058)]
    [InlineData(49, 1_484_872)]
    [InlineData(98, 8_990_567)]
    public void The_next_level_asks_what_the_original_table_asks(int level, long need)
    {
        Assert.Equal(need, ExperienceGauge.Of(level, need)!.Need);
    }

    [Fact]
    public void What_is_earned_is_the_need_less_what_is_left()
    {
        ExperienceGauge gauge = ExperienceGauge.Of(2, 1_800)!;

        Assert.Equal(600, gauge.Earned);
        Assert.Equal(2_400, gauge.Need);
    }

    [Fact]
    public void Nothing_left_is_a_full_gauge_and_more_left_than_the_need_is_an_empty_one()
    {
        Assert.Equal(2_400, ExperienceGauge.Of(2, 0)!.Earned);
        Assert.Equal(0, ExperienceGauge.Of(2, 9_999)!.Earned);
    }

    [Fact]
    public void At_the_top_there_is_no_next_level()
    {
        Assert.Null(ExperienceGauge.Of(99, 0));
        Assert.Null(ExperienceGauge.Of(0, 600));
    }

    /// <summary>백만 단위가 게이지 옆 칸을 넘지 않게 k·M 으로 줄인다(사용자 2026-09-26). 버림 — 모자란 것을 채운 것처럼 보이지 않게.</summary>
    [Theory]
    [InlineData(0, "0")]
    [InlineData(600, "600")]
    [InlineData(2_400, "2.4k")]
    [InlineData(50_058, "50k")]
    [InlineData(121_128, "121k")]
    [InlineData(999_999, "999k")]
    [InlineData(1_484_872, "1.48M")]
    [InlineData(8_990_567, "8.99M")]
    [InlineData(12_345_678, "12.3M")]
    public void Big_numbers_are_shortened(long value, string shown)
    {
        Assert.Equal(shown, ExperienceGauge.Short(value));
    }
}
