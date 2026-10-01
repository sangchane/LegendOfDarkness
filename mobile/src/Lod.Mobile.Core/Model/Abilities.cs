namespace Lod.Mobile.Core.Model;

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
