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
/// The six attacking and defending kinds. A blow carries the attacker's and meets the defender's, and the
/// pair decides how much of it lands — the table is <c>scripts/Formulas/elements.cs</c>. Two Nones are
/// worth a half, which is the pair every fresh character and every ported monster starts with.
/// </summary>
public enum Element : byte
{
    None = 0,
    Fire = 1,
    Water = 2,
    Wind = 3,
    Earth = 4,
    Light = 5,
    Dark = 6,

    /// <summary>Rolled fresh every time it is read, so a sprite holding this is never the same twice.</summary>
    Random = 7
}

/// <summary>
/// Our own character's numbers, as the server states them. Nobody else's — the server says only how hurt
/// other people are, out of a hundred.
/// </summary>
/// <remarks>
/// The server sends this in four independent pieces and includes only the ones that changed, so these
/// fields are the newest value it has stated for each: the standing figures (level, the five attributes,
/// the maxima), what is left of health and mana, what has been earned, and the fighting figures. A field
/// the server has not spoken about yet is zero.
/// </remarks>
/// <param name="Armor">
/// Lower is better, and negative is normal once gear is on. It does not subtract from a blow in this
/// server — <c>scripts/Formulas/ac.cs</c> returns the larger of the blow and the armoured blow — so a
/// monster's +69 makes it take about two and a half times what it otherwise would.
/// </param>
/// <param name="MagicResistance">Already divided by ten on the wire, which is how the pane shows it.</param>
/// <param name="Unspent">Attribute points waiting to be spent. Zero unless the server said otherwise.</param>
public sealed record Vitals(
    int Level,
    int AbilityLevel,
    int Health,
    int MaximumHealth,
    int Mana,
    int MaximumMana,
    int Str,
    int Int,
    int Wis,
    int Con,
    int Dex,
    int Unspent,
    int Weight,
    int MaximumWeight,
    long Experience,
    long ExperienceToGo,
    long AbilityExperience,
    long AbilityExperienceToGo,
    long GamePoints,
    long Gold,
    Element Offense,
    Element Defense,
    int MagicResistance,
    int Armor,
    int Damage,
    int Hit,
    bool Blind)
{
    /// <summary>What we hold before the server has said anything at all.</summary>
    public static readonly Vitals Unknown = new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        Element.None, Element.None, 0, 0, 0, 0, false);
}

/// <summary>What kind of thing the server is showing, which decides how the rest of it is read.</summary>
/// <summary>
/// One of the five attributes, numbered the way the raise-a-stat packet numbers them.
/// </summary>
/// <remarks>
/// Named <c>Stat</c> rather than <c>Attribute</c>, which is what the server calls it: an enum called
/// <c>Attribute</c> collides with <c>System.Attribute</c> in every file that has both namespaces open,
/// and the error it gives is about ambiguity rather than about the collision.
/// </remarks>
/// <remarks>
/// A level hands out <c>StatsPerLevel</c> points and each of these spends one. The numbers are flags in
/// the original and the server tests them as flags (<c>Format47Handler</c>), but it only ever spends one
/// point per packet, so sending two at once raises two attributes for the price of one — which is why
/// nothing here combines them.
/// </remarks>
public enum Stat : byte
{
    /// <summary>Strength. Most of what a blow is worth comes from here.</summary>
    Str = 0x01,

    /// <summary>Dexterity.</summary>
    Dex = 0x02,

    /// <summary>Intelligence.</summary>
    Int = 0x04,

    /// <summary>Wisdom. Maximum mana grows by this at every level.</summary>
    Wis = 0x08,

    /// <summary>Constitution. Maximum health grows by this at every level.</summary>
    Con = 0x10,
}

/// <summary>
/// Something that is on us — a curse, poison, sleep. The server names it by a picture number and grades how
/// much longer it lasts rather than counting it down (<c>Debuff.Display</c>): 6 is over ninety seconds, 5 is
/// sixty to ninety, 4 thirty to sixty, 3 twenty to thirty, 2 ten to twenty, 1 under ten. Zero means it is over.
/// </summary>
/// <param name="Left">
/// That grade. It is not seconds — the original never sends seconds for these, only which band it is in.
/// </param>
public sealed record Ailment(int Icon, int Left)
{
    /// <summary>Roughly how many seconds are left, for showing. The band's own floor, which never overstates.</summary>
    public int Seconds => Left switch
    {
        6 => 90,
        5 => 60,
        4 => 30,
        3 => 20,
        2 => 10,
        1 => 1,
        _ => 0
    };
}

/// <summary>
/// Something on somebody else — another player or a monster — as our own 0x5C tells it to everyone who can see
/// them. The original has no such packet: it only ever tells the afflicted one (0x3A). See
/// <c>ServerFormat5C</c> in the server fork.
/// </summary>
/// <param name="Left">The same time grade as <see cref="Ailment.Left" />; 0 means it is over.</param>
/// <param name="Harmful">A debuff rather than a buff. Only these tint a monster.</param>
/// <param name="Effect">
/// The picture the spell drew on them (<c>efct###</c>), whose colour a monster is tinted in; 0 when the server
/// does not know it.
/// </param>
public sealed record SeenAilment(uint Serial, int Icon, int Left, bool Harmful, int Effect)
{
    /// <summary>The badge this makes under a person's health bar.</summary>
    public Ailment Badge => new(Icon, Left);
}

/// <summary>
/// How much one blow took or one heal gave (0x5D, our server's own packet — the original only sends a percentage,
/// 0x13). See <c>ServerFormat5D</c> in the server fork.
/// </summary>
/// <param name="Target">Whose health changed.</param>
/// <param name="Source">Who did it; 0 when the server does not say.</param>
/// <param name="Amount">What really changed, after armour and the cap.</param>
public sealed record Figure(uint Target, uint Source, int Amount, FigureKind Kind);

public enum FigureKind
{
    Damage = 0,
    Heal = 1,
}

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

/// <summary>How something was said (0x0D) — the numbers are Hades' own <c>ServerFormat0D.MsgType</c>.</summary>
public enum SpeechKind : byte
{
    Normal = 0,
    Shout = 1,

    /// <summary>A spell said aloud as it is cast, not somebody talking.</summary>
    Chant = 2
}

/// <summary>A line somebody near us said (0x0D). Who they are is looked up by <paramref name="Serial" />.</summary>
public sealed record Spoken(SpeechKind Kind, uint Serial, string Text);

/// <summary>
/// How long until one skill or spell may be used again (0x3F). <paramref name="Skill" /> tells the two panes apart —
/// both count their slots from one.
/// </summary>
public sealed record Cooldown(bool Skill, int Slot, int Seconds)
{
    /// <summary>Whole seconds left before <paramref name="ready" />, never less than none.</summary>
    public static int Left(DateTime ready, DateTime now) => (int)Math.Max(0, Math.Ceiling((ready - now).TotalSeconds));
}

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

/// <summary>One learned technique in the character's skill pane.</summary>
/// <param name="Slot">The server-owned pane slot used again when the skill is activated.</param>
/// <param name="Icon">A zero-based frame in <c>skill001.epf</c>.</param>
public sealed record LearnedSkill(int Slot, int Icon, string Name);

/// <summary>How the original client asks for the argument to a learned spell.</summary>
public enum SpellTargetType : byte
{
    Unusable = 0,
    Prompt = 1,
    ChooseTarget = 2,
    FourDigit = 3,
    ThreeDigit = 4,
    NoTarget = 5,
    TwoDigit = 6,
    OneDigit = 7
}

/// <summary>One learned spell in the character's spell pane.</summary>
/// <param name="Icon">A zero-based frame in <c>spell001.epf</c>.</param>
/// <param name="Prompt">The server-provided hint shown when the spell needs typed data.</param>
public sealed record LearnedSpell(
    int Slot,
    int Icon,
    SpellTargetType TargetType,
    string Name,
    string Prompt,
    int Lines);

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
/// screen shows <paramref name="What"/>. Below the words is whatever the <see cref="Kind" /> carries.
/// </summary>
public sealed record Dialogue(uint Serial, string Who, string What)
{
    public DialogueKind Kind { get; init; }

    /// <summary>
    /// The number an answer picked from <see cref="Goods" />, <see cref="Slots" /> or <see cref="Abilities" />
    /// goes back with, or the one typed words go back with. A choice from <see cref="Options" /> has its own.
    /// </summary>
    public ushort Step { get; init; }

    /// <summary>What the NPC wants handed back with the answer — the thing a deal is about.</summary>
    public string Args { get; init; } = string.Empty;

    public IReadOnlyList<DialogueOption> Options { get; init; } = [];

    public IReadOnlyList<DialogueGoods> Goods { get; init; } = [];

    /// <summary>Pack slots to pick one of — what can be sold or put in the bank.</summary>
    public IReadOnlyList<int> Slots { get; init; } = [];

    public IReadOnlyList<DialogueAbility> Abilities { get; init; } = [];

    /// <summary>Why what the window carries under its words stops short, or null when it was read to the end.</summary>
    public string? Unread { get; init; }
}

/// <summary>What an NPC's window holds under its words — the first byte of 0x2F (Hades <c>IDialogData.Type</c>).</summary>
public enum DialogueKind : byte
{
    /// <summary>Choices, each answered with its own number. None at all when the NPC only speaks.</summary>
    Options = 0x00,

    /// <summary>Choices, and something to hand back with whichever is taken.</summary>
    OptionsWithArgs = 0x01,

    /// <summary>Words to type.</summary>
    TextInput = 0x02,

    /// <summary>Things to buy, or to take out of the bank.</summary>
    Goods = 0x04,

    /// <summary>Slots of our own pack — to sell, or to put in the bank.</summary>
    PackSlots = 0x05,

    Spells = 0x06,
    Skills = 0x07,
    ForgetSpell = 0x08,
    ForgetSkill = 0x09
}

public sealed record DialogueOption(string Text, ushort Step);

/// <summary>One thing on offer. <paramref name="Price" /> is gold in a shop and how many there are in a bank.</summary>
public sealed record DialogueGoods(int Icon, int Colour, uint Price, string Name);

public sealed record DialogueAbility(int Icon, string Name);
