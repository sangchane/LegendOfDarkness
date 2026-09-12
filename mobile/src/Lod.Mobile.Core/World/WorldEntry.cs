using System.Buffers.Binary;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;

namespace Lod.Mobile.Core.World;

/// <summary>Which map the character is standing on, and how big it is in tiles.</summary>
public sealed record MapInfo(int Id, int Columns, int Rows, string Name);

/// <summary>A tile on that map. Not pixels — the client works out where a tile lands on screen.</summary>
public readonly record struct Tile(int X, int Y);

/// <summary>What the server says as soon as a character is admitted: where it is and on what.</summary>
public sealed record WorldEntry(MapInfo Map, Tile Where);

/// <summary>
/// What somebody is wearing. Every number here names a drawing: the gender letter, the part letter, the
/// number padded to three digits, then the action — <c>mh285</c> is a man's helmet 285. The letters are in
/// docs/original-sprite-animation.md section 7.
/// </summary>
/// <remarks>
/// <see cref="Head" /> is the helmet when one is worn and the hair otherwise; the server decides which and
/// does not say. Colours are palette rows, not parts.
/// </remarks>
public sealed record Appearance(
    int Head,
    int Body,
    int Armor,
    int Boots,
    int Shield,
    int Weapon,
    int HairColor,
    int BootColor,
    int HeadAccessory1,
    int Lantern,
    int HeadAccessory2,
    int Resting,
    int OverCoat);

/// <summary>What kind of thing the server is showing, which decides how the rest of it is read.</summary>
public enum CreatureKind
{
    /// <summary>A monster. It fights.</summary>
    Hostile = 0,

    /// <summary>Something that can be walked through.</summary>
    Passable = 1,

    /// <summary>A merchant or other standing character. This one is named.</summary>
    Merchant = 2
}

/// <summary>
/// A monster, a merchant, or anything else the server puts on the floor beside the players.
/// </summary>
/// <param name="Sprite">
/// Which drawing, in the monster archive's own numbering — not the wardrobe numbering that dresses people.
/// </param>
public sealed record Creature(
    uint Serial,
    Tile Where,
    Art.Direction Facing,
    int Sprite,
    CreatureKind Kind,
    string Name);

/// <summary>
/// One thing in a character's pack. The server sends these one at a time, both on the way in and whenever
/// something is picked up.
/// </summary>
/// <param name="Icon">
/// Which picture to draw for it — the same number whether the thing is carried, worn or lying on the floor.
/// </param>
public sealed record InventoryItem(
    int Slot,
    int Icon,
    int Colour,
    string Name,
    int Stacks,
    int Durability,
    int MaxDurability);

/// <summary>
/// A piece of gear the character has on. The server names the place it sits by number — the same numbers
/// the item templates use in <c>EquipmentSlot</c> — and says nothing about what that place looks like.
/// </summary>
/// <param name="Slot">Where it is worn: 1 weapon, 2 armour, 3 shield, 4 helmet … 13 boots. See <see cref="WornPlace"/>.</param>
/// <param name="Name">What the item is called. <paramref name="Called"/> is that name after any upgrade is spelled into it.</param>
public sealed record WornItem(
    int Slot,
    int Icon,
    string Name,
    string Called,
    long Durability,
    long MaxDurability);

/// <summary>
/// The names of the places gear is worn, so a screen can say "신발" rather than "13". Straight from the
/// server's own <c>ItemSlots</c>; the gaps in the middle are the server's, not ours.
/// </summary>
public static class WornPlace
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [1] = "무기", [2] = "갑옷", [3] = "방패", [4] = "투구", [5] = "귀고리",
        [6] = "목걸이", [7] = "왼손", [8] = "오른손", [9] = "왼팔", [10] = "오른팔",
        [11] = "허리", [12] = "다리", [13] = "신발", [14] = "장신구", [15] = "겉옷",
        [16] = "겉투구", [17] = "장신구2",
    };

    /// <summary>The name of one place, or the number itself when the server uses one we do not know.</summary>
    public static string Of(int slot) => Names.TryGetValue(slot, out string? called) ? called : slot.ToString();
}

/// <summary>
/// Somebody else standing in the world: where they are, which way they face, what they wear and what they
/// are called. A character who is dead, or who has taken a monster's shape, arrives without a wardrobe.
/// </summary>
public sealed record Character(
    uint Serial,
    Tile Where,
    Art.Direction Facing,
    Appearance? Wearing = null,
    string Name = "");

/// <summary>
/// One window of NPC speech. <paramref name="Who"/> is the template's own name, which for ported content
/// carries the placement (<c>가렌@밀레스마을#52,43</c>) because the server keys its NPCs by name — the
/// screen shows <paramref name="What"/>.
/// </summary>
public sealed record Dialogue(uint Serial, string Who, string What);
