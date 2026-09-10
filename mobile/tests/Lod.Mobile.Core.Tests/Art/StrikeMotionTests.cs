using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// A blow is drawn in a file of its own — the one ending 02 — which holds nothing else, so its frames
/// start again from zero rather than carrying on from the walk. Getting that wrong plays a step, or
/// nothing at all.
/// </summary>
public sealed class StrikeMotionTests
{
    [Fact]
    public void The_two_directions_use_the_two_halves_of_a_four_frame_file()
    {
        Assert.Equal(0, WalkMotion.Strike(Side.Back, 0));
        Assert.Equal(1, WalkMotion.Strike(Side.Back, 1));
        Assert.Equal(2, WalkMotion.Strike(Side.Front, 0));
        Assert.Equal(3, WalkMotion.Strike(Side.Front, 1));
    }

    /// <summary>A blow does not begin where a step ended: both start at the top of their own file.</summary>
    [Fact]
    public void A_blow_does_not_start_where_the_walk_does()
    {
        Assert.NotEqual(WalkMotion.Walk(Side.Back, 0), WalkMotion.Strike(Side.Back, 0));
        Assert.Equal(WalkMotion.Stand(Side.Back), WalkMotion.Strike(Side.Back, 0));
    }

    [Fact]
    public void It_comes_back_round_rather_than_running_off_the_end()
    {
        Assert.Equal(WalkMotion.Strike(Side.Front, 0), WalkMotion.Strike(Side.Front, WalkMotion.StrikeFrames));
        Assert.Equal(WalkMotion.Strike(Side.Back, 1), WalkMotion.Strike(Side.Back, -1));
    }
}
