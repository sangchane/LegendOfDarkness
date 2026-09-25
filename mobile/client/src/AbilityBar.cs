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
    private readonly Label[] _waits = new Label[AbilityFan.PerPage];
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

    // 0.5초 길게 누르면 배치 목록을 연다(사용자 요청, 2026-09-25) — 뗄 때 쓰는 지금 방식(action_mode 기본값)이라
    // 길게 눌림이 잡히면 Use()에서 취소한다. 짧게 누르면 그대로 뗄 때 바로 쓰여 늦어지지 않는다.
    private const ulong HoldMilliseconds = 500;
    private readonly ulong[] _downAt = new ulong[AbilityFan.PerPage];
    private readonly bool[] _down = new bool[AbilityFan.PerPage];
    private readonly bool[] _longHeld = new bool[AbilityFan.PerPage];
    private int _rehearsedHold; // --slot-hold: 손 없이 확인할 때 프레임을 센다.

    // 슬롯 배치(사용자 요청) — 캐릭터 이름별로 기기 안에 저장한다(Main.LoadAbilitySlots/SaveAbilitySlots).
    private AbilityArrangement _skillArrangement = new();
    private AbilityArrangement _spellArrangement = new();
    private string _loadedFor = string.Empty;
    private readonly PopupPanel _picker = new();
    private readonly VBoxContainer _pickerList = new();
    private readonly ScrollContainer _pickerScroll = new();

    /// <summary>Given a character's name, the saved lines for their slots (empty if none yet).</summary>
    public Func<string, IEnumerable<string>>? LoadSlots { get; set; }

    /// <summary>Given a character's name and the lines to keep, saves the slot arrangement.</summary>
    public Action<string, IReadOnlyList<string>>? SaveSlots { get; set; }

    /// <summary>One tap is one blow — see <see cref="WorldView.Strike" />.</summary>
    /// <summary>
    /// 공격 단추. 시안에서 유일하게 돌로 남긴 조작이다 — 창의 확정 단추와 같은 자리다(data/ui-vault 안C).
    /// </summary>
    public Button Attack { get; } = Struck("공격", AbilityFan.AttackSide);

    /// <summary>How many seconds one slot still has to wait, asked of the server every frame.</summary>
    public Func<bool, int, int>? Cooling { get; set; }

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

            // 칸을 계속 눌러 둘 수 있어야 길게 눌러 배치를 바꿀 수 있다 — 빈 칸·식는 중에도 배치는 바꿀 수 있어야
            // 하므로, "쓸 수 있나"는 더는 Disabled 가 아니라 Use() 안에서 가린다(아래).
            slot.ButtonDown += () => OnSlotDown(which);
            slot.ButtonUp += () => OnSlotUp(which);
            slot.Pressed += () => Use(which);

            _slots[index] = slot;
            Place(slot, AbilityFan.Slots[index], AbilityFan.ButtonSide);

            // 남은 초는 그림 위에 겹쳐 적는다. 칸이 48 이라 그림 옆에 글자를 둘 자리가 없다.
            Label waiting = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            waiting.SetAnchorsPreset(LayoutPreset.FullRect);
            waiting.AddThemeColorOverride("font_color", Colors.White);
            waiting.AddThemeStyleboxOverride("normal", Greybox.Shade());

            _waits[index] = waiting;
            slot.AddChild(waiting);
        }

        // 길게 누르면 뜨는 배치 목록 — 목록 밖을 누르면 닫힌다(PopupPanel 기본 동작). 돌을 쓰지 않는 목록이다
        // (docs/original-ui-451.md: "돌을 안 쓰는 곳 — 목록").
        _pickerList.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _pickerScroll.CustomMinimumSize = new Vector2(PickerWidth, 0);
        _pickerScroll.AddChild(_pickerList);
        _picker.AddChild(_pickerScroll);
        AddChild(_picker);
    }

    /// <summary>
    /// Counts down whatever is still cooling. The server says how long when a skill is used (0x3F); until it is ready the
    /// slot says how many seconds are left and takes no press.
    /// </summary>
    public override void _Process(double delta)
    {
        for (int index = 0; index < _slots.Length; index++)
        {
            int slot = _drawn[index] switch
            {
                LearnedSkill skill => skill.Slot,
                LearnedSpell spell => spell.Slot,
                _ => 0
            };

            int left = slot > 0 && Cooling is { } ask ? ask(!_drawnSpells, slot) : 0;

            _waits[index].Visible = left > 0;
            _waits[index].Text = left > 0 ? left.ToString() : string.Empty;

            // 0.5초를 채우면 길게 누른 것으로 치고 배치 목록을 연다. 빈 칸·식는 중에도 열려야 하므로 여기서는
            // Disabled 를 보지 않는다(짧게 눌렀을 때 쓰는지는 Use() 가 가린다).
            if (_down[index] && !_longHeld[index] && Time.GetTicksMsec() - _downAt[index] >= HoldMilliseconds)
            {
                _longHeld[index] = true;
                OpenPicker(index);
            }
        }

        // --slot-hold N: 서버 없이 확인할 때, 자리를 잡고 잠시 뒤 N번째 칸을 길게 누른 셈 친다.
        if (Main.SlotHold > 0 && Main.SlotHold <= _slots.Length && !_longHeld[Main.SlotHold - 1] && ++_rehearsedHold == 90)
        {
            _longHeld[Main.SlotHold - 1] = true;
            OpenPicker(Main.SlotHold - 1);
        }
    }

    /// <summary>
    /// Redraws the page only when what is on it has changed, so loading textures is not a per-frame job. The
    /// character's name (once known) picks which saved slot arrangement to read, one time.
    /// </summary>
    public void Show(IReadOnlyList<LearnedSkill> skills, IReadOnlyList<LearnedSpell> spells, string character = "")
    {
        if (character.Length > 0 && character != _loadedFor)
        {
            _loadedFor = character;
            _skillArrangement = new AbilityArrangement();
            _spellArrangement = new AbilityArrangement();

            if (LoadSlots?.Invoke(character) is { } lines)
            {
                AbilitySlotSave.Parse(lines, _skillArrangement, _spellArrangement);
            }
        }

        _learnedSkills = skills;
        _learnedSpells = spells;
        Redraw();
    }

    private void Redraw()
    {
        int learned = _spells ? _learnedSpells.Count : _learnedSkills.Count;
        _page = AbilityFan.Kept(_page, learned);

        int capacity = AbilityFan.Pages(learned) * AbilityFan.PerPage;

        object?[] page = _spells
            ? [.. Slice(_spellArrangement.Fill(_learnedSpells, spell => spell.Slot, capacity), _page)]
            : [.. Slice(_skillArrangement.Fill(_learnedSkills, skill => skill.Slot, capacity), _page)];

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

            // 빈 칸은 자리만 알린다. 배운 것이 적으면 짙은 원 다섯이 바닥을 가렸다. 더는 Disabled 로 가리지
            // 않는다 — 빈 칸도 길게 누르면 배치를 받아야 한다(위 _Process, Use() 참고).
            _slots[index].Modulate = icon is null ? new Color(1, 1, 1, 0.4f) : Colors.White;
            _slots[index].Icon = icon is { } frame ? Frame(_spells ? SpellSheet : SkillSheet, frame) : null;
        }
    }

    private static IEnumerable<T?> Slice<T>(IReadOnlyList<T?> all, int page) =>
        all.Skip(page * AbilityFan.PerPage).Take(AbilityFan.PerPage);

    /// <summary>Presses one slot from outside — for a run with nobody watching (<c>--skill 1</c>, <c>--skill m1</c> for a spell).</summary>
    public void Press(int index, bool spell = false)
    {
        if (_spells != spell)
        {
            _spells = spell;
            _page = 0;
            Redraw();
        }

        if (index >= 0 && index < _slots.Length)
        {
            Use(index);
        }
    }

    private void OnSlotDown(int index)
    {
        _down[index] = true;
        _longHeld[index] = false;
        _downAt[index] = Time.GetTicksMsec();
    }

    private void OnSlotUp(int index) => _down[index] = false;

    private void Use(int index)
    {
        // 길게 눌러 배치 목록이 이미 열렸으면, 손을 뗄 때 오는 이 누름은 취소한다 — 길게 누른 것은 쓰지 않는다.
        if (_longHeld[index])
        {
            _longHeld[index] = false;
            return;
        }

        int slot = _drawn[index] switch
        {
            LearnedSkill skill => skill.Slot,
            LearnedSpell spell => spell.Slot,
            _ => 0
        };

        // 빈 칸, 또는 식는 중 — 예전에는 Disabled 가 막았지만 이제 그 칸도 길게 누를 수 있어야 해서 여기서 가린다.
        if (slot == 0 || (Cooling is { } ask && ask(!_drawnSpells, slot) > 0))
        {
            return;
        }

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

    /// <summary>What the bar is showing at a given (page, index) position of one kind, regardless of which tab is open now.</summary>
    private int? DisplayedSlotAt(bool spells, int position)
    {
        int learned = spells ? _learnedSpells.Count : _learnedSkills.Count;
        int capacity = AbilityFan.Pages(learned) * AbilityFan.PerPage;

        if (position < 0 || position >= capacity)
        {
            return null;
        }

        object? shown = spells
            ? _spellArrangement.Fill(_learnedSpells, spell => spell.Slot, capacity)[position]
            : _skillArrangement.Fill(_learnedSkills, skill => skill.Slot, capacity)[position];

        return shown switch
        {
            LearnedSkill skill => skill.Slot,
            LearnedSpell spell => spell.Slot,
            _ => null
        };
    }

    /// <summary>
    /// Opens the picker above the slot just held — a combined roster of every learned skill and spell, "비우기"
    /// first. Picking one puts it there (swapping with wherever it already sat), even across the 기술/마법 switch.
    /// </summary>
    private void OpenPicker(int index)
    {
        int position = _page * AbilityFan.PerPage + index;

        foreach (Node old in _pickerList.GetChildren())
        {
            _pickerList.RemoveChild(old);
            old.QueueFree();
        }

        Button empty = Row("비우기", null);
        bool heldSpells = _spells;
        empty.Pressed += () =>
        {
            (heldSpells ? _spellArrangement : _skillArrangement).Clear(position);
            Persist();
            _picker.Hide();
            Redraw();
        };
        _pickerList.AddChild(empty);

        foreach (LearnedSkill skill in _learnedSkills)
        {
            Button row = Row(skill.Name, Frame(SkillSheet, skill.Icon));
            int pickedSlot = skill.Slot;
            row.Pressed += () => Pick(position, spell: false, pickedSlot);
            _pickerList.AddChild(row);
        }

        foreach (LearnedSpell spell in _learnedSpells)
        {
            Button row = Row(spell.Name, Frame(SpellSheet, spell.Icon));
            int pickedSlot = spell.Slot;
            row.Pressed += () => Pick(position, spell: true, pickedSlot);
            _pickerList.AddChild(row);
        }

        // 다섯 줄까지는 그대로 보이고, 더 있으면 굴린다(docs/mobile-client.md 의 스크롤 규칙 — TouchInput 이
        // 목록 안 단추의 누름을 목록에도 넘긴다).
        const int MaxVisibleRows = 5;
        int visible = Math.Min(_pickerList.GetChildCount(), MaxVisibleRows);
        _pickerScroll.CustomMinimumSize = new Vector2(PickerWidth, visible * (Main.TouchMinimum + Main.Gutter / 2));

        _picker.Popup(new Rect2I(0, 0, 0, 0));

        // 그 슬롯 위에, 화면 밖으로 넘치지 않게.
        Rect2 at = _slots[index].GetGlobalRect();
        Vector2 screen = GetViewportRect().Size;
        Vector2I size = _picker.Size;

        int x = Mathf.Clamp((int)at.Position.X, Main.Gutter, Mathf.Max(Main.Gutter, (int)screen.X - size.X - Main.Gutter));
        int y = Mathf.Max(Main.Gutter, (int)at.Position.Y - size.Y - Main.Gutter / 2);

        _picker.Position = new Vector2I(x, y);
    }

    private void Pick(int position, bool spell, int slotNumber)
    {
        int? displaced = DisplayedSlotAt(spell, position);
        (spell ? _spellArrangement : _skillArrangement).Place(position, slotNumber, displaced);
        _spells = spell;
        Persist();
        _picker.Hide();
        Redraw();
    }

    private void Persist()
    {
        if (_loadedFor.Length > 0)
        {
            SaveSlots?.Invoke(_loadedFor, [.. AbilitySlotSave.ToLines(_skillArrangement, _spellArrangement)]);
        }
    }

    private const int PickerWidth = 220;

    private static Button Row(string name, Texture2D? icon)
    {
        Button row = new()
        {
            Text = name,
            Icon = icon,
            Alignment = HorizontalAlignment.Left,
            IconAlignment = HorizontalAlignment.Left,
            // 너비를 칸 자신이 갖는다 — 담는 쪽(ScrollContainer)의 CustomMinimumSize 는 다음 프레임에야
            // 자리를 잡아, 첫 장은 그림 너비로 오그라들었다.
            CustomMinimumSize = new Vector2(PickerWidth, Main.TouchMinimum),
            ClipText = true
        };

        Greybox.Plain(row);
        return row;
    }

    /// <summary>
    /// Puts one automatic-potion switch at the top of the fan, above the highest skills (<see cref="AbilityFan.Potions" />).
    /// </summary>
    public void Hold(PotionChip chip, int index)
    {
        chip.CustomMinimumSize = new Vector2(AbilityFan.PotionSide, AbilityFan.PotionSide);
        Place(chip, AbilityFan.Potions[index], AbilityFan.PotionSide);
    }

    private void Place(Button button, (int X, int Y) centre, int side)
    {
        button.Position = new Vector2(centre.X - (side / 2), centre.Y - (side / 2));
        button.Size = new Vector2(side, side);
        AddChild(button);
    }

    /// <summary>The attack button — a disc like the rest, but the one wearing the accent.</summary>
    private static Button Struck(string text, int side)
    {
        Button button = Disc(text, side);

        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
        {
            // 화면에서 유일하게 색을 입은 조작이다 — 손가락이 먼저 가는 곳이라 눈도 먼저 가야 한다.
            StyleBoxFlat filled = new()
            {
                BgColor = state == "pressed" ? Greybox.Accent.Darkened(0.18f) : Greybox.Accent
            };

            filled.SetCornerRadiusAll(side / 2 - 1);
            filled.SetContentMarginAll(7);
            button.AddThemeStyleboxOverride(state, filled);
        }

        foreach (string colour in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(colour, Greybox.OnAccent);
        }

        return button;
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
