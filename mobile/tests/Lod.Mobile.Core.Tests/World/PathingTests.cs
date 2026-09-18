using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Walking round the scenery rather than into it. The floors are drawn by hand here — <c>#</c> is a wall,
/// <c>.</c> is floor — so what each case is about can be read at a glance.
/// </summary>
public sealed class PathingTests
{
    /// <summary>Turns a drawn floor into the question the pathing asks: is this tile blocked?</summary>
    private static Func<Tile, bool> Floor(params string[] rows) =>
        tile => tile.Y < 0 || tile.Y >= rows.Length
                || tile.X < 0 || tile.X >= rows[tile.Y].Length
                || rows[tile.Y][tile.X] == '#';

    /// <summary>With nothing in the way the answer is the straight line, and the tile we stand on is not in it.</summary>
    [Fact]
    public void An_open_floor_is_walked_straight_across()
    {
        IReadOnlyList<Tile>? way = Pathing.Way(new Tile(0, 0), new Tile(3, 0), Floor("....."));

        Assert.NotNull(way);
        Assert.Equal(3, way!.Count);
        Assert.Equal(new Tile(1, 0), way[0]);
        Assert.Equal(new Tile(3, 0), way[^1]);
    }

    /// <summary>
    /// A wall between the two is walked round, not through — and the way is as short as going round allows.
    /// Straight-line distance says two steps; the real answer is six.
    /// </summary>
    [Fact]
    public void A_wall_is_walked_round()
    {
        Func<Tile, bool> floor = Floor(
            ".#.",
            ".#.",
            "...");

        Assert.Equal(2, Pathing.Steps(new Tile(0, 0), new Tile(2, 0), one => false));
        Assert.Equal(6, Pathing.Steps(new Tile(0, 0), new Tile(2, 0), floor));
    }

    /// <summary>
    /// Something walled in has no way to it at all. Saying so is what lets a hunt pass over the monster in the
    /// pit and go for one it can actually reach.
    /// </summary>
    [Fact]
    public void Something_walled_in_has_no_way_to_it()
    {
        Func<Tile, bool> floor = Floor(
            ".....",
            ".###.",
            ".#.#.",
            ".###.",
            ".....");

        Assert.Null(Pathing.Way(new Tile(0, 0), new Tile(2, 2), floor));
        Assert.Null(Pathing.Steps(new Tile(0, 0), new Tile(2, 2), floor));
    }

    /// <summary>A wall itself is never a place to walk to.</summary>
    [Fact]
    public void A_wall_is_not_somewhere_to_walk_to()
    {
        Assert.Null(Pathing.Way(new Tile(0, 0), new Tile(1, 0), Floor(".#.")));
    }

    /// <summary>Asking for where we already stand is answered with no steps at all, not with nothing.</summary>
    [Fact]
    public void Standing_on_the_goal_is_no_steps()
    {
        IReadOnlyList<Tile>? way = Pathing.Way(new Tile(1, 1), new Tile(1, 1), Floor("...", "...", "..."));

        Assert.NotNull(way);
        Assert.Empty(way!);
    }

    /// <summary>Further off than we are willing to look is the same as no way, so a frame is never spent sifting a map.</summary>
    [Fact]
    public void Further_than_we_look_is_as_good_as_no_way()
    {
        Assert.Null(Pathing.Steps(new Tile(0, 0), new Tile(9, 0), Floor(".........."), reach: 4));
        Assert.Equal(9, Pathing.Steps(new Tile(0, 0), new Tile(9, 0), Floor(".........."), reach: 20));
    }

    /// <summary>
    /// What a walker actually needs: which way to turn now. Round the wall that means going down first, even
    /// though the thing wanted is straight ahead.
    /// </summary>
    [Fact]
    public void The_first_step_goes_round_the_wall()
    {
        Func<Tile, bool> floor = Floor(
            ".#.",
            ".#.",
            "...");

        Assert.Equal(Direction.South, Pathing.StepTowards(new Tile(0, 0), new Tile(2, 0), floor));
        Assert.Equal(Direction.East, Pathing.StepTowards(new Tile(0, 0), new Tile(2, 0), one => false));
    }

    /// <summary>Nothing to turn towards when there is no way at all.</summary>
    [Fact]
    public void No_way_means_no_step()
    {
        Assert.Null(Pathing.StepTowards(new Tile(0, 0), new Tile(2, 2), Floor(
            ".....",
            ".###.",
            ".#.#.",
            ".###.",
            ".....")));
    }
}
