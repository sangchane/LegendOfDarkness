using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

public sealed class FacingTests
{
    [Fact]
    public void North_and_west_share_a_drawing_and_differ_only_by_the_mirror()
    {
        Facing north = Facing.Of(Direction.North);
        Facing west = Facing.Of(Direction.West);

        Assert.Equal(north.Side, west.Side);
        Assert.False(north.Mirror);
        Assert.True(west.Mirror);
    }

    [Fact]
    public void East_and_south_share_the_other_drawing()
    {
        Facing east = Facing.Of(Direction.East);
        Facing south = Facing.Of(Direction.South);

        Assert.Equal(east.Side, south.Side);
        Assert.NotEqual(east.Side, Facing.Of(Direction.North).Side);
        Assert.False(east.Mirror);
        Assert.True(south.Mirror);
    }

    [Fact]
    public void Every_direction_moves_diagonally_and_no_two_the_same_way()
    {
        (int X, int Y)[] steps =
        [
            Facing.Step(Direction.North),
            Facing.Step(Direction.East),
            Facing.Step(Direction.South),
            Facing.Step(Direction.West)
        ];

        Assert.All(steps, step => Assert.Equal((Facing.StepX, Facing.StepY), (Math.Abs(step.X), Math.Abs(step.Y))));
        Assert.Equal(4, steps.Distinct().Count());
    }

    [Fact]
    public void A_step_on_screen_is_the_same_step_on_the_floor()
    {
        const int rows = 31;

        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            (int column, int row) = Facing.TileStep(direction);
            (int x, int y) = Facing.Step(direction);

            (int fromX, int fromY) = IsometricFloor.Corner(10, 10, rows);
            (int toX, int toY) = IsometricFloor.Corner(10 + column, 10 + row, rows);

            // The two ways of saying "one step" have to agree, or the figure drifts off the tiles.
            Assert.Equal((x, y), (toX - fromX, toY - fromY));
        }
    }

    [Fact]
    public void Standing_and_walking_frames_stay_inside_their_own_half()
    {
        Assert.Equal(0, WalkMotion.Stand(Side.Back));
        Assert.Equal(5, WalkMotion.Stand(Side.Front));

        int[] back = [.. Enumerable.Range(0, 9).Select(step => WalkMotion.Walk(Side.Back, step))];
        int[] front = [.. Enumerable.Range(0, 9).Select(step => WalkMotion.Walk(Side.Front, step))];

        Assert.All(back, frame => Assert.InRange(frame, 1, 4));
        Assert.All(front, frame => Assert.InRange(frame, 6, 9));

        // The cycle repeats rather than running off the end of the sheet.
        Assert.Equal(WalkMotion.Walk(Side.Back, 0), WalkMotion.Walk(Side.Back, WalkMotion.WalkFrames));
    }
}
