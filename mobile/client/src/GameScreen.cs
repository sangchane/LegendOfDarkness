using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// Main game greybox, built to section 5 of the wireframes. The world fills the whole screen while every
/// label and control stays inside the safe area, and the two thumb clusters sit where a thumb can reach.
/// </summary>
public partial class GameScreen : Control
{
    private const int AuxFontSize = 14;
    private const int StatusBarWidth = 120;
    private const int PortraitStatusBarWidth = 96;

    /// <summary>Rows the chat and combat log keeps in portrait. Landscape has no room for it at all.</summary>
    private const int LogHeight = 76;

    private WorldView _world = null!;
    private Label _who = null!;
    private Label _place = null!;
    private Label _target = null!;
    private PackPanel _pack = null!;
    private TalkPanel _talk = null!;

    // 창이 몇 번 열리고 닫혔나. 같은 말의 창이 다시 온 것과 아무 일 없는 것을 가르려고 센다.
    private int _talked;
    private Label _notice = null!;
    private Control _noticePlate = null!;
    private double _noticeLeft;
    private ProgressBar _targetHealth = null!;
    private Control _targetPlate = null!;
    private Control? _placePlate;
    private AbilityBar _abilities = null!;

    /// <summary>How long a line the server said stays over the floor in landscape.</summary>
    private const double NoticeSeconds = 4;

    // 방향판. 걷는 동안 흐려져 그 밑의 바닥이 보인다 — 방향판은 가로에서 월드 왼쪽 아래를 덮는다.
    private Control _pad = null!;
    private readonly List<(ThumbButton Key, Direction Where)> _keys = [];
    private double _stillFor = SettleSeconds;

    /// <summary>How see-through the pad gets while walking, how long it waits after the last step, how fast it fades.</summary>
    private const float WalkingAlpha = 0.35f;
    private const double SettleSeconds = 0.25;
    private const double FadeSeconds = 0.12;

    // 내 체력·마력. 서버가 준 값이 바뀔 때만 다시 쓴다.
    private ProgressBar _health = null!;
    private ProgressBar _mana = null!;
    private Label _healthText = null!;
    private Label _manaText = null!;
    private Vitals? _shownVitals;

    // 서버가 말한 횟수. 같은 말을 다시 하는 것과 새로 하는 것을 가르려고 센다.
    private int _heard = -1;
    private Control _packRow = null!;

    // 손 없이 확인할 때 스스로 열어 보기 위한 것. 월드가 자리를 잡을 때까지 센다.
    private int _settling;

    // 리허설로 한 번만 입어 본다.
    private bool _worn;
    private Control _topRow = null!;
    private Control _controlRow = null!;

    private readonly WorldClient? _server;

    public GameScreen(WorldClient? server = null)
    {
        _server = server;
        Name = "GameScreen";
        AnchorRight = 1;
        AnchorBottom = 1;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    /// <summary>
    /// The pieces a layout check walks: everything that has to stay on the screen and out of each other's
    /// way. Named in Korean because the names are printed for a person to read.
    /// </summary>
    public IReadOnlyList<(string Name, Control Part)> Parts =>
        [("위 줄", _topRow), ("조작 줄", _controlRow), ("인벤토리", _pack), ("월드", _world)];

    /// <summary>Which tab the pack shows. Only a layout check asks — a thumb presses the tab itself.</summary>
    public void ShowGearTab(bool gear) => _pack.ShowTab(gear);

    public override void _Ready()
    {
        MarginContainer hud = Main.SafeAreaContainer();

        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);

        // In landscape the HUD lies over the whole world, and a container that eats taps would stop anyone
        // ever touching a figure. The plates and buttons inside it still take their own.
        hud.MouseFilter = MouseFilterEnum.Ignore;
        rows.MouseFilter = MouseFilterEnum.Ignore;

        _topRow = BuildTopRow();
        _pack = new PackPanel();
        _pack.Close.Pressed += () => Carrying(false);
        _pack.Used += slot => _ = _server?.UseAsync(slot, System.Threading.CancellationToken.None);
        _pack.TakenOff += place => _ = _server?.TakeOffAsync(place, System.Threading.CancellationToken.None);
        _pack.Dropped += slot => _ = Throw(slot);
        _pack.Tidy.Pressed += () => _ = Straighten();

        _talk = new TalkPanel();
        _talk.Close.Pressed += ShutTalk;
        _talk.Answered += (speaker, step, words) => _ = words is null
            ? _server?.AnswerAsync(speaker, step, System.Threading.CancellationToken.None)
            : _server?.AnswerAsync(speaker, step, words, System.Threading.CancellationToken.None);

        if (Main.Portrait)
        {
            // Portrait has the height to give the world a row of its own, so nothing the player is aiming
            // at sits under a thumb. That is the whole reason to hold the phone this way.
            BuildWorld(fullBleed: false);
            _controlRow = BuildControlRow();

            AddChild(hud);
            hud.AddChild(rows);
            rows.AddChild(_topRow);
            rows.AddChild(_world);
            rows.AddChild(_packRow = BuildPackRow());
            rows.AddChild(BuildLog());
            rows.AddChild(_controlRow);

            Cover(hud);
        }
        else
        {
            // Landscape has no such room: the world fills the screen and the HUD floats over its corners.
            BuildWorld(fullBleed: true);
            _controlRow = BuildControlRow();

            AddChild(_world);
            AddChild(hud);
            hud.AddChild(rows);
            rows.AddChild(_topRow);
            rows.AddChild(_packRow = BuildPackRow());
            rows.AddChild(_controlRow);

            Cover(hud);
        }
    }

    /// <summary>
    /// Lays the pack — and an NPC's window, in the same place — over the screen rather than in a row of its own.
    /// Neither shape leaves a row tall enough for a grid of pictures — landscape leaves less than one cell — and the
    /// wireframes already call both modals, so covering the control row costs nothing: it is dead while one is open.
    /// </summary>
    /// <remarks>
    /// A MarginContainer stretches its children to fill it, which throws away anchors. One plain Control
    /// in between restores them.
    /// </remarks>
    private void Cover(Control hud)
    {
        Control over = new() { MouseFilter = MouseFilterEnum.Ignore };

        hud.AddChild(over);

        // 가로는 오른쪽 기둥, 세로는 전폭. 위 줄만 남겨 두어 이름과 체력은 계속 보인다.
        // 기둥은 3분의 1 남짓이면 충분했지만 장비 탭의 고리는 그보다 넓다 — 좁은 화면에서는 고리가
        // 들어갈 만큼 떼어 준다. 16:9 에서는 그게 화면의 절반 남짓이다(시안 8.1절).
        float across = GetViewportRect().Size.X;
        float column = across > 0 ? 1f - (GearGrid.PanelWidth / across) : 0.6f;

        foreach (Control panel in new Control[] { _pack, _talk })
        {
            over.AddChild(panel);
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.AnchorLeft = Main.Portrait ? 0 : Mathf.Min(0.6f, column);
            panel.OffsetLeft = 0;
            panel.OffsetTop = Main.TouchMinimum + (Main.Gutter * 3);
            panel.OffsetRight = 0;
            panel.OffsetBottom = 0;
        }
    }

    /// <summary>
    /// Where the pack sits when it is open: against the right edge, over rather than beside the world, and
    /// taking a bit over a third of the width. The rest of the row lets taps through to the floor.
    /// </summary>
    private Control BuildPackRow()
    {
        HBoxContainer row = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };

        // 높이는 남는 만큼만 — 최소 높이를 박으면 위·아래 줄이 화면 밖으로 밀린다(한 번 그렇게 됐다).
        _pack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pack.SizeFlagsVertical = SizeFlags.ExpandFill;

        if (!Main.Portrait)
        {
            // 가로에서는 이 줄이 위 줄과 조작 줄 사이의 빈 자리이기도 하다. 닫혀 있어도 남아 있어야
            // 조작 줄이 위로 올라오지 않는다. 패널은 오른쪽 3분의 1 남짓만 덮는다.
            row.AddChild(new Control
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 62,
                MouseFilter = MouseFilterEnum.Ignore
            });

            _pack.SizeFlagsStretchRatio = 38;
        }

        // 패널은 이 줄이 아니라 HUD 위에 덮어 놓는다(Cover). 줄은 조작 줄이 올라오지 않게
        // 자리만 지킨다.

        // 세로에서는 시안대로 월드 아래 전폭이고, 닫혀 있으면 줄째로 사라져 월드에 자리를 돌려준다.
        row.Visible = !Main.Portrait;

        return row;
    }

    /// <summary>
    /// The world itself. In landscape it fills the screen and the HUD floats over it; in portrait it takes a
    /// row of its own so nothing the player is aiming at sits under a thumb.
    /// </summary>
    private Control BuildWorld(bool fullBleed)
    {
        _world = new WorldView(_server);

        if (fullBleed)
        {
            _world.AnchorRight = 1;
            _world.AnchorBottom = 1;
            _world.GrowHorizontal = GrowDirection.Both;
            _world.GrowVertical = GrowDirection.Both;
        }
        else
        {
            _world.SizeFlagsVertical = SizeFlags.ExpandFill;
        }

        return _world;
    }

    /// <summary>
    /// What the original kept at the bottom of its screen. Portrait has the height to keep it, and it is
    /// what replaces the passing notice landscape has to make do with.
    /// </summary>
    private static Control BuildLog()
    {
        Panel frame = new() { CustomMinimumSize = new Vector2(0, LogHeight) };
        frame.AddThemeStyleboxOverride("panel", Greybox.Surface());

        MarginContainer inset = new()
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
        };
        inset.AddThemeConstantOverride("margin_left", Main.Gutter);
        inset.AddThemeConstantOverride("margin_right", Main.Gutter);
        inset.AddThemeConstantOverride("margin_bottom", Main.Gutter / 2);
        frame.AddChild(inset);

        VBoxContainer lines = new() { Alignment = BoxContainer.AlignmentMode.End };
        lines.AddThemeConstantOverride("separation", 2);

        foreach (string line in new[] { "\uc548\uc804 \uac00\uc625\uc5d0 \ub4e4\uc5b4\uc654\uc2b5\ub2c8\ub2e4.", "\uc8fc\ubaa8: \uc5b4\uc11c \uc624\uc2dc\uac8c.", "\uac70\ubbf8\ub97c \uaca8\ub215\ub2c8\ub2e4." })
        {
            lines.AddChild(Aux(line));
        }

        inset.AddChild(lines);

        return frame;
    }

    /// <summary>
    /// Name and health on the left, whoever is picked out in the middle, world state and inventory on the right — each on
    /// a plate of its own that keeps it readable over the floor, with the floor showing between them.
    /// </summary>
    private Control BuildTopRow()
    {
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        // Empty until the server names us, in step with the place name below: a made-up name on the
        // HUD is worse than none, because there is no way to tell it from a real one.
        _who = Aux(string.Empty);

        HBoxContainer mine = new();
        mine.AddThemeConstantOverride("separation", Main.Gutter);
        mine.AddChild(_who);
        mine.AddChild(BuildVitals());
        row.AddChild(Plated(mine));

        // Whoever is picked out, in the middle where the original kept it. Empty until somebody is.
        _target = Aux(string.Empty);

        _targetHealth = new ProgressBar
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? 48 : 72, 10),
            MaxValue = 100,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Visible = false
        };
        _targetHealth.AddThemeStyleboxOverride("background", Greybox.Surface());
        _targetHealth.AddThemeStyleboxOverride("fill", Greybox.Fill());

        HBoxContainer picked = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        picked.AddThemeConstantOverride("separation", Main.Gutter / 2);
        picked.AddChild(_target);
        picked.AddChild(_targetHealth);

        // 고른 이가 없으면 판째로 숨긴다 — 빈 판이 바닥 한가운데를 가린다.
        CenterContainer middle = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _targetPlate = Plated(picked);
        _targetPlate.Visible = false;
        middle.AddChild(_targetPlate);
        row.AddChild(middle);

        // Where the server says we are. Offline it stays empty rather than claiming something untrue.
        _place = Aux(string.Empty);

        // 360 across cannot hold this as well, so in portrait the log carries it instead.
        if (!Main.Portrait)
        {
            row.AddChild(_placePlate = Plated(_place));
            _placePlate.Visible = false;
        }

        Button pack = new()
        {
            Text = "인벤토리",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        pack.Pressed += () => Carrying(true);
        row.AddChild(pack);

        return row;
    }

    private static Control Plated(Control inside)
    {
        PanelContainer plate = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        plate.AddThemeStyleboxOverride("panel", Greybox.Plate());
        plate.AddChild(inside);

        return plate;
    }

    /// <summary>Keeps the place name and whoever is picked out in step with the world below.</summary>
    public override void _Process(double delta)
    {
        if (_world.PlaceName.Length > 0)
        {
            _place.Text = $"{_world.PlaceName} · {_world.Standing.X},{_world.Standing.Y}";
        }

        // 서버가 어디라고 말하기 전에는 빈 판이 오른쪽 위에 남는다.
        if (_placePlate is not null)
        {
            _placePlate.Visible = _place.Text.Length > 0;
        }

        // The server names us in 0x33; nothing else on this screen knows who we are.
        if (_server?.Self?.Name is { Length: > 0 } called)
        {
            _who.Text = Mine.Level > 0 ? $"{called} Lv{Mine.Level}" : called;
        }

        ShowVitals();

        ShowTarget();
        _abilities.Show(
            _server?.Skills ?? LayoutCheck.PretendSkills,
            _server?.Spells ?? LayoutCheck.PretendSpells);

        if (_server is { } talking && talking.TalkCount != _talked)
        {
            _talked = talking.TalkCount;
            Talk(talking.Talking);
        }

        if (_server is { } server && server.SaidCount != _heard)
        {
            _heard = server.SaidCount;
            Notify(server.Said);
        }

        if (_noticeLeft > 0 && (_noticeLeft -= delta) <= 0)
        {
            Notify(string.Empty);
        }

        KeepWalking(delta);
        RehearseAHold(delta);

        // 레이아웃 검사는 세 프레임 만에 재고 끝난다. 90 프레임을 기다리면 닫힌 화면을 재게 되고,
        // 실제로 그래서 장비 칸이 넘쳤는데도 0 오류였다 — 검사 중에는 바로 연다.
        int settle = LayoutCheck.Requested() || LayoutCheck.PretendPack.Count > 0 ? 0 : 90;

        if (Main.OpeningPack && !_pack.Visible && _settling++ == settle)
        {
            Carrying(true);
        }

        if (_pack.Visible)
        {
            _pack.Show(_server?.Pack ?? LayoutCheck.PretendPack, _server?.Worn ?? LayoutCheck.PretendWorn, _server?.Self ?? LayoutCheck.PretendSelf, Mine.Gold);

            // 손 없이 확인할 때만. 목록이 채워진 다음 프레임에 첫 줄을 한 번 누른다.
            if ((Main.Wearing || Main.Throwing) && !_worn && _pack.PressFirst(Main.Throwing))
            {
                _worn = true;
                GD.Print(Main.Throwing ? "GREYBOX_THREW 첫 줄을 버렸다" : "GREYBOX_WORE 첫 줄을 눌렀다");

                // 버린 것을 이어서 주워 보려면 손을 뗀 화면이어야 한다 — 소지품이 열려 있으면 월드가
                // 얼어 탭이 통째로 무시된다. 그래서 여기서 닫는다.
                if (Main.Throwing && Main.Lifting)
                {
                    Carrying(false);
                }
            }
        }
    }

    /// <summary>Throws one slot on the floor, at our own feet — the only tile we can be sure of.</summary>
    private async Task Throw(int slot)
    {
        if (_server is not { State: { } standing } server)
        {
            return;
        }

        await server.DropAsync(slot, 1, standing.Where, System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// Pulls everything to the front of the pack. The server only swaps two slots at a time, so this is a
    /// run of swaps; it is worked out in one go from what we are holding now, before any of them land.
    /// </summary>
    private async Task Straighten()
    {
        if (_server is not { } server)
        {
            return;
        }

        foreach ((int from, int to) in PackOrder.Tidy(server.Pack))
        {
            await server.MoveAsync(from, to, System.Threading.CancellationToken.None);
        }
    }

    /// <summary>
    /// Whoever is picked out, and how hurt they are. The bar is never alone — the number is beside it,
    /// because health must not be readable by colour or length alone.
    /// </summary>
    private void ShowTarget()
    {
        int? left = _world.Target == 0 ? null : _server?.Health(_world.Target);

        _target.Text = left is { } percent
            ? $"{_world.TargetName} {percent}%"
            : _world.TargetName;

        _targetHealth.Visible = left is not null;
        _targetPlate.Visible = _target.Text.Length > 0;

        if (left is { } value)
        {
            _targetHealth.Value = value;
        }
    }

    // --hold 로 누르고 있는 시간. 음수면 아직 안 눌렀다.
    private double _held = -1;
    private int _holdWait;
    private double _holdReported;

    /// <summary>
    /// Only when checking without a hand (<c>--hold E</c>): presses the key in the middle with a finger for a second and a
    /// half, lets go, and says where the character stands and how see-through the pad is every quarter second.
    /// </summary>
    private void RehearseAHold(double delta)
    {
        // 접속 직후 서버가 화면을 새로 보내는 동안은 걸음을 버린다(CancelWalkingIfRefreshing) — 서버가 있으면 4초 남짓 기다린다.
        if (Main.Holding.Length == 0 || _held > 3 || _holdWait++ < (_server is null ? 30 : 240))
        {
            return;
        }

        Button? key = _keys.FirstOrDefault(one => one.Where.ToString()[0] == char.ToUpperInvariant(Main.Holding[0])).Key;

        if (key is null)
        {
            return;
        }

        if (_held < 0)
        {
            Press(key, true);
            _held = 0;
            GD.Print($"GREYBOX_HOLD 누름 칸 {_world.Standing.X},{_world.Standing.Y}");
            return;
        }

        double before = _held;
        _held += delta;

        if (before < 1.5 && _held >= 1.5)
        {
            Press(key, false);
            GD.Print($"GREYBOX_HOLD 뗌 칸 {_world.Standing.X},{_world.Standing.Y}");
        }

        if (_held - _holdReported >= 0.25)
        {
            _holdReported = _held;
            GD.Print($"GREYBOX_HOLD {_held:0.00}초 칸 {_world.Standing.X},{_world.Standing.Y} 투명도 {_pad.Modulate.A:0.00}");
        }
    }

    // 손가락으로 누른다 — 폰에서처럼 Godot 이 첫 손가락을 마우스로 바꿔 버튼에 준다. 마우스로 누르면 그다음 손가락이
    // 첫 손가락으로 쳐져 폰과 다르게 돈다(두 손가락 시험에서 그랬다).
    private static void Press(Button key, bool down) => Input.ParseInputEvent(
        new InputEventScreenTouch { Index = 0, Pressed = down, Position = key.GetGlobalRect().GetCenter() });

    /// <summary>
    /// Shows a line under the world, and in landscape hides its plate again once it has been read — an empty plate is a
    /// dark bar across the floor.
    /// </summary>
    private void Notify(string line)
    {
        _notice.Text = line;
        _noticePlate.Visible = line.Length > 0;
        _noticeLeft = line.Length > 0 ? NoticeSeconds : 0;
    }

    /// <summary>
    /// Keeps stepping while a direction is held, and lets the pad fade while the character walks so the floor under it
    /// shows. It comes back a moment after the last step, not between steps, or it would flicker.
    /// </summary>
    private void KeepWalking(double delta)
    {
        bool held = false;

        foreach ((ThumbButton key, Direction where) in _keys)
        {
            if (key.Held)
            {
                held = true;
                _world.Walk(where);
            }
        }

        _stillFor = held || _world.Walking ? 0 : _stillFor + delta;

        float wanted = _stillFor < SettleSeconds ? WalkingAlpha : 1f;
        Color look = _pad.Modulate;
        look.A = Mathf.MoveToward(look.A, wanted, (float)(delta / FadeSeconds));
        _pad.Modulate = look;
    }

    /// <summary>
    /// Opens or shuts the pack. While it is open the world takes no taps and no steps — the panel lies over
    /// it, and a thumb aimed at the list must not walk the character. An NPC's window lies in the same place, so
    /// opening the pack shuts it the way its own close button does.
    /// </summary>
    private void Carrying(bool open)
    {
        if (open && _talk.Visible)
        {
            ShutTalk();
        }

        _pack.Visible = open;
        _world.Frozen = open;

        if (Main.Portrait)
        {
            _packRow.Visible = open;
        }

        if (open)
        {
            _pack.Show(_server?.Pack ?? LayoutCheck.PretendPack, _server?.Worn ?? LayoutCheck.PretendWorn, _server?.Self ?? LayoutCheck.PretendSelf, Mine.Gold);
        }
    }

    /// <summary>
    /// Opens the window an NPC sent, or shuts it when the server did. While it is open the world takes no taps or steps,
    /// as with the pack, and the pack is put away so the two never lie on top of each other.
    /// </summary>
    private void Talk(Dialogue? talk)
    {
        // 우리가 닫기를 보내면 서버도 닫기(0x30)로 답한다. 그사이 소지품을 열었으면 그 화면을 건드리지 않는다.
        if (talk is null && !_talk.Visible)
        {
            return;
        }

        if (talk is not null && _pack.Visible)
        {
            Carrying(false);
        }

        _talk.Visible = talk is not null;
        _world.Frozen = talk is not null;

        if (Main.Portrait)
        {
            _packRow.Visible = talk is not null;
        }

        if (talk is not null)
        {
            _talk.Show(talk, _server?.Pack ?? []);
        }
    }

    /// <summary>Shuts an NPC's window from our side and tells the server, so it stops walking us through a menu.</summary>
    private void ShutTalk()
    {
        Talk(null);
        _ = _server?.ShutDialogueAsync(System.Threading.CancellationToken.None);
    }

    /// <summary>Our own numbers as the server last gave them; made-up ones while nothing is connected.</summary>
    private Vitals Mine => _server is null ? LayoutCheck.PretendVitals : _server.Vitals ?? Vitals.Unknown;

    /// <summary>
    /// Health over mana, each a bar with its numbers beside it: health must never be readable by colour alone, and
    /// the two bars share a colour, so the words say which is which.
    /// </summary>
    private Control BuildVitals()
    {
        VBoxContainer vitals = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        vitals.AddThemeConstantOverride("separation", 0);

        vitals.AddChild(Gauge("HP", out _health, out _healthText));
        vitals.AddChild(Gauge("MP", out _mana, out _manaText));

        return vitals;
    }

    private static Control Gauge(string name, out ProgressBar bar, out Label text)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);

        bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? PortraitStatusBarWidth : StatusBarWidth, 10),
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        bar.AddThemeStyleboxOverride("background", Greybox.Surface());
        bar.AddThemeStyleboxOverride("fill", Greybox.Fill());

        text = Aux(name);
        row.AddChild(bar);
        row.AddChild(text);

        return row;
    }

    /// <summary>Puts the newest health and mana on the bars, only when they have changed.</summary>
    private void ShowVitals()
    {
        Vitals mine = Mine;

        if (mine == _shownVitals)
        {
            return;
        }

        _shownVitals = mine;
        Fill(_health, _healthText, "HP", mine.Health, mine.MaximumHealth);
        Fill(_mana, _manaText, "MP", mine.Mana, mine.MaximumMana);
    }

    private static void Fill(ProgressBar bar, Label text, string name, int left, int most)
    {
        bar.MaxValue = Mathf.Max(1, most);
        bar.Value = left;
        text.Text = $"{name} {left}/{most}";
    }

    /// <summary>
    /// Movement on the left, the attack button with the skills fanned round it on the right, status between them, inside
    /// a width capped so the two clusters never drift further apart than a thumb can travel on a very wide screen.
    /// </summary>
    /// <remarks>
    /// In landscape the row lies over the floor, so nothing in it but the buttons and the notice takes a tap — a figure
    /// standing between the pad and the fan must still be pickable.
    /// </remarks>
    private Control BuildControlRow()
    {
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };

        // 세로 360 폭은 방향판 152 + 틈 8 + 부채꼴 184 로 꼭 찬다. 칸 사이 틈을 두 번 두면 8 이 넘쳐,
        // 세로에서는 가운데 칸 자체를 틈으로 쓴다.
        row.AddThemeConstantOverride("separation", Main.Portrait ? 0 : Main.Gutter);

        _notice = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _notice.AddThemeFontSizeOverride("font_size", AuxFontSize);
        _notice.AddThemeColorOverride("font_color", Greybox.Muted);

        // The notice floats over the floor in landscape, so it gets a plate of its own rather than an
        // outline: a line of text on gold tiles is unreadable either way without one.
        PanelContainer notice = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            Visible = false
        };
        notice.AddThemeStyleboxOverride("panel", Greybox.Plate());
        notice.AddChild(_notice);
        _noticePlate = notice;

        row.AddChild(_pad = BuildMovementPad());

        VBoxContainer middle = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            Alignment = BoxContainer.AlignmentMode.End,
            CustomMinimumSize = new Vector2(Main.Portrait ? Main.Gutter : 0, 0),
            MouseFilter = MouseFilterEnum.Ignore
        };

        if (!Main.Portrait)
        {
            middle.AddChild(notice);
        }

        row.AddChild(middle);

        _abilities = new AbilityBar { SizeFlagsVertical = SizeFlags.ShrinkEnd };
        _abilities.SkillUsed += slot => _world.UseSkill(slot);
        _abilities.SpellUsed += slot => UseSpell(slot);

        // One tap is one blow. It does not chase and it does not repeat — the server decides whether it
        // landed, and says so in words we show below rather than guessing at damage here.
        _abilities.Attack.Pressed += () => _world.Strike();
        row.AddChild(_abilities);

        MarginContainer capped = Main.Capped(row, Main.ThumbSpanMaximum);
        capped.MouseFilter = MouseFilterEnum.Ignore;

        return capped;
    }

    /// <summary>
    /// Targeted spells use the figure selected in the world. Everything else sends zero, which Hades
    /// deliberately turns into the caster. Typed-input spells need their prompt UI before they are usable.
    /// </summary>
    private void UseSpell(int slot)
    {
        LearnedSpell? spell = _server?.Spells.FirstOrDefault(one => one.Slot == slot);

        if (spell is null)
        {
            return;
        }

        if (spell.TargetType == SpellTargetType.ChooseTarget && _world.Target == 0)
        {
            Notify("마법 대상을 먼저 누르세요.");
            return;
        }

        if (spell.TargetType is SpellTargetType.Prompt or SpellTargetType.FourDigit
            or SpellTargetType.ThreeDigit or SpellTargetType.TwoDigit or SpellTargetType.OneDigit)
        {
            Notify($"{spell.Name}: 입력 창이 필요한 마법입니다.");
            return;
        }

        _world.UseSpell(spell.Slot, spell.TargetType == SpellTargetType.ChooseTarget ? _world.Target : 0);
    }

    /// <summary>
    /// Four directions, no diagonals: one tap is one tile, which is what this game is about, and holding a direction keeps
    /// walking (wireframes section 5). The floor is laid in diamonds, so each of them moves diagonally on screen.
    /// </summary>
    /// <remarks>
    /// A step starts the moment the key goes down rather than when it comes back up, and <see cref="KeepWalking" /> takes
    /// the next one each time a step ends while it is still down.
    /// </remarks>
    private Control BuildMovementPad()
    {
        GridContainer pad = new() { Columns = 3, SizeFlagsVertical = SizeFlags.ShrinkEnd, MouseFilter = MouseFilterEnum.Ignore };
        pad.AddThemeConstantOverride("h_separation", Main.Gutter / 2);
        pad.AddThemeConstantOverride("v_separation", Main.Gutter / 2);

        (string Glyph, Direction Where)?[] layout =
        [
            null, ("↑", Direction.North), null,
            ("←", Direction.West), null, ("→", Direction.East),
            null, ("↓", Direction.South), null
        ];

        foreach ((string Glyph, Direction Where)? key in layout)
        {
            if (key is null)
            {
                pad.AddChild(new Control
                {
                    CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
                    MouseFilter = MouseFilterEnum.Ignore
                });
                continue;
            }

            Direction where = key.Value.Where;

            ThumbButton button = new()
            {
                Text = key.Value.Glyph,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
            };

            Greybox.Disc(button);
            button.ButtonDown += () => _world.Walk(where);
            _keys.Add((button, where));

            pad.AddChild(button);
        }

        return pad;
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }
}
