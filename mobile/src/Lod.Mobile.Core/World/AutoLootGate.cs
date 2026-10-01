namespace Lod.Mobile.Core.World;

/// <summary>
/// Decides which floor object may be requested by walk-over loot.
/// </summary>
/// <remarks>
/// A successful pickup is removed from <see cref="WorldClient.Creatures"/> by its 0x0E reply.  Until
/// that reply arrives, an item must only be asked for once: a full pack and owner-only drops otherwise
/// make the same request every frame.  The gate deliberately does not retry a still-present object;
/// tapping it remains the explicit retry after the player changes their inventory.
/// </remarks>
public sealed class AutoLootGate
{
    private readonly HashSet<uint> _requested = [];

    // The tile we just threw something on. What lands there while we still stand on it is ours, thrown away.
    private Tile? _thrown;

    /// <summary>
    /// We threw something at our feet. It is not to be picked straight back up — not now, nor on walking back over it
    /// while it stays in sight; tapping it still picks it up.
    /// </summary>
    public void Threw(Tile where) => _thrown = where;

    /// <summary>
    /// Returns one object standing under the player that has not already been requested, or no request.
    /// Call this once per frame with the server's current floor-object snapshot.
    /// </summary>
    public Tile? Next(bool enabled, Tile standing, IEnumerable<Creature> creatures)
    {
        Creature[] floor = creatures.Where(one => one.Kind == CreatureKind.Passable).ToArray();
        _requested.IntersectWith(floor.Select(one => one.Serial));

        if (_thrown is { } thrown)
        {
            if (thrown == standing)
            {
                _requested.UnionWith(floor.Where(one => one.Where == thrown).Select(one => one.Serial));
            }
            else
            {
                _thrown = null;
            }
        }

        if (!enabled)
        {
            return null;
        }

        Creature? candidate = floor
            .Where(one => one.Where == standing && !_requested.Contains(one.Serial))
            .OrderBy(one => one.Serial)
            .FirstOrDefault();

        if (candidate is null)
        {
            return null;
        }

        _requested.Add(candidate.Serial);
        return candidate.Where;
    }
}
