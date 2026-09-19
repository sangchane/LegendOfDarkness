using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 결번 건너뛰기와 성별을 바꿀 때 옮기는 규칙 — 순수 계산이라 바이트 시험처럼 짧게 잰다
/// (plans/character-creation.md Task B).
/// </summary>
public sealed class HairStylesTests
{
    [Fact]
    public void Male_list_is_59_and_skips_26()
    {
        Assert.Equal(59, HairStyles.For(1).Count);
        Assert.DoesNotContain(26, HairStyles.For(1));
    }

    [Fact]
    public void Female_list_is_56_and_skips_18_26_32_33()
    {
        Assert.Equal(56, HairStyles.For(2).Count);
        Assert.DoesNotContain(18, HairStyles.For(2));
        Assert.DoesNotContain(26, HairStyles.For(2));
        Assert.DoesNotContain(32, HairStyles.For(2));
        Assert.DoesNotContain(33, HairStyles.For(2));
    }

    [Fact]
    public void Stepping_forward_skips_a_missing_number()
    {
        Assert.Equal(27, HairStyles.Step(25, gender: 1, direction: 1));
    }

    [Fact]
    public void Stepping_backward_wraps_to_the_far_end()
    {
        Assert.Equal(60, HairStyles.Step(1, gender: 1, direction: -1));
    }

    [Fact]
    public void Switching_gender_moves_a_now_missing_number_to_the_nearer_neighbour()
    {
        // 32 는 여자에 없다. 31(거리 1)이 34(거리 2)보다 가깝다.
        Assert.Equal(31, HairStyles.ClosestFor(32, newGender: 2));
    }

    [Fact]
    public void A_tie_between_neighbours_moves_to_the_smaller_one()
    {
        // 18 만 빠져 있어 17·19 둘 다 거리 1 — 작은 쪽 17.
        Assert.Equal(17, HairStyles.ClosestFor(18, newGender: 2));
    }

    [Fact]
    public void A_number_that_already_exists_for_the_new_gender_does_not_move()
    {
        Assert.Equal(40, HairStyles.ClosestFor(40, newGender: 2));
    }
}
