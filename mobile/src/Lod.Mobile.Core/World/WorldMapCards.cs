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
}
