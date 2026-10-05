namespace Lod.Mobile.Core.Model;

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
public sealed record DialogueGoods(int Icon, int Colour, uint Price, string Name, string Class = "", byte Gender = 255, byte Circle = 0, ItemStats? Stats = null);

public sealed record DialogueAbility(int Icon, string Name);
