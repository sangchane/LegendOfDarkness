using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

public sealed class IsometricFloorTests
{
    // lod1.map, the Safe House, as the server announces it and as tools/dat-extract draws it.
    private const int Columns = 30;
    private const int Rows = 31;

    [Fact]
    public void The_picture_is_the_size_the_extractor_draws()
    {
        Assert.Equal(1708, IsometricFloor.PictureWidth(Columns, Rows));
        Assert.Equal(820, IsometricFloor.PictureHeight(Columns, Rows));
    }

    [Fact]
    public void Neighbouring_tiles_sit_half_a_tile_apart_in_both_axes()
    {
        (int X, int Y) here = IsometricFloor.Corner(4, 4, Rows);
        (int X, int Y) east = IsometricFloor.Corner(5, 4, Rows);
        (int X, int Y) south = IsometricFloor.Corner(4, 5, Rows);

        Assert.Equal((IsometricFloor.TileWidth / 2, 13), (east.X - here.X, east.Y - here.Y));
        Assert.Equal((-(IsometricFloor.TileWidth / 2), 13), (south.X - here.X, south.Y - here.Y));
    }

    [Fact]
    public void Every_tile_of_the_map_is_inside_the_picture()
    {
        int width = IsometricFloor.PictureWidth(Columns, Rows);
        int height = IsometricFloor.PictureHeight(Columns, Rows);

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                (int x, int y) = IsometricFloor.Corner(column, row, Rows);

                Assert.InRange(x, 0, width - IsometricFloor.TileWidth);
                Assert.InRange(y, 0, height - IsometricFloor.TileHeight);
            }
        }
    }

    [Fact]
    public void A_figure_stands_in_the_middle_of_its_tile()
    {
        (int cornerX, int cornerY) = IsometricFloor.Corner(4, 4, Rows);
        (int standX, int standY) = IsometricFloor.Stand(4, 4, Rows);

        Assert.Equal(cornerX + (IsometricFloor.TileWidth / 2), standX);
        Assert.InRange(standY, cornerY, cornerY + IsometricFloor.TileHeight);
    }
}
