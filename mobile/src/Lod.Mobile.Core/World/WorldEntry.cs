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
