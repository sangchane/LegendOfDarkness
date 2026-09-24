using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>The 길 찾기 map's diamond: it fits the box, it is the floor's way round, and a finger finds its tile.</summary>
public sealed class TabMapProjectionTests
{
    /// <summary>Every tile's diamond stays inside the box, in either shape of phone.</summary>
    [Theory]
    [InlineData(70, 70, 328, 330)]
    [InlineData(70, 70, 440, 250)]
    [InlineData(30, 60, 300, 300)]
    public void The_whole_map_fits_the_box(int columns, int rows, float width, float height)
    {
        TabMapProjection map = TabMapProjection.Fit(columns, rows, width, height);

        foreach ((int column, int row) in new[] { (0, 0), (columns - 1, 0), (0, rows - 1), (columns - 1, rows - 1) })
        {
            (float x, float y) = map.Centre(column, row);

            Assert.InRange(x - map.HalfWidth, -0.01f, width + 0.01f);
            Assert.InRange(x + map.HalfWidth, -0.01f, width + 0.01f);
            Assert.InRange(y - map.HalfHeight, -0.01f, height + 0.01f);
            Assert.InRange(y + map.HalfHeight, -0.01f, height + 0.01f);
        }
    }

    /// <summary>
    /// Wide boxes get the original's flat diamond (twice as wide as tall); a square box may stand it up to square
    /// — never taller, so the map never looks stood on end.
    /// </summary>
    [Fact]
    public void The_diamond_is_flat_when_wide_and_at_most_square_when_tall()
    {
        TabMapProjection wide = TabMapProjection.Fit(70, 70, 440, 150);
        TabMapProjection tall = TabMapProjection.Fit(70, 70, 328, 600);

        Assert.Equal(TabMapProjection.FlattestSquash, wide.HalfHeight / wide.HalfWidth, 3);
        Assert.Equal(1f, tall.HalfHeight / tall.HalfWidth, 3);
    }

    /// <summary>North runs up and to the right, east down and to the right — the floor's own way round.</summary>
    [Fact]
    public void Compass_points_run_as_on_the_floor()
    {
        TabMapProjection map = TabMapProjection.Fit(70, 70, 440, 250);

        (float nx, float ny) = map.Toward(Direction.North);
        (float ex, float ey) = map.Toward(Direction.East);

        Assert.True(nx > 0 && ny < 0);
        Assert.True(ex > 0 && ey > 0);

        (float ax, float ay) = map.Centre(10, 10);
        (float bx, float by) = map.Centre(10, 9);
        Assert.True(bx > ax && by < ay);
    }

    /// <summary>Zoomed in, the tile we stand on is in the middle of the box and a finger still finds its tile.</summary>
    [Fact]
    public void Zooming_puts_us_in_the_middle()
    {
        TabMapProjection whole = TabMapProjection.Fit(70, 70, 400, 200);
        TabMapProjection close = whole.Zoomed(2.5f, 60, 12, 400, 200);

        (float x, float y) = close.Centre(60, 12);
        Assert.Equal(200f, x, 3);
        Assert.Equal(100f, y, 3);
        Assert.Equal(whole.HalfWidth * 2.5f, close.HalfWidth, 3);
        Assert.Equal((61, 12), close.TileAt(x + close.HalfWidth, y + close.HalfHeight));
    }

    /// <summary>A finger anywhere inside a tile's diamond finds that tile; off the map finds nothing.</summary>
    [Fact]
    public void A_point_finds_its_tile()
    {
        TabMapProjection map = TabMapProjection.Fit(70, 70, 328, 330);

        foreach ((int column, int row) in new[] { (0, 0), (69, 26), (35, 60), (69, 69) })
        {
            (float x, float y) = map.Centre(column, row);

            Assert.Equal((column, row), map.TileAt(x, y));
            Assert.Equal((column, row), map.TileAt(x + (map.HalfWidth * 0.4f), y));
            Assert.Equal((column, row), map.TileAt(x, y - (map.HalfHeight * 0.4f)));
        }

        Assert.Null(map.TileAt(1, 1));
    }
}
