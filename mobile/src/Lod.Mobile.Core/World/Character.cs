namespace Lod.Mobile.Core.World;

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
/// <param name="Count">
/// How many a thing on the floor holds (a bundle of potions), from the four bytes the server used to leave
/// empty. Zero for monsters, merchants and gold.
/// </param>
public sealed record Creature(
    uint Serial,
    Tile Where,
    Art.Direction Facing,
    int Sprite,
    CreatureKind Kind,
    string Name,
    int Count = 0);

/// <summary>
/// A skill's flash, as the server sends it (0x29). On somebody the first animation plays over
/// <paramref name="Target" /> and the second over <paramref name="Source" />; on the ground both serials are zero
/// and <paramref name="At" /> says where.
/// </summary>
/// <param name="Speed">How long each frame stays, in milliseconds as the original counts them.</param>
/// <summary>A body motion (0x1A): whose, which motion (see <c>Art.BodyMotion</c>) and how fast.</summary>
public sealed record Motion(uint Serial, int Number, int Speed);

public sealed record Effect(uint Target, uint Source, int TargetAnimation, int SourceAnimation, int Speed, Tile? At);

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
