namespace Lod.Mobile.Core.World;

/// <summary>
/// One card on the world map: the place's name as the server gives it, and what the client knows beside it — a town or
/// a hunting ground, the map it lands on, and the level asked to go in (0 when none above 1 is asked).
/// </summary>
public sealed record WorldMapCard(string Name, int AreaId, string Kind, string Arrival, int Level);

/// <summary>Turns the server's world map (0x2E) into cards. The server says only names and map numbers; the rest is <c>guide.txt</c>'s.</summary>
public static class WorldMapCards
{
    public static IReadOnlyList<WorldMapCard> From(WorldMapInfo field, MapGuide guide) =>
    [
        .. field.Nodes.Select(node => guide.Place(node.AreaId) is { } place
            ? new WorldMapCard(node.Name, node.AreaId, place.Town ? "마을" : "사냥터", place.Name, place.Level > 1 ? place.Level : 0)
            : new WorldMapCard(node.Name, node.AreaId, string.Empty, string.Empty, 0))
    ];

    /// <summary>Whether a card is a town — the guide says so, or, for a place it does not know, "마을" is in its name.</summary>
    public static bool IsTown(WorldMapCard card) => card.Kind.Length > 0 ? card.Kind == "마을" : card.Name.Contains("마을", StringComparison.Ordinal);

    /// <summary>The [마을] tab: towns, in the server's order.</summary>
    public static IReadOnlyList<WorldMapCard> Towns(IReadOnlyList<WorldMapCard> cards) => [.. cards.Where(IsTown)];

    /// <summary>The [사냥터] tab: the rest, lowest entry level first (ties keep the server's order).</summary>
    public static IReadOnlyList<WorldMapCard> Fields(IReadOnlyList<WorldMapCard> cards) =>
        [.. cards.Where(card => !IsTown(card)).OrderBy(card => card.Level)];

    /// <summary>
    /// Which tab opens first: the kind of place we stand in ("마을" in its name, or no name yet — a town), unless that tab
    /// would be empty.
    /// </summary>
    public static bool OpensOnTowns(string standingIn, IReadOnlyList<WorldMapCard> cards)
    {
        bool town = standingIn.Length == 0 || standingIn.Contains("마을", StringComparison.Ordinal);

        return town ? Towns(cards).Count > 0 : Fields(cards).Count == 0;
    }
}
