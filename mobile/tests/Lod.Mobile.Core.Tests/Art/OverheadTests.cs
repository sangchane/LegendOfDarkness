using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// What stands over a head, bottom to top: the head slot for brief head effects, the health bar, one badge row
/// (user + lead decision 2026-09-24). Drawn bottoms below are measured from the cut sheets in
/// <c>mobile/client/assets/effect</c> — the lowest row with anything on it in any frame.
/// </summary>
public sealed class OverheadTests
{
    private static readonly EffectSheet Small = new(4, 111, 85, 55, 70, []);
    private static readonly EffectSheet Tall = new(4, 111, 111, 55, 97, []);

    [Theory]
    [InlineData(24, 25)] // 혼수
    [InlineData(42, 23)] // 일음지 회색 소용돌이
    [InlineData(33, 23)] // Miss
    [InlineData(115, 23)] // Miss(한글)
    public void Coma_ilumji_and_miss_are_head_effects(int number, int drawnBottom)
    {
        Assert.True(Overhead.IsHeadClass(Small, drawnBottom), $"efct{number:000}");
    }

    [Theory]
    [InlineData(1, 69)] // 몸에 맞는 번쩍임
    [InlineData(22, 48)] // 가슴께까지 내려오는 것
    [InlineData(27, 46)]
    [InlineData(6, 79)] // 발밑
    public void Body_and_impact_effects_stay_where_the_sheet_puts_them(int number, int drawnBottom)
    {
        Assert.False(Overhead.IsHeadClass(Small, drawnBottom), $"efct{number:000}");
    }

    [Fact]
    public void A_tall_cell_is_judged_by_its_own_anchor()
    {
        Assert.True(Overhead.IsHeadClass(Tall, 40)); // efct032, 97 - 40 = 57 above the feet
        Assert.False(Overhead.IsHeadClass(Tall, 68)); // efct068, 29 above the feet
    }

    [Fact]
    public void A_head_effect_is_lifted_off_the_face_to_just_over_the_head()
    {
        // Miss 33 ends 47 above the feet; a head that tops out 55 above them would be drawn over.
        float shift = Overhead.Shift(Small, 23, -55);

        Assert.Equal(-10, shift);
        Assert.Equal(-55 - Overhead.Gap, 23 - Small.AnchorY + shift);
    }

    [Fact]
    public void Over_a_short_creature_it_comes_down_into_that_creature_s_head_slot()
    {
        float shift = Overhead.Shift(Small, 23, -30);

        Assert.Equal(-30 - Overhead.Gap, 23 - Small.AnchorY + shift);
    }

    [Fact]
    public void The_bar_stands_over_the_head_slot_and_the_badges_over_the_bar()
    {
        (float bar, float badges) = Overhead.Place(-55, barShown: true, barHeight: 4, rowHeight: 10);

        float slotTop = -55 - Overhead.Gap - Overhead.SlotHeight;
        Assert.Equal(slotTop - Overhead.Gap - 4, bar);
        Assert.Equal(bar - Overhead.Gap - 10, badges);
    }

    [Fact]
    public void Without_the_bar_the_badges_drop_to_where_it_was()
    {
        (float bar, float shown) = Overhead.Place(-55, barShown: true, barHeight: 4, rowHeight: 10);
        (_, float hidden) = Overhead.Place(-55, barShown: false, barHeight: 4, rowHeight: 10);

        Assert.Equal(bar + 4, hidden + 10); // same bottom as the bar
        Assert.True(hidden > shown);
    }

    [Fact]
    public void Five_badges_soonest_expiring_first_and_the_rest_counted()
    {
        Ailment[] on = [new(10, 6), new(11, 3), new(12, 1), new(13, 5), new(14, 2), new(15, 4), new(16, 6)];

        (IReadOnlyList<Ailment> shown, int more) = Overhead.Badges(on);

        Assert.Equal([12, 14, 11, 15, 13], shown.Select(one => one.Icon));
        Assert.Equal(2, more);
    }

    [Fact]
    public void Five_or_fewer_leave_nothing_over()
    {
        (IReadOnlyList<Ailment> shown, int more) = Overhead.Badges([new(20, 2), new(21, 2)]);

        Assert.Equal([20, 21], shown.Select(one => one.Icon));
        Assert.Equal(0, more);
    }

    [Fact]
    public void In_a_coma_no_badges_are_drawn()
    {
        Ailment[] on = [new(10, 6), new(Overhead.ComaIcon, 2), new(11, 3)];

        Assert.True(Overhead.InComa(on));
        Assert.Empty(Overhead.Badges(on).Shown);
        Assert.Equal(0, Overhead.Badges(on).More);
    }
}
