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
    private readonly Button _next = Disc("1/1", AbilityFan.NextSide);

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
    private int _rehearsedAutoHunt; // --auto-hunt-preview: 서버 없이 켜짐 표시만 그려 볼 때 프레임을 센다.

    // 공격 단추도 같은 0.5초 규칙으로 길게 누르면 자동 사냥을 켜고 끈다(사용자 요청, 2026-09-26) — 판단은
    // 알맹이 LongPress(시험 LongPressTests)로 뺐다. 짧게 누르면(길게 눌리지 않았으면) 지금처럼 곧장 평타.
    private readonly LongPress _attackHold = new(TimeSpan.FromMilliseconds(HoldMilliseconds));

    // 슬롯 배치(사용자 요청) — 캐릭터 이름별로 기기 안에 저장한다(Main.LoadAbilitySlots/SaveAbilitySlots).
    private AbilityArrangement _skillArrangement = new();
    private AbilityArrangement _spellArrangement = new();
    private string _loadedFor = string.Empty;
    private readonly PopupPanel _picker = new();
    private readonly VBoxContainer _pickerRoot = new();
    private readonly VBoxContainer _pickerList = new();
    private readonly ScrollContainer _pickerScroll = new();

    // "비우기" 줄 — 굴림 밖에 고정해 늘 맨 위에 보인다(사용자 확인 요청, 2026-09-26 — 전에는 목록 첫 줄이라
    // 함께 굴러가 화면 밖으로 밀렸다). 한 번만 만들고 열 때마다 눌림 줄만 바꿔 단다.
    private readonly Button _clearRow = Row("비우기", null);
    private Action? _clearHandler;

    /// <summary>Given a character's name, the saved lines for their slots (empty if none yet).</summary>
    public Func<string, IEnumerable<string>>? LoadSlots { get; set; }

    /// <summary>Given a character's name and the lines to keep, saves the slot arrangement.</summary>
    public Action<string, IReadOnlyList<string>>? SaveSlots { get; set; }

    /// <summary>One tap is one blow — see <see cref="WorldView.Strike" />.</summary>
    /// <summary>
    /// 공격 단추. 시안에서 유일하게 돌로 남긴 조작이다 — 창의 확정 단추와 같은 자리다(data/ui-vault 안C).
    /// </summary>
    public Button Attack { get; } = Struck("공격", AbilityFan.AttackSide);

    // 자동 사냥이 켜지면 공격 단추에 테두리와 이 작은 글자로 보인다(위 줄의 [자동] 단추는 없앴다 — 사용자
    // 요청, 2026-09-26).
    private readonly Label _autoHuntTag = new()
    {
        Text = "자동",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        MouseFilter = MouseFilterEnum.Ignore,
        Visible = false
    };

    // 켜져 있는 동안 공격 단추 둘레를 도는 빛 — 테두리가 멈춰 있어 "자동 사냥이 도는지" 한눈에 안 읽히던 것을
    // 고친다(사용자 요청, 2026-09-26). AutoHuntRing.cs.
    private readonly AutoHuntRing _autoHuntRing = new();

    /// <summary>How many seconds one slot still has to wait, asked of the server every frame.</summary>
    public Func<bool, int, int>? Cooling { get; set; }

    public event Action<int>? SkillUsed;
    public event Action<int>? SpellUsed;

    /// <summary>The attack button was let go as an ordinary short press — swing once (<see cref="WorldView.Strike" />).</summary>
    public event Action? AttackReleased;

    /// <summary>The attack button was held 0.5초 — turn auto-hunt on or off.</summary>
    public event Action? AutoHuntToggleRequested;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(AbilityFan.Width, AbilityFan.Height);
        MouseFilter = MouseFilterEnum.Ignore;

        Place(Attack, AbilityFan.Attack, AbilityFan.AttackSide);

        // 0.5초 길게 누르면 자동 사냥을 켜고 끈다 — 뗄 때(짧게 눌렀을 때)만 평타가 나간다. 길게 눌러 이미
        // 자동 사냥을 건드렸으면 뗄 때의 눌림은 버린다(기술 슬롯의 길게 누르기와 같은 결).
        Attack.ButtonDown += () => _attackHold.Down(TimeSpan.FromMilliseconds(Time.GetTicksMsec()));
        Attack.ButtonUp += () => _attackHold.Up();
        Attack.Pressed += () =>
        {
            if (_attackHold.ShortPress)
            {
                AttackReleased?.Invoke();
            }
        };

        // 링을 글자보다 먼저 붙여, 도는 빛이 "자동" 글자 뒤로 지나가 글자는 늘 그대로 읽힌다.
        _autoHuntRing.SetAnchorsPreset(LayoutPreset.FullRect);
        Attack.AddChild(_autoHuntRing);

        _autoHuntTag.SetAnchorsPreset(LayoutPreset.BottomWide);
        _autoHuntTag.OffsetTop = -18;
        _autoHuntTag.AddThemeFontSizeOverride("font_size", 9);
        _autoHuntTag.AddThemeColorOverride("font_color", Greybox.OnAccent);
        Attack.AddChild(_autoHuntTag);

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
        // 쪽 표시는 포션 칸만 한 작은 칸으로 부채꼴 왼쪽 위 구석에(2026-09-26). 글자도 작게.
        _next.AddThemeFontSizeOverride("font_size", 11);

        // 둥근 판의 안 여백(7)은 기술 그림용이다 — 32 칸에 "1/3" 이 잘리지 않게 줄인다.
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            if (_next.GetThemeStylebox(state) is StyleBoxFlat box)
            {
                box.SetContentMarginAll(1);
            }
        }
        Place(_next, AbilityFan.Next, AbilityFan.NextSide);

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
        // (docs/original-ui-451.md: "돌을 안 쓰는 곳 — 목록"). "비우기"는 _pickerRoot 에 고정으로 붙고,
        // 배운 기술·마법만 그 아래 _pickerScroll 안에서 굴러간다.
        _pickerList.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _pickerScroll.CustomMinimumSize = new Vector2(PickerWidth, 0);
        _pickerScroll.AddChild(_pickerList);

        _pickerRoot.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _pickerRoot.AddChild(_clearRow);
        _pickerRoot.AddChild(_pickerScroll);
        _picker.AddChild(_pickerRoot);
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

        if (_attackHold.CrossedThreshold(TimeSpan.FromMilliseconds(Time.GetTicksMsec())))
        {
            AutoHuntToggleRequested?.Invoke();
        }

        // --auto-hunt-preview: 서버가 없어 GameScreen 이 실제로 켤 수 없으니, 여기서 스스로 켜짐 표시만 그려 본다.
        if (Main.AutoHuntPreview && ++_rehearsedAutoHunt == 90)
        {
            ShowAutoHunt(true);
        }
    }

    /// <summary>Shows or hides the attack button's auto-hunt "켜짐" mark — a ring plus the small "자동" tag,
    /// dimmed while a person's own hand has it paused. <see cref="GameScreen"/> calls this instead of drawing
    /// a separate [자동] button (없앴다, 사용자 요청 2026-09-26).</summary>
    public void ShowAutoHunt(bool on, bool paused = false)
    {
        PaintAttack(Attack, AbilityFan.AttackSide, on, paused);
        _autoHuntTag.Visible = on;
        _autoHuntTag.Modulate = paused ? new Color(1, 1, 1, 0.55f) : Colors.White;
        _autoHuntRing.Show(on, paused);
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

    /// <summary>기술 부채꼴에 놓인 기술, 놓인 차례대로 — 비운 칸의 것은 빠진다. 자동 사냥이 이것만 쓴다.</summary>
    public IReadOnlyList<LearnedSkill> PlacedSkills()
    {
        int capacity = AbilityFan.Pages(_learnedSkills.Count) * AbilityFan.PerPage;
        return [.. _skillArrangement.Fill(_learnedSkills, skill => skill.Slot, capacity).OfType<LearnedSkill>()];
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
    /// Opens the picker above the slot just held — "비우기" pinned above a combined, scrolling roster of every
    /// learned skill and spell. Picking one puts it there (swapping with wherever it already sat), even across
    /// the 기술/마법 switch.
    /// </summary>
    private void OpenPicker(int index)
    {
        int position = _page * AbilityFan.PerPage + index;

        foreach (Node old in _pickerList.GetChildren())
        {
            _pickerList.RemoveChild(old);
            old.QueueFree();
        }

        if (_clearHandler is { } previous)
        {
            _clearRow.Pressed -= previous;
        }

        bool heldSpells = _spells;
        int currentSlot = _drawn[index] switch
        {
            LearnedSkill skill => skill.Slot,
            LearnedSpell spell => spell.Slot,
            _ => 0
        };

        _clearHandler = () =>
        {
            // 지금 그 자리에 있던 슬롯 번호(currentSlot)도 함께 넘긴다 — 그래야 그 기술이 다른 빈 칸으로
            // 도로 들어가지 않는다(AbilityArrangement.Clear 참고, 사용자 버그 리포트 2026-09-26).
            (heldSpells ? _spellArrangement : _skillArrangement).Clear(position, currentSlot);
            Persist();
            _picker.Hide();
            Redraw();
        };
        _clearRow.Pressed += _clearHandler;

        // 이미 빈 칸이면 지울 것이 없다 — 흐리게 하고 눌러도 아무 일이 안 일어난다(사용자 확인 요청, 2026-09-26).
        _clearRow.Disabled = currentSlot == 0;
        _clearRow.Modulate = currentSlot == 0 ? new Color(1, 1, 1, 0.4f) : Colors.White;

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

        // 다섯 줄까지는 그대로 보이고, 더 있으면 굴린다 — 화면이 낮으면(가로 아이폰 등) 그보다 더 줄여, "비우기"
        // + 목록을 합친 판이 화면 높이를 넘지 않게 한다(사용자 확인 요청, 2026-09-26). docs/mobile-client.md 의
        // 스크롤 규칙 — TouchInput 이 목록 안 단추의 누름을 목록에도 넘긴다.
        const int MaxVisibleRows = 5;
        int rowHeight = Main.TouchMinimum + Main.Gutter / 2;
        int reserved = Main.TouchMinimum + Main.Gutter * 3; // 비우기 줄 + 틈 + 판 테두리 어림
        int screenLimited = Math.Max(1, (int)((GetViewportRect().Size.Y - reserved) / rowHeight));
        int visible = Math.Min(Math.Min(_pickerList.GetChildCount(), MaxVisibleRows), screenLimited);
        _pickerScroll.CustomMinimumSize = new Vector2(PickerWidth, visible * rowHeight);

        _picker.Popup(new Rect2I(0, 0, 0, 0));

        // 그 슬롯 위에, 화면·가장자리를 넘지 않게(위아래 모두 자른다 — 전에는 위로 넘치는 것만 막았다).
        Rect2 at = _slots[index].GetGlobalRect();
        Vector2 screen = GetViewportRect().Size;
        Vector2I size = _picker.Size;

        int x = Mathf.Clamp((int)at.Position.X, Main.Gutter, Mathf.Max(Main.Gutter, (int)screen.X - size.X - Main.Gutter));
        int y = Mathf.Clamp(
            (int)at.Position.Y - size.Y - Main.Gutter / 2,
            Main.Gutter,
            Mathf.Max(Main.Gutter, (int)screen.Y - size.Y - Main.Gutter));

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

    /// <summary>코마디움 칸을 부채꼴 왼쪽 위에(<see cref="AbilityFan.Coma" />), 포션 칸과 같은 크기로.</summary>
    public void HoldComa(Button chip)
    {
        chip.CustomMinimumSize = new Vector2(AbilityFan.PotionSide, AbilityFan.PotionSide);
        Place(chip, AbilityFan.Coma, AbilityFan.PotionSide);
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
        PaintAttack(button, side, on: false);

        foreach (string colour in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(colour, Greybox.OnAccent);
        }

        return button;
    }

    /// <summary>
    /// Fills the attack button — plain, or with a border ring when auto-hunt is on (원작 4.51 규칙표의 밝은 돌
    /// 틀 색 <see cref="Greybox.Muted"/>), dimmed while a person's own hand has it paused.
    /// </summary>
    private static void PaintAttack(Button button, int side, bool on, bool paused = false)
    {
        Color ring = paused ? Greybox.Muted with { A = 0.5f } : Greybox.Muted;

        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
        {
            // 화면에서 유일하게 색을 입은 조작이다 — 손가락이 먼저 가는 곳이라 눈도 먼저 가야 한다.
            StyleBoxFlat filled = new()
            {
                BgColor = state == "pressed" ? Greybox.Accent.Darkened(0.18f) : Greybox.Accent,
                BorderColor = ring
            };

            filled.SetCornerRadiusAll(side / 2 - 1);
            filled.SetContentMarginAll(7);
            filled.SetBorderWidthAll(on ? 3 : 0);
            button.AddThemeStyleboxOverride(state, filled);
        }
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
