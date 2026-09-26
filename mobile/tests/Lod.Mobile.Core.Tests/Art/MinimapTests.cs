using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// The always-on minimap in the top row: it is the floor's diamond round us, one tile of it is the size of our own dot,
/// it follows as we walk, and it only shows what falls inside its little box.
/// </summary>
public sealed class MinimapTests
{
    private const float Wide = 152;
    private const float High = 76;

    /// <summary>We stand in the middle of the box, wherever we are on the map.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(34, 29)]
    [InlineData(69, 69)]
    public void We_stand_in_the_middle(int x, int y)
    {
        TabMapProjection frame = Minimap.Frame(new Tile(x, y), 70, 70, Wide, High);

        (float cx, float cy) = frame.Centre(x, y);

        Assert.Equal(Wide / 2, cx, 3);
        Assert.Equal(High / 2, cy, 3);
    }

    /// <summary>
    /// Walking the radius in any of the four ways stays in the box; two tiles further along the box's long way does not.
    /// The diamond is the floor's own flat one (twice as wide as tall), so a way read off it is the way the pad walks.
    /// </summary>
    [Fact]
    public void The_radius_fits_and_no_more()
    {
        Tile me = new(35, 35);
        TabMapProjection frame = Minimap.Frame(me, 70, 70, Wide, High);

        Assert.Equal(TabMapProjection.FlattestSquash, frame.HalfHeight / frame.HalfWidth, 3);

        foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            Assert.True(Minimap.Sees(frame, Wide, High, new Tile(me.X + (dx * Minimap.Radius), me.Y + (dy * Minimap.Radius))));
        }

        // 화면 오른쪽 끝 방향(동·북 사이 대각선이 아니라 칸 x+1, y-1 이 화면 오른쪽이다).
        Tile far = new(me.X + Minimap.Radius, me.Y - Minimap.Radius);
        Assert.False(Minimap.Sees(frame, Wide, High, far));
    }

    /// <summary>Off the map is never seen, even when it would fall inside the box.</summary>
    [Fact]
    public void Off_the_map_is_not_seen()
    {
        TabMapProjection frame = Minimap.Frame(new Tile(0, 0), 70, 70, Wide, High);

        Assert.False(Minimap.Sees(frame, Wide, High, new Tile(-1, 0)));
        Assert.False(Minimap.Sees(frame, Wide, High, new Tile(0, -1)));
        Assert.True(Minimap.Sees(frame, Wide, High, new Tile(1, 0)));
    }

    /// <summary>One step east moves everything else on the minimap one tile the other way — it follows us.</summary>
    [Fact]
    public void It_follows_a_step()
    {
        TabMapProjection before = Minimap.Frame(new Tile(10, 10), 70, 70, Wide, High);
        TabMapProjection after = Minimap.Frame(new Tile(11, 10), 70, 70, Wide, High);

        (float x0, float y0) = before.Centre(20, 5);
        (float x1, float y1) = after.Centre(20, 5);

        Assert.Equal(-before.HalfWidth, x1 - x0, 3);
        Assert.Equal(-before.HalfHeight, y1 - y0, 3);
    }

    /// <summary>
    /// The baked picture has one pixel per tile; drawn through <see cref="Minimap.FromGrid" /> the middle of pixel (c, r)
    /// lands on tile (c, r)'s diamond middle — so the cached floor lines up with the dots drawn over it.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 40)]
    [InlineData(69, 3)]
    public void The_baked_grid_lines_up_with_the_tiles(int column, int row)
    {
        TabMapProjection frame = Minimap.Frame(new Tile(30, 30), 70, 70, Wide, High);

        (float gx, float gy) = Minimap.FromGrid(frame, column + 0.5f, row + 0.5f);
        (float tx, float ty) = frame.Centre(column, row);

        Assert.Equal(tx, gx, 3);
        Assert.Equal(ty, gy, 3);
    }

    /// <summary>Only the dots inside the box are kept; an exit counts when any of its tiles is in sight; we are drawn apart.</summary>
    [Fact]
    public void Only_what_is_in_the_box_is_drawn()
    {
        Tile me = new(35, 35);
        TabMapProjection frame = Minimap.Frame(me, 70, 70, Wide, High);

        TabMarker near = new(new Tile(37, 35), TabMarkerKind.Monster, string.Empty, []);
        TabMarker far = new(new Tile(69, 0), TabMarkerKind.Npc, "멀리", []);
        TabMarker exit = new(new Tile(60, 35), TabMarkerKind.Exit, "출구", [new Tile(60, 35), new Tile(36, 36)]);
        TabMarker self = new(me, TabMarkerKind.Me, string.Empty, []);

        IReadOnlyList<TabMarker> shown = Minimap.InSight(frame, Wide, High, [near, far, exit, self]);

        Assert.Contains(near, shown);
        Assert.Contains(exit, shown);
        Assert.DoesNotContain(far, shown);
        Assert.DoesNotContain(self, shown);
    }
}
