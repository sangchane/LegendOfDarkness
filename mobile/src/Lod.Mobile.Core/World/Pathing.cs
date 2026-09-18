namespace Lod.Mobile.Core.World;

/// <summary>
/// Finding a way round the scenery. Walking straight at something is what leaves a figure pressed against a
/// wall with the thing it wants three tiles away behind it (사용자, 2026-09-18).
/// </summary>
/// <remarks>
/// This is the idea a map application uses to route a car, cut down to what a tile floor needs. Those use
/// Dijkstra or A*, which earn their keep when steps cost different amounts — a motorway against a lane. Here
/// every step costs exactly one, and for that the plainest breadth-first search gives the very same shortest
/// way with nothing to tune: spread one ring at a time, and the first time the goal is touched is by a
/// shortest route.
///
/// It is kept engine-free and asks "is this tile blocked?" as a question rather than taking a map, so the one
/// routine serves the floor the client drew, the walls the server knows, and a test with a wall drawn by hand.
/// </remarks>
public static class Pathing
{
    /// <summary>
    /// The shortest way from one tile to another — the goal last, and the tile we stand on left out. Nothing
    /// at all when there is no way, or when it is further than <paramref name="reach" /> steps.
    /// </summary>
    /// <param name="blocked">Whether a tile cannot be walked on. Tiles off the map count as blocked.</param>
    /// <param name="reach">
    /// How many steps to look before giving up. A whole map is a lot of tiles to sift through on every frame,
    /// and anything further off than this is not worth walking to anyway.
    /// </param>
    public static IReadOnlyList<Tile>? Way(Tile from, Tile to, Func<Tile, bool> blocked, int reach = 40)
    {
        if (from == to)
        {
            return [];
        }

        if (blocked(to))
        {
            return null;
        }

        Dictionary<Tile, Tile> cameFrom = new() { [from] = from };
        Queue<(Tile Where, int Steps)> edge = new();
        edge.Enqueue((from, 0));

        while (edge.Count > 0)
        {
            (Tile where, int steps) = edge.Dequeue();

            if (steps >= reach)
            {
                continue;
            }

            foreach (Tile next in Around(where))
            {
                if (cameFrom.ContainsKey(next) || blocked(next))
                {
                    continue;
                }

                cameFrom[next] = where;

                if (next == to)
                {
                    return Retrace(cameFrom, from, to);
                }

                edge.Enqueue((next, steps + 1));
            }
        }

        return null;
    }

    /// <summary>
    /// How many steps away something really is — round the scenery, not through it. Nothing when there is no
    /// way at all, so a monster behind a wall can be passed over for one that can actually be reached.
    /// </summary>
    public static int? Steps(Tile from, Tile to, Func<Tile, bool> blocked, int reach = 40) =>
        Way(from, to, blocked, reach)?.Count;

    /// <summary>Which way to turn now to be going there. Nothing when there is no way.</summary>
    public static Art.Direction? StepTowards(Tile from, Tile to, Func<Tile, bool> blocked, int reach = 40)
    {
        if (Way(from, to, blocked, reach) is not { Count: > 0 } way)
        {
            return null;
        }

        Tile first = way[0];

        return first.X > from.X ? Art.Direction.East
            : first.X < from.X ? Art.Direction.West
            : first.Y > from.Y ? Art.Direction.South
            : Art.Direction.North;
    }

    /// <summary>The four tiles a step away. The original never walks diagonally, and neither does this.</summary>
    private static IEnumerable<Tile> Around(Tile tile)
    {
        yield return new Tile(tile.X, tile.Y - 1);
        yield return new Tile(tile.X + 1, tile.Y);
        yield return new Tile(tile.X, tile.Y + 1);
        yield return new Tile(tile.X - 1, tile.Y);
    }

    private static List<Tile> Retrace(Dictionary<Tile, Tile> cameFrom, Tile from, Tile to)
    {
        List<Tile> way = [];

        for (Tile at = to; at != from; at = cameFrom[at])
        {
            way.Add(at);
        }

        way.Reverse();

        return way;
    }
}
