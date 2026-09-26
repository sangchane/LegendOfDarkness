using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// 사용자(2026-09-26) — "돈은 항상 아이템 밑에 있는 구조로". 같은 칸의 금화와 물건은 같은 높이(Y)라 y 정렬만으로는
/// 나중에 온 것이 위에 그려졌다(금화 무더기는 합쳐질 때마다 새로 놓인다). 금화를 아주 조금 먼저 그리게 한다.
/// </summary>
public sealed class GroundPileTests
{
    [Theory]
    [InlineData(32905)]
    [InlineData(32910)]
    public void Gold_is_known_by_its_picture(int sprite) => Assert.True(GroundPile.IsGold(sprite));

    [Theory]
    [InlineData(32904)]
    [InlineData(32911)]
    [InlineData(32827)]
    public void Anything_else_is_not_gold(int sprite) => Assert.False(GroundPile.IsGold(sprite));

    [Fact]
    public void On_one_tile_gold_is_drawn_before_the_item_so_the_item_lies_on_top()
    {
        const float tileY = 120f;
        int[] arrived = [32827, 32906, 32882];

        int[] drawn = [.. arrived.OrderBy(sprite => tileY + GroundPile.SortNudge(sprite))];

        Assert.Equal(32906, drawn[0]);
    }

    [Fact]
    public void The_nudge_is_too_small_to_move_gold_off_its_tile_or_under_the_row_behind()
    {
        float nudge = Math.Abs(GroundPile.SortNudge(32905));

        Assert.InRange(nudge, float.Epsilon, 0.5f);
        Assert.Equal(0f, GroundPile.SortNudge(32827));
    }
}
