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
    /// <summary>체력·마력 구슬의 지름. 원작 구슬(86x85)을 이만큼으로 줄여 그림 없이 그린다.</summary>
    private const int Pip = 18;

    /// <summary>
    /// How tall the ticker's row is in portrait: two one-row lines, and the 대화 button beside them. It does not grow —
    /// the character stands in the middle of what is left above it (FocusY).
    /// </summary>
    private const int LogHeight = Main.TouchMinimum;

    private WorldView _world = null!;
    private Label _who = null!;
    private Label _place = null!;
    private Label _target = null!;
    private PackPanel _pack = null!;
    private TalkPanel _talk = null!;
    private FieldPanel _field = null!;
    private SettingsPanel _settings = null!;

    // 고른 곳의 맵 번호. 0x15(맵 바뀜)가 올 때까지 담아 둔다 — 그 전에는 알맹이의 _server.Field 가
    // 그대로 남아 있어(WorldClient.cs:363), 창을 도로 띄워 두 번 고르게 하면 안 된다.
    private int? _chosenField;

    // 닫기를 보냈을 때 알맹이의 FieldShown(WorldClient.cs 0x2E 셈)을 담아 둔다. 서버가 창을 거두면
    // (0x15 → Field null) 또는 새 창을 보내면(FieldShown 이 오르면) 풀린다 — 둘 다 서버가 보낸
    // 신호라 시간에 기대지 않는다. 취소가 영영 유실돼 서버가 창을 안 거두면 "지도" 단추가 계속
    // 막힌다 — 닫았는데 도로 열리는 것보다 낫고, 그때는 사람이 다시 접속한다(사용자 결정).
    private int? _closedAtFieldShown;

    // 창이 몇 번 열리고 닫혔나. 같은 말의 창이 다시 온 것과 아무 일 없는 것을 가르려고 센다.
    private int _talked;
    private MessageLog _messages = null!;
    private ChatPanel _chat = null!;

    // 파티(원작의 그룹) — 초대 단추·묻기·목록이 위 줄 아래 한 기둥에 선다(PartyColumn).
    private readonly PartyColumn _party = new();
    private bool _rosterAsked;
    private double _groupedFor = -1;
    private bool _partyRehearsed;
    private bool _partySaid;
    private bool _partyLeft;
    private Control _chatHolder = null!;
    private Control? _settingsHolder;
    private Control _over = null!;
    private const int ToastWidth = 150;

    /// <summary>What has been said, kept for reading back through — every line, wherever else it was shown.</summary>
    private readonly List<(MessageChannel Channel, string Text)> _history = [];

    // 얻은 것은 옆에 쌓이고, 큰일은 가운데 한 줄로 뜬다(MessageSort).
    private readonly ToastFeed _toasts = new();
    private readonly Banner _banner = new();

    // 마지막으로 가운데 띄운 곳 이름. 바뀔 때만 다시 띄운다.
    private string _bannered = string.Empty;

    // --chat: 월드가 자리를 잡은 뒤 기록 창을 그 탭으로 연다.
    private int _chatSettling;
    private const ulong ChatAfterMilliseconds = 12000;
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
    private Label _healthText = null!;
    private Label _manaText = null!;
    private Label _experience = null!;
    private Vitals? _shownVitals;

    private Control _packRow = null!;
    private Control? _log;

    // 손 없이 확인할 때 스스로 열어 보기 위한 것. 월드가 자리를 잡을 때까지 센다.
    private int _settling;

    // --map 을 따로 센다 — --pack 과 함께 주면 _settling 하나로는 둘 다 못 잰다.
    private int _mapSettling;

    // 지도가 뜬 뒤 사진 찍을 시간을 준 다음 닫기까지 눌러, 조작이 돌아오는 화면도 --map 하나로
    // --shot-after 만 달리해 잡을 수 있게 한다. 프레임 수로 세면 기기마다 빠르기가 달라 몇 초인지
    // 가늠이 안 된다 — 흐른 시간(초)으로 센다.
    private double _mapOpenSeconds;
    private const double MapCloseAfterSeconds = 5;
    private Button _map = null!;

    // 리허설로 한 번만 입어 본다.
    private bool _worn;

    // --gear-after: 소지품을 연 뒤(--wear 면 입기를 누른 뒤) 얼마나 지났나, 그리고 장비 탭으로 넘겼나.
    private const double GearAfterSeconds = 3;
    private double _wornFor;
    private bool _gearShown;
    private Control _topRow = null!;
    private Control _controlRow = null!;
    private Button _logout = null!;
    private bool _leaving;

    private readonly WorldClient? _server;

    /// <summary>Asks the host to replace this disposed game screen with a fresh login screen.</summary>
    public Action? LoggedOut { get; set; }

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
        _chat.Sent += line => _ = _chat.ToParty
            ? _server?.SayToGroupAsync(line, System.Threading.CancellationToken.None)
            : _server?.SayAsync(line, System.Threading.CancellationToken.None);

        _party.Invited += Invite;
        _party.Answered += (name, yes) =>
        {
            if (yes)
            {
                _ = _server?.AcceptGroupAsync(name, System.Threading.CancellationToken.None);
            }
            else
            {
                // 원작에는 "싫다"는 말이 없다 — 답하지 않는 것이 거절이다. 청한 쪽에는 아무것도 가지 않는다.
                Route(new Notice(MessageChannel.Party, MessagePlace.LogOnly, $"{name}님의 파티 초대를 거절했습니다.", string.Empty));
            }
        };
        _party.Left += () => _ = _server?.LeaveGroupAsync(System.Threading.CancellationToken.None);

        _field = new FieldPanel();
        _field.Chosen += area =>
        {
            _field.Visible = false;
            _chosenField = area;
            _ = _server?.ChooseFieldAsync(area, System.Threading.CancellationToken.None);
        };
        _field.Close.Pressed += () =>
        {
            _field.Visible = false;
            _closedAtFieldShown = _server?.FieldShown;
            _ = _server?.CloseFieldAsync(System.Threading.CancellationToken.None);
        };

        _settings = new SettingsPanel();
        _settings.Close.Pressed += () => _settings.Visible = false;
        _settings.Visible = Main.OpeningSettings;

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
            _messages = new MessageLog(2, wraps: false) { CustomMinimumSize = new Vector2(0, LogHeight) };
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

        // 얻은 것은 오른쪽 위 줄 바로 아래에 쌓인다 — 방향판에서 멀고, 가운데 캐릭터 옆을 비켜 간다. 가로는 부채꼴(공격)이
        // 위 줄 바로 밑까지 올라오므로 부채꼴 왼쪽 끝에 맞춘다(PlaceToasts). 창들보다 먼저 넣어 창이 열리면 그 아래로 간다.
        over.AddChild(_toasts);
        _over = over;

        // 파티는 위 줄 바로 아래 왼쪽에 — 세로는 방향판·부채꼴·기록 줄이 모두 아래에 있어 비어 있는 자리다. 가로는 왼쪽 아래
        // 방향판과 그 위 기록 줄이 위 줄 가까이까지 올라오므로 방향판 오른쪽 옆으로 비킨다(PlaceParty). 창들보다 먼저
        // 넣어 창이 열리면 그 아래로 간다.
        over.AddChild(_party);
        _party.AnchorLeft = 0;
        _party.AnchorRight = 0;
        _party.CustomMinimumSize = new Vector2(PartyColumn.Wide, 0);
        _topRow.Resized += () => _party.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + Main.Gutter;
        _toasts.AnchorLeft = 1;
        _toasts.AnchorRight = 1;
        _toasts.OffsetLeft = -ToastWidth;
        _toasts.OffsetRight = 0;
        _toasts.GrowHorizontal = GrowDirection.Begin;
        _topRow.Resized += () =>
        {
            // 가로는 가운데 한 줄(배너)과 높이가 겹쳐 레벨이 오를 때 글자가 포개졌다 — 그 아래에서 시작한다.
            _toasts.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + Main.Gutter + (Main.Portrait ? 0 : 40);
            _toasts.OffsetBottom = _toasts.OffsetTop + 120;
        };

        // 큰일은 가운데, 캐릭터 머리보다 위에 — 위 줄과 캐릭터 사이.
        over.AddChild(_banner);
        _banner.AnchorLeft = 0;
        _banner.AnchorRight = 1;
        _banner.AnchorTop = Main.Portrait ? 0.27f : 0.24f;
        _banner.AnchorBottom = _banner.AnchorTop;
        _banner.OffsetTop = -20;
        _banner.OffsetBottom = 20;

        // 창은 아래에 붙는다. 대화 창과 장비 고리는 남는 높이를 다 쓰고, 소지품 한 장은 제 높이만큼만 올라와
        // 위쪽 맵을 남긴다(PackPanel.ShowTab 이 정한다).
        _talk.SizeFlagsVertical = SizeFlags.ExpandFill;

        // 곳이 스물넷이라 남는 높이를 다 쓴다 — 대화 창과 같다.
        _field.SizeFlagsVertical = SizeFlags.ExpandFill;

        // 대화 창은 제 높이만큼만 아래에 붙는다 — 소지품 한 장과 같다. 긴 이야기는 창 안에서 굴린다.
        _chat.SizeFlagsVertical = SizeFlags.ShrinkEnd;

        List<VBoxContainer> holders = [];

        foreach (Control panel in new Control[] { _pack, _talk, _chat, _field, _settings })
        {
            VBoxContainer holder = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
            over.AddChild(holder);
            holder.AddChild(panel);
            holders.Add(holder);

            if (panel == _chat)
            {
                _chatHolder = holder;
            }

            if (panel == _settings)
            {
                _settingsHolder = holder;
            }

            holder.SetAnchorsPreset(LayoutPreset.FullRect);
            holder.OffsetLeft = 0;
            holder.OffsetTop = Main.TouchMinimum + (Main.Gutter * 3);
            holder.OffsetRight = 0;
            holder.OffsetBottom = 0;

            // 가로 소지품·장비 창은 화면 높이를 거의 다 쓴다 — 위 줄 아래에서 시작하면 장비 고리 여섯 줄이 한 화면에 안
            // 들어 굴려야 했고, 사용자가 그건 못 쓴다고 했다(2026-09-23). 열려 있는 동안 오른쪽 위 줄을 덮고, 닫기는 창 안에 있다.
            if (panel == _pack && !Main.Portrait)
            {
                holder.OffsetTop = 0;
                continue;
            }

            // 가로 설정 창은 오른쪽 기둥에 서면 공격 단추와 기술 부채꼴을 덮었다(사용자, 2026-09-23). 설정은 월드를 멈추지
            // 않으므로 조작이 살아 있어야 한다 — 위 줄 바로 아래, 방향판과 부채꼴 사이 가운데에 제 크기만큼만 선다.
            if (panel == _settings && !Main.Portrait)
            {
                holder.Alignment = BoxContainer.AlignmentMode.Begin;
                _settings.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            }

            // 창은 위 줄이 실제로 끝나는 곳 아래에서 시작한다. 세로 위 줄은 단추 줄이 붙어 두 줄(120)이라, 가로 위 줄
            // 높이로 박아 둔 자리(72)에서 시작하면 장비 탭이 인벤토리·지도·로그아웃을 덮었다.
            _topRow.Resized += () => holder.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + Main.Gutter;
        }

        PlaceColumn(holders);

        // 창의 최소 폭은 글자를 잰 뒤에야 맞는다. 처음 잰 값이 모자랐다(314 — 창은 438) — 바뀔 때마다 다시 세운다.
        _pack.MinimumSizeChanged += () => PlaceColumn(holders);
    }

    /// <summary>
    /// Stands the windows in landscape in a column against the right edge, full width upright. The column is a bit over
    /// a third of the width, more where the pack window needs more — on a 16:9 screen it is about half. The window's
    /// width is its own minimum; the share is of the width inside the safe margins, not of the screen — counted from the
    /// screen, the gear window came up 9 short and ran past the right edge of a 640 screen.
    /// </summary>
    private void PlaceColumn(IReadOnlyList<VBoxContainer> holders)
    {
        float across = GetViewportRect().Size.X;
        float column = across > 0
            ? SideColumn.LeftAnchor(across, Main.SafeInsets.Left, Main.SafeInsets.Right, _pack.GetCombinedMinimumSize().X, 0.6f)
            : 0.6f;

        foreach (VBoxContainer holder in holders)
        {
            holder.AnchorLeft = Main.Portrait || holder == _settingsHolder ? 0 : column;
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

        // A real portrait status plate can already use half the safe width once name, HP, MP and EXP arrive.
        // Keep all three 48px actions in a second line of the same top status area instead of squeezing the
        // last one beyond the right safe edge. Landscape has the width and keeps the established single row.
        HBoxContainer actions = Main.Portrait
            ? new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }
            : row;
        actions.AddThemeConstantOverride("separation", Main.Gutter);

        Button pack = new()
        {
            Text = "인벤토리",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        Greybox.Plain(pack);
        pack.Pressed += () => Carrying(true);
        actions.AddChild(pack);

        _map = new Button
        {
            Text = "지도",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        Greybox.Plain(_map);
        _map.Pressed += () => _ = _server?.OpenFieldAsync(System.Threading.CancellationToken.None);
        actions.AddChild(_map);

        _logout = new Button
        {
            Text = "로그아웃",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        Greybox.Plain(_logout);
        _logout.Pressed += LogOut;


        Button settings = new()
        {
            Text = "설정",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        Greybox.Plain(settings);
        settings.Pressed += () => _settings.Visible = !_settings.Visible;

        actions.AddChild(settings);
        actions.AddChild(_logout);

        if (Main.Portrait)
        {
            foreach (Button action in new Button[] { pack, _map, settings, _logout })
            {
                action.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            }

            VBoxContainer top = new() { MouseFilter = MouseFilterEnum.Ignore };
            top.AddThemeConstantOverride("separation", Main.Gutter);
            top.AddChild(row);
            top.AddChild(actions);

            return top;
        }

        return row;
    }

    /// <summary>
    /// Closes the network before asking Main for a deferred tree change. The exit callback below is a
    /// second safety net, so WorldClient and every owner beneath it make Dispose idempotent.
    /// </summary>
    private void LogOut()
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        _logout.Disabled = true;
        _world.Frozen = true;
        _server?.Dispose();
        LoggedOut?.Invoke();
    }

    public override void _ExitTree() => _server?.Dispose();

    /// <summary>
    /// A panel over the world: an original stone frame with a dark, nearly opaque inside. The frame is what
    /// carries the theme; the inside is flat, because a pattern under small text is the first thing to fail.
    /// </summary>
    private static Control Plated(Control inside)
    {
        PanelContainer inner = new();
        inner.AddThemeStyleboxOverride("panel", Greybox.Plate());
        inner.AddChild(inside);

        PanelContainer plate = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        plate.AddThemeStyleboxOverride("panel", Greybox.Stone());
        plate.AddChild(inner);

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

        while (_server is { } server && server.TakeTold(out byte type, out string told))
        {
            if (MessageSort.FromServer(type, told) is { } notice)
            {
                Route(notice);

                // 누가 들어오고 나갔다는 말이 오면 목록을 다시 받는다 — 서버는 목록을 스스로 보내지 않는다(Party).
                if (notice.Channel == MessageChannel.Party && type != GroupChat)
                {
                    _ = _server?.AskProfileAsync(System.Threading.CancellationToken.None);
                }
            }
        }

        Listen();
        KeepParty(delta);
        PlaceToasts();
        Entered();
        OpenChatOnItsOwn();
        RehearseNotices();
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

        // 손 없이 확인할 때만. 같은 규칙 — 옆 단추(인벤토리)가 쓰는 것을 그대로 쓴다: 눌러 보는 것은
        // 잇는 서버 말이 아니라 단추 자신의 눌림(EmitSignal) — 배선까지 확인된다. 뜬 것을 잠시 두었다가
        // 닫기까지 눌러, 열린 화면과 닫아 조작이 돌아온 화면을 --shot-after 만 달리해 --map 하나로 잡는다.
        if (Main.OpeningMap)
        {
            if (_server?.Field is null && _closedAtFieldShown is null && _mapSettling++ == settle)
            {
                _map.EmitSignal(BaseButton.SignalName.Pressed);
            }

            if (_field.Visible && (_mapOpenSeconds += delta) >= MapCloseAfterSeconds)
            {
                _field.Close.EmitSignal(BaseButton.SignalName.Pressed);
            }
        }

        // 닫는 동안(또는 창이 떠 있는 동안) "지도"를 다시 누르면 그 0xF0 이 닫기의 0x15 와 한 프레임에
        // 겹쳐 위 표시가 영영 굳을 수 있었다 — 막아서 그 경주 자체를 없앤다.
        _map.Disabled = _closedAtFieldShown is not null || _field.Visible;

        // 월드맵은 서버가 띄우는 것이지 사람이 여는 것이 아니다. 온 것을 그대로 보여 준다.
        // 한 곳을 고른 뒤(_chosenField)에는 0x15(맵 바뀜)로 알맹이가 비울 때까지 다시 띄우지 않는다 —
        // 서버가 맵을 새로 보내기까지 두 번의 0.5초를 거치는 동안(GameServerHandlers.cs:1885-1890)
        // _server.Field 가 그대로 남아 있어, 그새 창을 도로 띄우면 두 번 고를 수 있었다. 닫기를
        // 보낸 뒤에는(_closedAtFieldShown) FieldShown 이 그때와 달라졌을 때만 — 즉 서버가 새 창을
        // 보냈을 때만 — 다시 띄운다.
        if (_server?.Field is { } field && !_field.Visible && _chosenField is null &&
            (_closedAtFieldShown is null || _server?.FieldShown != _closedAtFieldShown))
        {
            _field.Show(field);
            _closedAtFieldShown = null;
        }
        else if (_server?.Field is null)
        {
            // 보내기가 실패해 서버가 영영 맵을 안 바꾸면(고르기도, 닫기도) 창이 다시 안 뜬다 — 두 번
            // 이동하거나 닫았는데 도로 열리는 것보다 안 뜨는 편이 낫다고 보고, 그때는 사람이 다시 접속한다.
            _field.Visible = false;
            _chosenField = null;
            _closedAtFieldShown = null;
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

            // 손 없이 확인할 때만(--gear-after). 소지품 탭을 보인 뒤 — --wear 면 입기를 누르고 서버가 답할 틈을 둔 뒤 —
            // 장비 탭으로 넘겨, 소지품에서 빠진 것이 장비 칸에 앉은 것까지 한 번에 찍는다.
            if (Main.GearAfter && (_worn || !Main.Wearing) && !_gearShown && (_wornFor += delta) >= GearAfterSeconds)
            {
                _gearShown = true;
                ShowGearTab(true);
            }
        }
    }

    /// <summary>0x0A kind 11 — somebody talking to the group, not the server saying the group changed.</summary>
    private const byte GroupChat = 11;

    /// <summary>
    /// Keeps the party column in step: asks for the list once on entering, puts up whoever is asking us, shows the list
    /// with each member's health, and offers 파티 초대 only for a person who could join.
    /// </summary>
    private void KeepParty(double delta)
    {
        PlaceParty();

        if (_server is not { } server)
        {
            return;
        }

        if (!_rosterAsked && server.State is not null && server.Self is not null)
        {
            _rosterAsked = true;
            _ = server.AskProfileAsync(System.Threading.CancellationToken.None);
        }

        while (server.TakeAsk(out string? asker))
        {
            _party.Ask(asker);
            Route(new Notice(MessageChannel.Party, MessagePlace.LogOnly, $"{asker}님이 파티에 초대합니다.", string.Empty));
        }

        string self = server.Self?.Name ?? string.Empty;
        PartyRoster roster = server.Roster;

        _party.Show(roster, self, name =>
            server.Others.FirstOrDefault(other => other.Name == name) is { } seen ? server.Health(seen.Serial) : null);

        Character? picked = server.Others.FirstOrDefault(other => other.Serial == _world.Target);
        bool leading = !roster.Grouped || roster.Members.Any(member => member.Leader && member.Name == self);

        _party.CanInvite(picked is { Name.Length: > 0 } && leading &&
                         !roster.Members.Any(member => member.Name == picked.Name));

        RehearseParty(delta);
    }

    /// <summary>Asks whoever is picked out to join. The server says nothing back to the asker, so this screen says it.</summary>
    private void Invite()
    {
        if (_server?.Others.FirstOrDefault(other => other.Serial == _world.Target) is not { Name.Length: > 0 } person)
        {
            return;
        }

        _ = _server.AskToGroupAsync(person.Name, System.Threading.CancellationToken.None);
        Route(new Notice(MessageChannel.Party, MessagePlace.Ticker, $"{person.Name}님을 파티에 초대했습니다.", string.Empty));
    }

    /// <summary>
    /// Stands the party column under the top row: at the left edge in portrait, beside the movement pad in landscape —
    /// there the pad and the lines over it reach up close to the top row. Read from where the pad really is, because
    /// the control row is capped and centred on a wide screen.
    /// </summary>
    private void PlaceParty()
    {
        _party.OffsetLeft = Main.Portrait
            ? 0
            : _pad.GetGlobalRect().End.X - _over.GetGlobalRect().Position.X + Main.Gutter;
        _party.OffsetRight = _party.OffsetLeft + PartyColumn.Wide;
    }

    /// <summary>
    /// Only when checking without a hand: taps the person named by <c>--invite</c> and presses 파티 초대, presses 수락 for
    /// <c>--accept</c>, sends <c>--party-say</c> from the 파티 tab once grouped, and presses 나가기 <c>--leave-after</c>
    /// seconds after that. Each press is the button's own signal, so the wiring is what gets checked.
    /// </summary>
    private void RehearseParty(double delta)
    {
        if (Main.Inviting.Length > 0 && !_partyRehearsed && Time.GetTicksMsec() > 6000)
        {
            if (_world.TargetName == Main.Inviting && _party.Invite.Visible)
            {
                _partyRehearsed = true;
                _party.Invite.EmitSignal(BaseButton.SignalName.Pressed);
                GD.Print($"GREYBOX_PARTY 초대 {Main.Inviting}");
            }
            else if (_world.TargetName != Main.Inviting)
            {
                _world.TapPerson(Main.Inviting);
            }
        }

        if (Main.Accepting && _party.Asking)
        {
            _party.AcceptButton.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print("GREYBOX_PARTY 수락");
        }

        _groupedFor = _party.Grouped ? Math.Max(_groupedFor, 0) + delta : -1;

        if (Main.PartySaying.Length > 0 && !_partySaid && _groupedFor > 2)
        {
            _partySaid = true;
            Chatting(true);
            _chat.Rehearse(Main.PartySaying);
            GD.Print($"GREYBOX_PARTY 말 {Main.PartySaying}");
        }

        if (Main.LeavingAfter >= 0 && !_partyLeft && _groupedFor > Main.LeavingAfter)
        {
            _partyLeft = true;
            _party.Leave.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print("GREYBOX_PARTY 나가기");
        }

        if (_party.Grouped || _party.Asking || _party.Invite.Visible)
        {
            Rect2 column = _party.GetGlobalRect();

            foreach ((string name, Control part) in new (string, Control)[]
                     { ("방향판", _pad), ("부채꼴", _abilities), ("기록 줄", _messages), ("위 줄", _topRow) })
            {
                if (part.IsVisibleInTree() && column.Intersects(part.GetGlobalRect()))
                {
                    GD.Print($"GREYBOX_PARTY_OVERLAP {name}");
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
        // "m3" 은 마법 쪽 세 번째 칸이다.
        bool spell = Main.Ability.StartsWith('m');

        if (Main.Ability.Length == 0 || !int.TryParse(spell ? Main.Ability[1..] : Main.Ability, out int slot))
        {
            return;
        }

        _skillWait += delta;

        if (_skillWait < 1.5)
        {
            return;
        }

        _skillWait = 0;
        _abilities.Press(slot - 1, spell);
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

    /// <summary>Says something this screen itself has to say — a refusal, a lost connection. It goes on the ticker.</summary>
    private void Notify(string line) => Route(MessageSort.Own(line));

    /// <summary>
    /// Puts one sorted line where it belongs (<see cref="MessageSort" />) and keeps it for reading back through. Every
    /// line goes to the log; only some of them go anywhere over the world.
    /// </summary>
    private void Route(Notice notice, uint speaker = 0)
    {
        if (notice.Text.Length == 0)
        {
            return;
        }

        _history.Add((notice.Channel, notice.Text));

        if (_history.Count > HistoryKept)
        {
            _history.RemoveRange(0, _history.Count - HistoryKept);
        }

        switch (notice.Place)
        {
            case MessagePlace.Ticker:
                _messages.Add(notice.Text);
                break;

            case MessagePlace.Toast:
                _toasts.Add(notice.Short);
                break;

            case MessagePlace.Banner:
                _banner.Show(notice.Short, bright: notice.Short == "레벨이 올랐습니다");
                break;

            case MessagePlace.Bubble:
                _world.Speak(speaker, notice.Short);
                break;
        }

        GD.Print($"GREYBOX_MESSAGE {Time.GetTicksMsec() / 1000.0:0.0}s {notice.Place} {notice.Channel} {notice.Text}");
    }

    /// <summary>
    /// Keeps the toasts clear of the attack fan in landscape. The fan sits where the width cap puts it, not against the
    /// screen edge, so its left end is read rather than assumed.
    /// </summary>
    private void PlaceToasts()
    {
        if (Main.Portrait)
        {
            return;
        }

        float fanLeft = _abilities.GetGlobalRect().Position.X - _over.GetGlobalRect().Position.X;
        _toasts.AnchorLeft = 0;
        _toasts.AnchorRight = 0;
        _toasts.OffsetRight = fanLeft - Main.Gutter;
        _toasts.OffsetLeft = _toasts.OffsetRight - ToastWidth;
    }

    /// <summary>Names the place in the middle of the screen when the character comes into it.</summary>
    private void Entered()
    {
        string place = _world.PlaceName;

        if (place.Length == 0 || place == _bannered)
        {
            return;
        }

        _bannered = place;
        _banner.Show(place);
    }

    // --notices 를 한 번만 흘린다.
    private int _noticeWait;

    /// <summary>
    /// Only when checking without a server (<c>--notices</c>): puts lines Hades really sends through the same sorting
    /// the live screen uses, so each place they land can be seen at once.
    /// </summary>
    private void RehearseNotices()
    {
        if (!Main.Noticing || _server is not null || _noticeWait < 0 || _noticeWait++ < 30)
        {
            return;
        }

        _noticeWait = -1;

        foreach ((byte type, string line) in new (byte, string)[]
        {
            (2, "you cast dion."),
            (2, "Your skin is already like stone."),
            (2, "You can't attack that."),
            (2, "길이 막혀 가까운 곳으로 옮겼습니다."),
            (2, "쿠룸 Received."),
            (3, "You've Received 120 coins."),
            (2, "You received 1164 Experience!."),
            (2, "Your insight has increased!")
        })
        {
            if (MessageSort.FromServer(type, line) is { } notice)
            {
                Route(notice);
            }
        }
    }

    /// <summary>Only when checking without a hand (<c>--chat 시스템</c>): opens the full log on that tab.</summary>
    private void OpenChatOnItsOwn()
    {
        // 사냥이 몇 줄을 쌓을 틈을 준다 — 창이 열리면 월드가 멈춘다.
        if (Main.ChatTab.Length == 0 || _chat.Visible || _chatSettling < 0 || Time.GetTicksMsec() < ChatAfterMilliseconds)
        {
            return;
        }

        _chatSettling = -1;
        Chatting(true);
        _chat.Choose(Main.ChatTab switch
        {
            "일반" or "general" => MessageChannel.General,
            "파티" or "party" => MessageChannel.Party,
            "시스템" or "system" => MessageChannel.System,
            _ => null
        });
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
            // 서버가 이미 "이름: 말" 로 보낸다(Hades ServerFormat0D) — 기록에는 그대로, 머리 위에는 이름을 뗀 말만.
            if (MessageSort.FromSpeech(spoken.Kind, spoken.Text) is { } notice)
            {
                Route(notice, spoken.Serial);
            }
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

        vitals.AddChild(Gauge("체력", Greybox.Health, out _healthText));
        vitals.AddChild(Gauge("마력", Greybox.Mana, out _manaText));
        vitals.AddChild(_experience);

        return vitals;
    }

    /// <summary>
    /// One vital: the original's bead, shrunk to a flat disc, and the exact numbers beside it. No long bar —
    /// how a fight is going is read over the head now (HealthBar), and the same thing is not drawn twice.
    /// </summary>
    private static Control Gauge(string name, Color paint, out Label text)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);

        StyleBoxFlat bead = new() { BgColor = paint, BorderColor = new Color(0, 0, 0, 0.55f) };
        bead.SetCornerRadiusAll(Pip / 2);
        bead.SetBorderWidthAll(2);

        Panel pip = new()
        {
            CustomMinimumSize = new Vector2(Pip, Pip),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };

        pip.AddThemeStyleboxOverride("panel", bead);

        // 구슬만 두었더니 무엇을 뜻하는지 알 수 없다는 말을 들었다(사용자, 2026-09-18). 이름을 되살린다 —
        // 색은 거드는 것이지 뜻을 나르는 것이 아니다.
        Label named = Aux(name);

        text = Aux(string.Empty);
        row.AddChild(pip);
        row.AddChild(named);
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
        Fill(_healthText, mine.Health, mine.MaximumHealth);
        Fill(_manaText, mine.Mana, mine.MaximumMana);

        string points = mine.Unspent > 0 ? $" · 점수 {mine.Unspent}" : string.Empty;

        _experience.Text = mine.Level <= 0
            ? string.Empty
            : (mine.ExperienceToGo <= 0 ? "EXP 다 올랐습니다" : $"EXP 다음까지 {mine.ExperienceToGo:N0}") + points;
    }

    /// <summary>
    /// Writes one vital. It turns colour as it falls — but the numbers themselves are the reading, so somebody
    /// who cannot tell the colours apart loses nothing.
    /// </summary>
    private static void Fill(Label text, int left, int most)
    {
        text.Text = $"{left} / {most}";

        text.AddThemeColorOverride("font_color", most <= 0 || left > most * 0.5
            ? Greybox.Text
            : left > most * 0.15 ? Greybox.Health : Greybox.Gone);
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
            // 가로에는 기록 줄이 들어갈 자리가 없다 — 방향판 위에 얹는다(사용자, 2026-09-18). 이쪽은
            // 기록판이 아니라 잠깐 뜨는 토스트다. 중앙의 캐릭터를 가리지 않도록, 세 칸 방향판 너비를
            // 넘지 않는다. 긴 말은 그 안에서 줄바꿈하고 [대화]가 지난 말을 모두 다시 보여 준다.
            int toastWidth = MessageToastLayout.DirectionPadWidth(Main.TouchMinimum, Main.Gutter / 2);
            _messages = new MessageLog(2, wraps: true) { CustomMinimumSize = new Vector2(toastWidth, 0) };

            VBoxContainer left = new()
            {
                CustomMinimumSize = new Vector2(toastWidth, 0),
                SizeFlagsVertical = SizeFlags.ShrinkEnd,
                MouseFilter = MouseFilterEnum.Ignore
            };
            left.AddThemeConstantOverride("separation", Main.Gutter);
            left.AddChild(BuildMessageRow());
            left.AddChild(_pad);

            row.AddChild(left);

            // The empty middle takes the extra width, not the toast. That keeps the two thumb clusters at
            // opposite sides while leaving their play area clear.
            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
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

        // 자동 포션은 창 안에 숨기지 않는다 — 싸우는 중에 한 번에 닿아야 한다(사용자, 2026-09-23). 위 줄에 있던 것을
        // 기술 부채꼴 맨 위, 가장 높은 기술 칸 위로 옮겼다 — 기술 칸(48)보다 조금 작게(사용자, 2026-09-23 "기술창 제일
        // 상단쪽에 … 기술창 보다 조금 작게"). 마실 포션의 그림에 줄을 작게 적는다. 누르면 켜고 끄기, 길게 누르면 다른
        // 포션을 고른다. 줄은 설정 창에서.
        _abilities.Hold(new PotionChip(AutoPotion.Healing,
            () => Main.HealthPotion, rule => Main.SetPotions(rule, Main.ManaPotion), () => _server?.Pack ?? []), 0);
        _abilities.Hold(new PotionChip(AutoPotion.Restoring,
            () => Main.ManaPotion, rule => Main.SetPotions(Main.HealthPotion, rule), () => _server?.Pack ?? []), 1);

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

        // 대화 단추는 손댈 자리를 지키려고 48 높이를 요구한다(TouchMinimum) — 여기서 ShrinkEnd 를 안 주면
        // 줄이 한 줄뿐이어도 판이 그 48 높이까지 늘어나 아래에 빈 검정이 남는다. 세로는 이미 LogHeight 만큼
        // 커스텀 최소 높이를 주므로(단추의 48보다 커) 줄지 않는다 — 그대로다.
        _messages.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _messages.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        _messages.Tapped += () => Chatting(true);
        row.AddChild(_messages);

        Button said = new()
        {
            Text = "대화",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
            SizeFlagsVertical = SizeFlags.ShrinkEnd
        };
        Greybox.Plain(said);
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
