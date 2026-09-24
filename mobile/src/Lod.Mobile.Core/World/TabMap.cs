using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.World;

/// <summary>A way off the map: the tiles that move you on, and where they take you.</summary>
public sealed record MapExit(string To, IReadOnlyList<Tile> Tiles)
{
    /// <summary>The tile of the exit nearest its middle — where its label goes.</summary>
    public Tile Middle
    {
        get
        {
            double x = Tiles.Average(tile => tile.X);
            double y = Tiles.Average(tile => tile.Y);

            return Tiles.MinBy(tile => Math.Abs(tile.X - x) + Math.Abs(tile.Y - y));
        }
    }
}

/// <summary>Somebody who always stands in the same place — a shopkeeper, a trainer.</summary>
public sealed record MapSign(Tile Where, string Name);

/// <summary>
/// What the 길 찾기 map knows about a map beyond its walls: the exits and the standing NPCs, read from
/// <c>assets/world/guide.txt</c> (<c>scripts/build-client-guide.py</c>, out of the server's warp and NPC templates).
/// </summary>
/// <remarks>
/// The original client never knew where the exits were — it saw a door drawn on the floor and the server moved
/// it when it stepped there. A phone map that has to say "this way to 노비스평원A" needs it written down.
/// </remarks>
public sealed class MapGuide
{
    private readonly Dictionary<int, List<(Tile Where, string To)>> _exits = [];
    private readonly Dictionary<int, List<MapSign>> _signs = [];

    public static MapGuide Empty { get; } = new();

    public static MapGuide Read(string text)
    {
        MapGuide guide = new();

        foreach (string line in text.Split('\n', StringSplitOptions.TrimEntries))
        {
            string[] words = line.Split(' ', 5, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 5 || !int.TryParse(words[1], out int map)
                || !int.TryParse(words[2], out int x) || !int.TryParse(words[3], out int y))
            {
                continue;
            }

            switch (words[0])
            {
                case "exit":
                    Add(guide._exits, map, (new Tile(x, y), words[4]));
                    break;

                case "npc":
                    Add(guide._signs, map, new MapSign(new Tile(x, y), words[4]));
                    break;
            }
        }

        return guide;
    }

    /// <summary>
    /// The exits on one map. Warp tiles side by side that go to the same place are one exit — the edge of
    /// 노비스마을 into 노비스평원A is five tiles, and five labels saying the same thing would only be clutter.
    /// </summary>
    public IReadOnlyList<MapExit> ExitsOn(int map)
    {
        if (!_exits.TryGetValue(map, out List<(Tile Where, string To)>? tiles))
        {
            return [];
        }

        List<MapExit> exits = [];
        HashSet<Tile> taken = [];

        foreach ((Tile start, string to) in tiles)
        {
            if (!taken.Add(start))
            {
                continue;
            }

            HashSet<Tile> same = [.. tiles.Where(one => one.To == to).Select(one => one.Where)];
            List<Tile> group = [start];

            for (int at = 0; at < group.Count; at++)
            {
                Tile here = group[at];

                foreach (Tile next in new Tile[] { new(here.X, here.Y - 1), new(here.X + 1, here.Y), new(here.X, here.Y + 1), new(here.X - 1, here.Y) })
                {
                    if (same.Contains(next) && taken.Add(next))
                    {
                        group.Add(next);
                    }
                }
            }

            exits.Add(new MapExit(to, group));
        }

        return exits;
    }

    public IReadOnlyList<MapSign> SignsOn(int map) => _signs.TryGetValue(map, out List<MapSign>? signs) ? signs : [];

    private static void Add<T>(Dictionary<int, List<T>> into, int map, T what)
    {
        if (!into.TryGetValue(map, out List<T>? list))
        {
            into[map] = list = [];
        }

        list.Add(what);
    }
}

/// <summary>What a dot on the 길 찾기 map stands for. Drawn in this order, so the ones that matter most go on top.</summary>
public enum TabMarkerKind
{
    Monster,
    Person,
    Npc,
    Party,
    Exit,
    Me
}

/// <summary>One dot on the map, with the words beside it when it has any.</summary>
public sealed record TabMarker(Tile Where, TabMarkerKind Kind, string Label, IReadOnlyList<Tile> Goals);

/// <summary>Where a tap on the map sends us: the tiles that count as arriving, and what to call it.</summary>
public sealed record TabGoal(string Label, IReadOnlyList<Tile> Goals);

/// <summary>
/// The 길 찾기 map — the original's Tab map (<c>Legend.exe</c> <c>MapViewPane</c>, the <c>TabMap</c> button in
/// <c>setoa.dat</c>) redone for a thumb: the walls, the ways out, who is about, and a tap to walk there.
/// </summary>
public static class TabMap
{
    /// <summary>
    /// Sorts what the client knows onto the map. Monsters are dots only; NPCs and exits are named, because those
    /// are what someone finding the way is looking for; party members are told apart from other people.
    /// </summary>
    /// <param name="partyNames">Who is in our group — people whose name is here are drawn as party.</param>
    public static IReadOnlyList<TabMarker> Markers(
        Tile me,
        IEnumerable<Character> others,
        IEnumerable<Creature> creatures,
        IEnumerable<string> partyNames,
        IReadOnlyList<MapExit> exits,
        IReadOnlyList<MapSign> signs)
    {
        HashSet<string> party = [.. partyNames];
        List<TabMarker> markers = [];
        HashSet<string> named = [];

        foreach (Creature one in creatures)
        {
            switch (one.Kind)
            {
                case CreatureKind.Hostile:
                    markers.Add(new TabMarker(one.Where, TabMarkerKind.Monster, string.Empty, Beside(one.Where)));
                    break;

                case CreatureKind.Merchant:
                    // 이식한 NPC 는 이름에 자리가 붙어 온다(카르마@노비스마을식당#3,10).
                    string name = one.Name.Split('@')[0];
                    named.Add(name);
                    markers.Add(new TabMarker(one.Where, TabMarkerKind.Npc, name, Beside(one.Where)));
                    break;
            }
        }

        // 멀어서 아직 안 보이는 NPC 는 템플릿 자리에 둔다. 보이는 것은 서버가 말한 자리가 이긴다.
        markers.AddRange(signs.Where(sign => !named.Contains(sign.Name))
            .Select(sign => new TabMarker(sign.Where, TabMarkerKind.Npc, sign.Name, Beside(sign.Where))));

        markers.AddRange(others.Select(one => party.Contains(one.Name)
            ? new TabMarker(one.Where, TabMarkerKind.Party, one.Name, Beside(one.Where))
            : new TabMarker(one.Where, TabMarkerKind.Person, string.Empty, Beside(one.Where))));

        markers.AddRange(exits.Select(exit => new TabMarker(exit.Middle, TabMarkerKind.Exit, exit.To, exit.Tiles)));
        markers.Add(new TabMarker(me, TabMarkerKind.Me, string.Empty, []));

        return [.. markers.OrderBy(marker => marker.Kind)];
    }

    /// <summary>
    /// What a finger at this point on the map means. A named place near the finger wins — an exit or an NPC is a
    /// few pixels across on a phone and has to be forgiving — otherwise the tile under it, when it can be stood on.
    /// Nothing when neither.
    /// </summary>
    /// <param name="reach">How far from a named place, in the map's pixels, a finger still counts as on it.</param>
    public static TabGoal? Pick(
        TabMapProjection map,
        float x,
        float y,
        IEnumerable<TabMarker> markers,
        Func<Tile, bool> blocked,
        float reach = 24)
    {
        TabMarker? nearest = null;
        float best = reach * reach;

        foreach (TabMarker marker in markers.Where(one => one.Kind is TabMarkerKind.Exit or TabMarkerKind.Npc))
        {
            IEnumerable<Tile> spots = marker.Kind == TabMarkerKind.Exit ? marker.Goals : [marker.Where];

            foreach (Tile spot in spots)
            {
                (float cx, float cy) = map.Centre(spot.X, spot.Y);
                float distance = ((cx - x) * (cx - x)) + ((cy - y) * (cy - y));

                if (distance < best)
                {
                    best = distance;
                    nearest = marker;
                }
            }
        }

        if (nearest is not null)
        {
            return new TabGoal(nearest.Label, nearest.Goals);
        }

        return map.TileAt(x, y) is (int column, int row) && !blocked(new Tile(column, row))
            ? new TabGoal(string.Empty, [new Tile(column, row)])
            : null;
    }

    /// <summary>
    /// The shortest way to whichever of the goals is nearest by walking — an exit five tiles wide is reached at
    /// whichever of its tiles comes first. The goal last, the tile we stand on left out; empty when we are already
    /// on one; nothing when none can be reached.
    /// </summary>
    public static IReadOnlyList<Tile>? WayToAny(Tile from, IEnumerable<Tile> goals, Func<Tile, bool> blocked, int reach = 4096)
    {
        HashSet<Tile> ends = [.. goals.Where(goal => !blocked(goal))];

        if (goals.Contains(from))
        {
            return [];
        }

        if (ends.Count == 0)
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

                if (ends.Contains(next))
                {
                    List<Tile> way = [];

                    for (Tile at = next; at != from; at = cameFrom[at])
                    {
                        way.Add(at);
                    }

                    way.Reverse();

                    return way;
                }

                edge.Enqueue((next, steps + 1));
            }
        }

        return null;
    }

    /// <summary>Which way the first step of a way goes.</summary>
    public static Direction StepOf(Tile from, Tile next) =>
        next.X > from.X ? Direction.East
        : next.X < from.X ? Direction.West
        : next.Y > from.Y ? Direction.South
        : Direction.North;

    /// <summary>
    /// The tiles beside somebody — they stand on their own tile, and walking up to them is arriving at one of these.
    /// </summary>
    private static Tile[] Beside(Tile where) => [.. Around(where)];

    private static IEnumerable<Tile> Around(Tile tile)
    {
        yield return new Tile(tile.X, tile.Y - 1);
        yield return new Tile(tile.X + 1, tile.Y);
        yield return new Tile(tile.X, tile.Y + 1);
        yield return new Tile(tile.X - 1, tile.Y);
    }
}
