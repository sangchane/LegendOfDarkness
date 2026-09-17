using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// Two thumb-sized live shortcuts: the first learned skill and spell. The pictures are the original
/// <c>skill001.epf</c>/<c>spell001.epf</c> frames decoded with <c>gui06.pal</c>.
/// </summary>
public sealed partial class AbilityBar : HBoxContainer
{
    private const int IconSide = 35;
    private const int SheetColumns = 16;
    internal const string SkillSheet = "res://assets/ability/skill.png";
    internal const string SpellSheet = "res://assets/ability/spell.png";

    private readonly Button _skill = Shortcut("기술");
    private readonly Button _spell = Shortcut("마법");

    private LearnedSkill? _shownSkill;
    private LearnedSpell? _shownSpell;

    public event Action<int>? SkillUsed;
    public event Action<int>? SpellUsed;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", Main.Gutter / 2);
        SizeFlagsVertical = SizeFlags.ShrinkEnd;

        _skill.Pressed += () =>
        {
            if (_shownSkill is { } skill)
            {
                SkillUsed?.Invoke(skill.Slot);
            }
        };

        _spell.Pressed += () =>
        {
            if (_shownSpell is { } spell)
            {
                SpellUsed?.Invoke(spell.Slot);
            }
        };

        AddChild(_skill);
        AddChild(_spell);
    }

    /// <summary>Refreshes only when a slot changes, so loading textures is not a per-frame job.</summary>
    public void Show(IReadOnlyList<LearnedSkill> skills, IReadOnlyList<LearnedSpell> spells)
    {
        LearnedSkill? skill = skills.FirstOrDefault();
        LearnedSpell? spell = spells.FirstOrDefault();

        if (skill != _shownSkill)
        {
            _shownSkill = skill;
            Set(_skill, skill?.Name ?? "배운 기술 없음", skill?.Icon, SkillSheet);
        }

        if (spell != _shownSpell)
        {
            _shownSpell = spell;
            Set(_spell, spell?.Name ?? "배운 마법 없음", spell?.Icon, SpellSheet);
        }
    }

    private static Button Shortcut(string text) => new()
    {
        Text = text,
        TooltipText = text,
        CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
        Disabled = true,
        ExpandIcon = true
    };

    private static void Set(Button button, string name, int? icon, string sheetPath)
    {
        button.TooltipText = name;
        button.Disabled = icon is null;
        button.Text = icon is null ? (sheetPath == SkillSheet ? "기술" : "마법") : string.Empty;
        button.Icon = icon is { } frame ? Frame(sheetPath, frame) : null;
    }

    internal static AtlasTexture Frame(string sheetPath, int frame)
    {
        int safe = Math.Max(0, frame);

        return new AtlasTexture
        {
            Atlas = GD.Load<Texture2D>(sheetPath),
            Region = new Rect2(
                (safe % SheetColumns) * IconSide,
                (safe / SheetColumns) * IconSide,
                IconSide,
                IconSide)
        };
    }
}
