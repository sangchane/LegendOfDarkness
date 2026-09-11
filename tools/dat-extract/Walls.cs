namespace Lod.DatExtract;

/// <summary>
/// Which cells cannot be walked into. A wall is not a picture of its own — <c>sotp.dat</c> carries one byte
/// of flags per tile, and a wall number whose flag is <c>0x0F</c> blocks. The server decides the same way
/// (<c>Hades.Server.Base</c> <c>Types/Area.cs</c>), so this reads the same bytes rather than inventing a
/// second rule that could drift from it.
/// </summary>
internal static class Walls
{
    /// <summary>The flag sotp.dat uses for a wall. da-lib names it TileFlags.Wall.</summary>
    public const byte WallFlag = 0x0F;

    /// <summary>One cell of a .map file: the floor picture and the two wall numbers.</summary>
    internal readonly record struct Cell(int Floor, int Left, int Right);

    /// <summary>
    /// Whether a cell blocks. A wall number of 0 means "no wall on that side", so a cell with neither is
    /// open; with one, that one decides; with both, either one blocking is enough.
    /// </summary>
    public static bool Blocks(byte[] sotp, int left, int right)
    {
        if (left == 0 && right == 0)
        {
            return false;
        }

        if (left == 0)
        {
            return IsWall(sotp, right);
        }

        if (right == 0)
        {
            return IsWall(sotp, left);
        }

        return IsWall(sotp, left) || IsWall(sotp, right);
    }

    /// <summary>
    /// Wall numbers count from 1, so the flag for number N sits at N-1. The server indexes without checking
    /// and would throw on a number past the end of the table; a reading tool answers "not a wall" instead.
    /// </summary>
    private static bool IsWall(byte[] sotp, int number)
    {
        int index = number - 1;

        return index >= 0 && index < sotp.Length && sotp[index] == WallFlag;
    }

    /// <summary>Reads a .map file. Each cell is three little-endian ushorts: floor, left wall, right wall.</summary>
    public static List<Cell> Read(byte[] map)
    {
        List<Cell> cells = [];

        for (int at = 0; at + 6 <= map.Length; at += 6)
        {
            cells.Add(new Cell(
                map[at] | (map[at + 1] << 8),
                map[at + 2] | (map[at + 3] << 8),
                map[at + 4] | (map[at + 5] << 8)));
        }

        return cells;
    }
}
