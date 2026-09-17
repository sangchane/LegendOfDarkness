namespace Lod.Mobile.Core.Art;

/// <summary>
/// Where a tile lands on the drawn floor. The map picture is built by laying each tile half a tile across
/// and half a tile down from its neighbour, so this has to agree with the renderer in tools/dat-extract
/// exactly — a pixel of disagreement puts every character a little off the ground.
/// </summary>
public static class IsometricFloor
{
    public const int TileWidth = 56;
    public const int TileHeight = 27;

    private const int HalfWidth = TileWidth / 2;
    private const int HalfHeight = 13;

    /// <summary>How wide the whole floor comes out for a map of this size.</summary>
    public static int PictureWidth(int columns, int rows) => (columns + rows) * HalfWidth;

    public static int PictureHeight(int columns, int rows) => ((columns + rows) * HalfHeight) + TileHeight;

    /// <summary>The top-left corner of one tile's diamond.</summary>
    public static (int X, int Y) Corner(int column, int row, int rows) =>
        ((rows * HalfWidth) + ((column - row) * HalfWidth) - HalfWidth, (column + row) * HalfHeight);

    /// <summary>
    /// Where a figure standing on that tile puts its feet: the middle of the diamond, a little below centre
    /// so it reads as standing on the tile rather than floating over it.
    /// </summary>
    public static (int X, int Y) Stand(int column, int row, int rows)
    {
        (int x, int y) = Corner(column, row, rows);

        return (x + HalfWidth, y + HalfHeight + 8);
    }

    /// <summary>
    /// The bottom-left corner of a picture standing on a cell (<see cref="MapObject" />): the left half's picture
    /// starts at the cell's left corner, the right half's half a tile over, and both hang up from the cell's bottom
    /// corner — da-lib Graphics.RenderMap. Sorting by this height puts whoever is behind the cell under the picture
    /// and whoever is in front over it.
    /// </summary>
    public static (int X, int Y) ObjectFoot(int column, int row, int rows, bool right)
    {
        (int x, int y) = Corner(column, row, rows);

        return (right ? x + HalfWidth : x, y + (2 * HalfHeight));
    }
}
