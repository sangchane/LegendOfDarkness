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
    private MessageLog _messages = null!;
    private ChatPanel _chat = null!;
    private Control _chatHolder = null!;

    /// <summary>What has been said, kept for reading back through — the same lines the log shows as they fade.</summary>
    private readonly List<(bool Speech, string Text)> _history = [];
    private const int HistoryKept = 60;

    // 서버가 들려준 말이 몇 줄째인가. 새로 온 것만 적는다.
    private int _heardSeen;
    private ProgressBar _targetHealth = null!;
    private Control _targetPlate = null!;
    private Control? _placePlate;
    private AbilityBar _abilities = null!;

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
    private Label _experience = null!;
    private Vitals? _shownVitals;

    // 서버가 말한 횟수. 같은 말을 다시 하는 것과 새로 하는 것을 가르려고 센다.
    private int _heard = -1;
    private Control _packRow = null!;
    private Control? _log;

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

        _chat = new ChatPanel();
        _chat.Close.Pressed += () => Chatting(false);
        _chat.Sent += line => _ = _server?.SayAsync(line, System.Threading.CancellationToken.None);

        _talk = new TalkPanel();
        _talk.Close.Pressed += ShutTalk;
        _talk.Answered += (speaker, step, words) => _ = words is null
            ? _server?.AnswerAsync(speaker, step, System.Threading.CancellationToken.None)
            : _server?.AnswerAsync(speaker, step, words, System.Threading.CancellationToken.None);

        // The world fills the screen and the HUD floats over it, in both orientations. Portrait used to give the world a
        // row of its own above the controls, which left a third of the screen black behind the buttons (사용자,
        // 2026-09-18). Nothing the player aims at goes under a thumb all the same: the character stands in the middle of
        // the part the controls leave uncovered (WorldView.FocusY).
        BuildWorld();
        _controlRow = BuildControlRow();

        AddChild(_world);
        AddChild(hud);
        hud.AddChild(rows);
        rows.AddChild(_topRow);
        rows.AddChild(_packRow = BuildPackRow());

        // 세로는 기록 줄이 조작 바로 위에 있다. 가로는 그 자리가 없어 방향판 위에 얹는다(BuildControlRow).
        if (Main.Portrait)
        {
            _messages = new MessageLog(3) { CustomMinimumSize = new Vector2(0, LogHeight) };
            rows.AddChild(_log = BuildMessageRow());
        }

        rows.AddChild(_controlRow);

        Cover(hud);
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

        // 창은 아래에 붙는다. 대화 창과 장비 고리는 남는 높이를 다 쓰고, 소지품 한 장은 제 높이만큼만 올라와
        // 위쪽 맵을 남긴다(PackPanel.ShowTab 이 정한다).
        _talk.SizeFlagsVertical = SizeFlags.ExpandFill;

        // 대화 창은 제 높이만큼만 아래에 붙는다 — 소지품 한 장과 같다. 긴 이야기는 창 안에서 굴린다.
        _chat.SizeFlagsVertical = SizeFlags.ShrinkEnd;

        foreach (Control panel in new Control[] { _pack, _talk, _chat })
        {
            VBoxContainer holder = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
            over.AddChild(holder);
            holder.AddChild(panel);

            if (panel == _chat)
            {
                _chatHolder = holder;
            }

            holder.SetAnchorsPreset(LayoutPreset.FullRect);
            holder.AnchorLeft = Main.Portrait ? 0 : Mathf.Min(0.6f, column);
            holder.OffsetLeft = 0;
            holder.OffsetTop = Main.TouchMinimum + (Main.Gutter * 3);
            holder.OffsetRight = 0;
            holder.OffsetBottom = 0;
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
        }

        // 패널은 이 줄이 아니라 HUD 위에 덮어 놓는다(Cover). 줄은 조작 줄이 올라오지 않게
        // 자리만 지킨다.

        return row;
    }

    /// <summary>The world itself, filling the screen with the HUD floating over it.</summary>
    private void BuildWorld()
    {
        _world = new WorldView(_server)
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
        };
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

        // 세로에서는 조작이 맵 아래쪽을 덮으니, 캐릭터를 위 줄과 기록 줄 사이 한가운데에 세운다.
        if (_log is not null)
        {
            float middle = (_topRow.GetGlobalRect().End.Y + _log.GetGlobalRect().Position.Y) / 2;
            _world.FocusY = middle - _world.GetGlobalRect().Position.Y;
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

        Listen();
        Dropped();
        KeepWalking(delta);
        RehearseAHold(delta);
        RehearseASkill(delta);

        // 레이아웃 검사는 세 프레임 만에 재고 끝난다. 90 프레임을 기다리면 닫힌 화면을 재게 되고,
        // 실제로 그래서 장비 칸이 넘쳤는데도 0 오류였다 — 검사 중에는 바로 연다.
        int settle = LayoutCheck.Requested() || LayoutCheck.PretendPack.Count > 0 ? 0 : 90;

        if (Main.OpeningPack && !_pack.Visible && _settling++ == settle)
        {
            Carrying(true);
        }

        // 창이 열려 있는 동안은 새 줄과 탭을 따라가고, 글자를 치는 동안 화면 키보드에 가리지 않게 창을 들어 올린다
        // (시안 2.1절 — 로그인 화면과 같은 방식).
        if (_chat.Visible)
        {
            _chat.Show(_history);
            _chatHolder.OffsetBottom = -Lifted();
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

    /// <summary>How much of the screen the on-screen keyboard is taking, in this screen's own units.</summary>
    private float Lifted()
    {
        int keyboard = DisplayServer.VirtualKeyboardGetHeight();
        Vector2I screen = DisplayServer.ScreenGetSize();

        return keyboard > 0 && screen.Y > 0 ? keyboard / (float)screen.Y * GetViewportRect().Size.Y : 0;
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

    // --skill 로 기술을 누르는 사이. 손 없이 확인할 때만 돈다.
    private double _skillWait;

    /// <summary>Presses one fan slot every so often, so a run with nobody watching shows what a technique draws.</summary>
    private void RehearseASkill(double delta)
    {
        if (Main.Ability.Length == 0 || !int.TryParse(Main.Ability, out int slot))
        {
            return;
        }

        _skillWait += delta;

        if (_skillWait < 1.5)
        {
            return;
        }

        _skillWait = 0;
        _abilities.Press(slot - 1);
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

    /// <summary>Adds a line to the messages, which fade on their own once read, and keeps it for reading back through.</summary>
    private void Notify(string line, bool speech = false)
    {
        line = MessageLog.Clean(line);
        _messages.Add(line);

        if (line.Length == 0)
        {
            return;
        }

        _history.Add((speech, line));

        if (_history.Count > HistoryKept)
        {
            _history.RemoveRange(0, _history.Count - HistoryKept);
        }
    }

    // 듣기가 멈춘 것을 한 번만 알린다.
    private bool _toldBroken;

    /// <summary>
    /// Says when the listening has stopped. It used to stop without a word — the character froze on screen while
    /// everything else looked fine, and nothing said why (2026-09-18 조사).
    /// </summary>
    private void Dropped()
    {
        if (_toldBroken || _server?.Broke is not { } why)
        {
            return;
        }

        _toldBroken = true;
        Notify($"연결이 끊겼습니다 — {why}");
    }

    /// <summary>
    /// Puts what people nearby said (0x0D) with the rest of the messages. A chant is a spell being said aloud as it is
    /// cast, not somebody talking, so it is left out.
    /// </summary>
    private void Listen()
    {
        if (_server is not { } server || server.HeardCount == _heardSeen)
        {
            return;
        }

        int missed = Math.Min(server.HeardCount - _heardSeen, server.Heard.Count);
        _heardSeen = server.HeardCount;

        foreach (Spoken spoken in server.Heard.TakeLast(missed))
        {
            if (spoken.Kind == SpeechKind.Chant)
            {
                continue;
            }

            // 서버가 이미 "이름: 말" 로 보낸다(Hades ServerFormat0D) — 이름을 한 번 더 붙이면 두 번 나온다.
            Notify(spoken.Text, speech: true);
        }
    }

    /// <summary>
    /// Opens or shuts what has been said. Like the pack it lies over the world, so the world takes no taps or steps
    /// while it is open, and the two never lie on top of each other.
    /// </summary>
    private void Chatting(bool open)
    {
        if (open)
        {
            if (_pack.Visible)
            {
                Carrying(false);
            }

            if (_talk.Visible)
            {
                ShutTalk();
            }

            _chat.Show(_history);
        }

        _chat.Visible = open;
        _world.Frozen = open;
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

        // 경험치는 막대 없이 숫자만 둔다 — 서버는 다음 레벨까지 얼마 남았는지만 말하고 그 레벨에 얼마가 드는지는
        // 말하지 않는다. 막대를 그리려면 길이를 지어내야 한다.
        _experience = Aux(string.Empty);

        vitals.AddChild(Gauge("HP", out _health, out _healthText));
        vitals.AddChild(Gauge("MP", out _mana, out _manaText));
        vitals.AddChild(_experience);

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

        string points = mine.Unspent > 0 ? $" · 점수 {mine.Unspent}" : string.Empty;

        _experience.Text = mine.Level <= 0
            ? string.Empty
            : (mine.ExperienceToGo <= 0 ? "EXP 다 올랐습니다" : $"EXP 다음까지 {mine.ExperienceToGo:N0}") + points;
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

        _pad = BuildMovementPad();

        if (Main.Portrait)
        {
            row.AddChild(_pad);
        }
        else
        {
            // 가로에는 기록 줄이 들어갈 자리가 없다 — 방향판 위에 얹는다(사용자, 2026-09-18). 판 하나에
            // 사람들이 한 말과 서버가 한 말이 함께 오른다.
            _messages = new MessageLog(2) { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            VBoxContainer left = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkEnd,
                MouseFilter = MouseFilterEnum.Ignore
            };
            left.AddThemeConstantOverride("separation", Main.Gutter);
            left.AddChild(BuildMessageRow());
            left.AddChild(_pad);

            row.AddChild(left);
        }

        // 세로는 방향판과 부채꼴 사이의 틈이 이 칸이다(360 폭에 152 + 8 + 184). 가로는 기록 줄이 방향판 위로
        // 올라가 비었으므로 두지 않는다 — 두면 남는 폭을 반씩 가져가 기록 줄이 좁아진다.
        if (Main.Portrait)
        {
            row.AddChild(new Control
            {
                CustomMinimumSize = new Vector2(Main.Gutter, 0),
                MouseFilter = MouseFilterEnum.Ignore
            });
        }

        _abilities = new AbilityBar { SizeFlagsVertical = SizeFlags.ShrinkEnd };
        _abilities.Cooling = (skill, slot) => _server?.CoolingFor(skill, slot) ?? 0;
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

    /// <summary>The messages with the button that opens what was said — the lines fade, this brings them back.</summary>
    private Control BuildMessageRow()
    {
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        _messages.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_messages);

        Button said = new()
        {
            Text = "대화",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        };
        said.Pressed += () => Chatting(true);
        row.AddChild(said);

        return row;
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }
}
