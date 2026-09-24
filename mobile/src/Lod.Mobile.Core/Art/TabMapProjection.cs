namespace Lod.Mobile.Core.Art;

/// <summary>
/// Where each tile lands on the 길 찾기 map (the original's Tab map), and which tile a finger on it means.
/// </summary>
/// <remarks>
/// The map is drawn as the floor is — a diamond, north up and to the right — so a way read off the map is the
/// way the thumb then walks on the floor. The original's diamond is twice as wide as it is tall; a phone held
/// upright has more height than width to give, so the diamond may stand taller (up to square) to fill the box.
/// Everything is in the box's own pixels: (0, 0) is its top-left corner.
/// </remarks>
public readonly record struct TabMapProjection(float OriginX, float OriginY, float HalfWidth, float HalfHeight, int Columns, int Rows)
{
    /// <summary>The original's tile is 56×27 — the flattest the diamond gets.</summary>
    public const float FlattestSquash = 0.5f;

    /// <summary>Fits a map of this size into a box, as large as the box allows, in the middle of it.</summary>
    public static TabMapProjection Fit(int columns, int rows, float width, float height)
    {
        int across = Math.Max(1, columns + rows);
        float squash = Math.Clamp(height / Math.Max(1f, width), FlattestSquash, 1f);
        float half = Math.Min(width / across, height / (across * squash));
        float halfHeight = half * squash;

        float originX = ((width - (across * half)) / 2) + (rows * half);
        float originY = (height - (across * halfHeight)) / 2;

        return new TabMapProjection(originX, originY, half, halfHeight, columns, rows);
    }

    /// <summary>
    /// The same map drawn <paramref name="factor" /> times larger with one tile in the middle of the box — a phone box
    /// is a few hundred pixels, and a whole 70×70 town in it leaves a tile a few pixels across. What falls outside the
    /// box is simply not drawn.
    /// </summary>
    public TabMapProjection Zoomed(float factor, int column, int row, float width, float height)
    {
        float half = HalfWidth * factor;
        float halfHeight = HalfHeight * factor;

        return this with
        {
            HalfWidth = half,
            HalfHeight = halfHeight,
            OriginX = (width / 2) - ((column - row) * half),
            OriginY = (height / 2) - ((column + row + 1) * halfHeight)
        };
    }

    /// <summary>The middle of a tile's diamond.</summary>
    public (float X, float Y) Centre(int column, int row) =>
        (OriginX + ((column - row) * HalfWidth), OriginY + ((column + row + 1) * HalfHeight));

    /// <summary>The tile under a point, or nothing when the point is off the map.</summary>
    public (int Column, int Row)? TileAt(float x, float y)
    {
        float across = (x - OriginX) / HalfWidth;
        float down = ((y - OriginY) / HalfHeight) - 1;
        int column = (int)MathF.Round((across + down) / 2);
        int row = (int)MathF.Round((down - across) / 2);

        return column < 0 || row < 0 || column >= Columns || row >= Rows ? null : (column, row);
    }

    /// <summary>
    /// Which way a compass point runs on the map, as a screen direction of length one — north is up and to the
    /// right, as on the floor. For the arrow that says which way we face.
    /// </summary>
    public (float X, float Y) Toward(Direction direction)
    {
        (int column, int row) = Facing.TileStep(direction);
        float x = (column - row) * HalfWidth;
        float y = (column + row) * HalfHeight;
        float length = MathF.Sqrt((x * x) + (y * y));

        return (x / length, y / length);
    }
}
