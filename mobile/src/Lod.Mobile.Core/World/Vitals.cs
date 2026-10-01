namespace Lod.Mobile.Core.World;

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

/// <summary>
/// How long until one skill or spell may be used again (0x3F). <paramref name="Skill" /> tells the two panes apart —
/// both count their slots from one.
/// </summary>
public sealed record Cooldown(bool Skill, int Slot, int Seconds)
{
    /// <summary>Whole seconds left before <paramref name="ready" />, never less than none.</summary>
    public static int Left(DateTime ready, DateTime now) => (int)Math.Max(0, Math.Ceiling((ready - now).TotalSeconds));
}
