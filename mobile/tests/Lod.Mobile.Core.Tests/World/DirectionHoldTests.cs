using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>방향키: 보던 쪽이면 곧장 걷고, 다른 쪽이면 돌기만 한 뒤 0.2초 더 눌러야 걷는다.</summary>
public sealed class DirectionHoldTests
{
    [Fact]
    public void The_way_already_faced_walks_at_once()
    {
        DirectionHold hold = new();

        Assert.True(hold.IsNew(Direction.East));
        hold.Pressing(looking: true);
        hold.Began(Direction.East);

        Assert.True(hold.MayWalk);
        Assert.False(hold.IsNew(Direction.East));
    }

    [Fact]
    public void Another_way_turns_first_and_walks_after_the_hold()
    {
        DirectionHold hold = new();
        hold.Pressing(looking: false);
        hold.Began(Direction.North);

        Assert.False(hold.MayWalk);

        hold.Held(Tuning.TurnHoldSeconds / 2);
        Assert.False(hold.MayWalk);

        hold.Held(Tuning.TurnHoldSeconds / 2);
        Assert.True(hold.MayWalk);
    }

    [Fact]
    public void Letting_go_makes_the_same_way_new_again()
    {
        DirectionHold hold = new();
        hold.Pressing(looking: true);
        hold.Began(Direction.West);
        hold.Released();

        Assert.True(hold.IsNew(Direction.West));
    }
}
