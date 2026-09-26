using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Art;

/// <summary>
/// The small map that always stays up in the top row, as phone RPGs keep one in a corner: the floor round us drawn as
/// the floor itself is (a flat diamond, north up and to the right), <see cref="Radius" /> tiles each way, with us in the
/// middle as one tile's diamond. Everything is in the box's own pixels, (0, 0) its top-left corner.
/// </summary>
/// <remarks>
/// It reuses the 길 찾기 map's arithmetic (<see cref="TabMapProjection" />) — only the scale and the middle differ:
/// that one fits the whole map, this one keeps a fixed number of tiles round us and slides as we walk.
/// </remarks>
public static class Minimap
{
    /// <summary>How many steps each way the minimap shows at first.</summary>
    public const int Radius = 12;

    /// <summary>The radii [+]·[−] step through (2026-09-26) — [+] is fewer tiles, larger.</summary>
    public static IReadOnlyList<int> Steps { get; } = [6, 9, 12, 16, 20, 24];

    /// <summary>One step in (fewer tiles); the smallest stays.</summary>
    public static int ZoomIn(int radius)
    {
        int at = IndexOf(radius);

        return Steps[Math.Max(0, at - 1)];
    }

    /// <summary>One step out (more tiles); the largest stays.</summary>
    public static int ZoomOut(int radius)
    {
        int at = IndexOf(radius);

        return Steps[Math.Min(Steps.Count - 1, at + 1)];
    }

    /// <summary>The step nearest a radius — for one read back from the device.</summary>
    public static int NearestStep(int radius) => Steps.MinBy(step => Math.Abs(step - radius));

    private static int IndexOf(int radius)
    {
        int nearest = NearestStep(radius);

        for (int at = 0; at < Steps.Count; at++)
        {
            if (Steps[at] == nearest)
            {
                return at;
            }
        }

        return 0;
    }

    /// <summary>
    /// The drawing round <paramref name="me" />: as large as lets <paramref name="radius" /> steps in each of the four
    /// ways stay in the box, in the floor's flat diamond.
    /// </summary>
    public static TabMapProjection Frame(Tile me, int columns, int rows, float width, float height, int radius = Radius)
    {
        int steps = Math.Max(1, radius);
        float squash = TabMapProjection.FlattestSquash;

        // 한 걸음은 화면에서 (반폭, 반높이)만큼 간다 — 동서남북 어느 쪽으로 radius 걸음 가도 상자 안.
        float half = Math.Min(width / (2f * steps), height / (2f * steps * squash));
        float halfHeight = half * squash;

        return new TabMapProjection(
            (width / 2) - ((me.X - me.Y) * half),
            (height / 2) - ((me.X + me.Y + 1) * halfHeight),
            half,
            halfHeight,
            columns,
            rows);
    }

    /// <summary>Whether a tile's middle falls inside the box — and the tile is on the map at all.</summary>
    public static bool Sees(TabMapProjection frame, float width, float height, Tile tile)
    {
        if (tile.X < 0 || tile.Y < 0 || tile.X >= frame.Columns || tile.Y >= frame.Rows)
        {
            return false;
        }

        (float x, float y) = frame.Centre(tile.X, tile.Y);
        const float slack = 0.01f;

        return x >= -slack && x <= width + slack && y >= -slack && y <= height + slack;
    }

    /// <summary>
    /// Where a point of the one-pixel-per-tile picture lands — the cached floor is drawn through this, so pixel (c, r)'s
    /// middle sits on tile (c, r)'s diamond middle. Its axes are (half, halfHeight) and (−half, halfHeight).
    /// </summary>
    public static (float X, float Y) FromGrid(TabMapProjection frame, float gridX, float gridY) =>
        (frame.OriginX + ((gridX - gridY) * frame.HalfWidth), frame.OriginY + ((gridX + gridY) * frame.HalfHeight));

    /// <summary>The dots worth drawing: inside the box (an exit when any of its tiles is), ourselves left to the caller.</summary>
    public static IReadOnlyList<TabMarker> InSight(TabMapProjection frame, float width, float height, IEnumerable<TabMarker> markers) =>
    [
        .. markers.Where(marker => marker.Kind != TabMarkerKind.Me
                                   && (marker.Kind == TabMarkerKind.Exit
                                       ? marker.Goals.Any(tile => Sees(frame, width, height, tile))
                                       : Sees(frame, width, height, marker.Where)))
    ];

    /// <summary>
    /// Whether a tile's middle falls inside the round minimap — a circle of diameter <paramref name="side" /> filling a
    /// square box (사용자, 2026-09-26: 동그란 테두리) — and the tile is on the map.
    /// </summary>
    public static bool SeesRound(TabMapProjection frame, float side, Tile tile)
    {
        if (tile.X < 0 || tile.Y < 0 || tile.X >= frame.Columns || tile.Y >= frame.Rows)
        {
            return false;
        }

        (float x, float y) = frame.Centre(tile.X, tile.Y);
        float dx = x - (side / 2), dy = y - (side / 2);

        return (dx * dx) + (dy * dy) <= (side / 2) * (side / 2);
    }

    /// <summary>The dots inside the circle (an exit when any of its tiles is), ourselves left to the caller.</summary>
    public static IReadOnlyList<TabMarker> InRound(TabMapProjection frame, float side, IEnumerable<TabMarker> markers) =>
    [
        .. markers.Where(marker => marker.Kind != TabMarkerKind.Me
                                   && (marker.Kind == TabMarkerKind.Exit
                                       ? marker.Goals.Any(tile => SeesRound(frame, side, tile))
                                       : SeesRound(frame, side, marker.Where)))
    ];
}
