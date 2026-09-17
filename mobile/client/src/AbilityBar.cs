using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// The attack button with the learned skills and spells fanned round it, six to a page (<see cref="AbilityFan" />).
/// The pictures are the original <c>skill001.epf</c>/<c>spell001.epf</c> frames decoded with <c>gui06.pal</c>.
/// </summary>
/// <remarks>
/// The box lets taps through wherever there is no button: in landscape it lies over the floor, and a figure standing in
/// a gap of the fan must still be pickable.
/// </remarks>
public sealed partial class AbilityBar : Control
{
    private const int IconSide = 35;
    private const int SheetColumns = 16;
    internal const string SkillSheet = "res://assets/ability/skill.png";
    internal const string SpellSheet = "res://assets/ability/spell.png";

    private readonly Button[] _slots = new Button[AbilityFan.PerPage];
    private readonly Button _switch = Disc("기술", AbilityFan.ButtonSide);
    private readonly Button _next = Disc("1/1", AbilityFan.ButtonSide);

    // 지금 칸에 그려 둔 것. 서버 목록은 프레임마다 새로 만들어지므로, 내용이 같으면 다시 그리지 않는다.
    private object?[] _drawn = new object?[AbilityFan.PerPage];
    private bool _drawnSpells;
    private string _drawnPage = string.Empty;

    private bool _spells;
    private int _page;
    private IReadOnlyList<LearnedSkill> _learnedSkills = [];
    private IReadOnlyList<LearnedSpell> _learnedSpells = [];

    /// <summary>One tap is one blow — see <see cref="WorldView.Strike" />.</summary>
    public Button Attack { get; } = Disc("공격", AbilityFan.AttackSide);

    public event Action<int>? SkillUsed;
    public event Action<int>? SpellUsed;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(AbilityFan.Width, AbilityFan.Height);
        MouseFilter = MouseFilterEnum.Ignore;

        Place(Attack, AbilityFan.Attack, AbilityFan.AttackSide);

        // 누른 그 자리에서 다시 그린다 — 다음 프레임까지 기다리면 그사이 누른 칸이 앞 장의 것을 쓴다.
        _switch.Pressed += () =>
        {
            _spells = !_spells;
            _page = 0;
            Redraw();
        };
        Place(_switch, AbilityFan.Switch, AbilityFan.ButtonSide);

        _next.Pressed += () =>
        {
            _page = AbilityFan.After(_page, _spells ? _learnedSpells.Count : _learnedSkills.Count);
            Redraw();
        };
        Place(_next, AbilityFan.Next, AbilityFan.ButtonSide);

        for (int index = 0; index < _slots.Length; index++)
        {
            int which = index;
            Button slot = Disc(string.Empty, AbilityFan.ButtonSide);
            slot.ExpandIcon = true;
            slot.IconAlignment = HorizontalAlignment.Center;
            slot.Disabled = true;
            slot.Pressed += () => Use(which);

            _slots[index] = slot;
            Place(slot, AbilityFan.Slots[index], AbilityFan.ButtonSide);
        }
    }

    /// <summary>Redraws the page only when what is on it has changed, so loading textures is not a per-frame job.</summary>
    public void Show(IReadOnlyList<LearnedSkill> skills, IReadOnlyList<LearnedSpell> spells)
    {
        _learnedSkills = skills;
        _learnedSpells = spells;
        Redraw();
    }

    private void Redraw()
    {
        int learned = _spells ? _learnedSpells.Count : _learnedSkills.Count;
        _page = AbilityFan.Kept(_page, learned);

        object?[] page = _spells ? [.. AbilityFan.Page(_learnedSpells, _page)] : [.. AbilityFan.Page(_learnedSkills, _page)];
        string pages = $"{_page + 1}/{AbilityFan.Pages(learned)}";

        if (_drawnSpells == _spells && _drawnPage == pages && page.SequenceEqual(_drawn))
        {
            return;
        }

        _drawn = page;
        _drawnSpells = _spells;
        _drawnPage = pages;

        _switch.Text = _spells ? "마법" : "기술";
        _next.Text = pages;
        _next.Disabled = AbilityFan.Pages(learned) == 1;

        for (int index = 0; index < _slots.Length; index++)
        {
            (string name, int? icon) = page[index] switch
            {
                LearnedSkill skill => (skill.Name, skill.Icon),
                LearnedSpell spell => (spell.Name, spell.Icon),
                _ => (string.Empty, (int?)null)
            };

            _slots[index].TooltipText = name;
            _slots[index].Disabled = icon is null;

            // 빈 칸은 자리만 알린다. 배운 것이 적으면 짙은 원 다섯이 바닥을 가렸다.
            _slots[index].Modulate = icon is null ? new Color(1, 1, 1, 0.4f) : Colors.White;
            _slots[index].Icon = icon is { } frame ? Frame(_spells ? SpellSheet : SkillSheet, frame) : null;
        }
    }

    private void Use(int index)
    {
        switch (_drawn[index])
        {
            case LearnedSkill skill:
                SkillUsed?.Invoke(skill.Slot);
                break;
            case LearnedSpell spell:
                SpellUsed?.Invoke(spell.Slot);
                break;
        }
    }

    private void Place(Button button, (int X, int Y) centre, int side)
    {
        button.Position = new Vector2(centre.X - (side / 2), centre.Y - (side / 2));
        button.Size = new Vector2(side, side);
        AddChild(button);
    }

    private static Button Disc(string text, int side)
    {
        Button button = new ThumbButton
        {
            Text = text,
            CustomMinimumSize = new Vector2(side, side),
            ClipText = true
        };

        Greybox.Disc(button);

        return button;
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
