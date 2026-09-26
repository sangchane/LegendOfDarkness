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

    /// <summary>체력·마력 막대의 높이 — 고른 대상의 막대(_targetHealth)와 같은 높이라 HUD가 한 체계로 읽힌다.</summary>
    private const int GaugeHeight = 10;

    /// <summary>막대 옆 숫자의 글자 크기. 작게 두어(사용자 지시) 막대를 더한 만큼 판이 넓어지지 않게 한다.</summary>
    private const int GaugeFontSize = 11;

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

    // 길 찾기 — 원작의 Tab 지도. 위 줄의 미니맵을 누르면 연다. 길을 걷는 동안은 위 줄 아래 가운데에 간 곳과 [멈춤]이 뜬다.
    private TabMapPanel _tabMap = null!;
    private MinimapView _minimap = null!;
    private MapGuide _guide = MapGuide.Empty;

    // 큰 창은 한 번에 하나만(사용자, 2026-09-26). 여는 것은 모두 SetWindow 를 지난다.
    private readonly OneWindow _windows = new();
    private Control _guideChip = null!;
    private Label _guideText = null!;
    private int _tabMapSettling;
    private double _tabMapOpenFor = -1;
    private bool _tabMapWent;
    private SettingsPanel _settings = null!;
    private readonly BotGearPanel _botGear = new();
    private Control? _botGearHolder;

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
    private ProgressBar _healthBar = null!;
    private ProgressBar _manaBar = null!;
    private Label _healthText = null!;
    private Label _manaText = null!;
    private Label _experience = null!;
    // 버프·디버프 아이콘 — 체력·마력 판의 첫 줄, 이름 옆(사용자, 2026-09-26). 다섯까지, 나머지는 "+N".
    private readonly StatusStrip _myStatus = new(side: 12, most: 5);
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

    // 자동 사냥 켜고 끄기 — 공격 단추를 0.5초 길게 눌러서 한다(위 줄의 [자동] 단추는 없앴다, 사용자 요청
    // 2026-09-26). 켜져 있으면 공격 단추 자체가 표시한다(AbilityBar.ShowAutoHunt).
    private bool _autoHuntDrawn;
    private bool _autoHuntPausedDrawn;
    private int _autoHuntSettling;
    private int _companionSettling; // --companion: 자리를 잡은 뒤 [동료 부르기] 를 한 번 누르기까지 센 프레임.

    // 설정 → 계정 탭의 [종료] 가 여는 작은 판 — 로그아웃 · 게임 종료 · 취소.
    private readonly ExitChoice _exit = new();

    // --exit-menu 로 설정 → 계정 → [종료] 를 누르기까지 센 프레임.
    private int _exitSettling;
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
        [("위 줄", _topRow), ("미니맵", _minimap), ("조작 줄", _controlRow), ("방향판", _pad), ("파티원", _party.Members), ("인벤토리", _pack), ("월드", _world)];

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

        _guide = LoadGuide();

        // 미니맵은 위 줄 안에 선다 — 월드를 먼저 지어야 한다(무엇을 그릴지 월드에게 묻는다).
        BuildWorld();
        _minimap = new MinimapView(_world, _server, _guide);
        _minimap.Pressed += () => SetWindow(GameWindow.TabMap, !_tabMap.Visible);

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

        _field = new FieldPanel(_guide);
        _field.Chosen += area =>
        {
            _chosenField = area;
            SetWindow(GameWindow.WorldMap, false);
            _ = _server?.ChooseFieldAsync(area, System.Threading.CancellationToken.None);
        };
        _field.Close.Pressed += () =>
        {
            CancelField();
            _windows.Shut(GameWindow.WorldMap);
            _world.Frozen = _windows.Freezing;
        };

        _settings = new SettingsPanel();
        _settings.Close.Pressed += () => SetWindow(GameWindow.Settings, false);

        // [종료] 는 위 줄에서 설정 창 제목 줄의 [로그아웃] 으로 옮겼다(2026-09-26). 판은 그대로 — 로그아웃 · 게임 종료 · 취소.
        _settings.Exit.Pressed += () =>
        {
            if (_exit.Visible)
            {
                _exit.Shut();
            }
            else
            {
                _exit.Open(_settings.Exit.GetGlobalRect(), centred: true);
            }
        };
        _settings.Companion.Pressed += () => _ = _server?.Companion is null
            ? _server?.CallCompanionAsync(System.Threading.CancellationToken.None)
            : _server.DismissCompanionAsync(System.Threading.CancellationToken.None);

        // 봇 칸을 누르면 봇 장비창. 주기·벗기기는 우리 확장 0xF1 2·3, 결과는 서버 알림과 봇 장비 안내(0x5E 종류 5).
        _party.BotOpened += () => SetWindow(GameWindow.BotGear, !_botGear.Visible);
        _botGear.Close.Pressed += () => SetWindow(GameWindow.BotGear, false);
        _botGear.Given += (slot, count) => _ = _server?.GiveToCompanionAsync(slot, count, System.Threading.CancellationToken.None);
        _botGear.TakenOff += place => _ = _server?.TakeOffCompanionAsync(place, System.Threading.CancellationToken.None);

        _talk = new TalkPanel();
        _talk.Close.Pressed += ShutTalk;
        _talk.Answered += (speaker, step, words) => _ = words is null
            ? _server?.AnswerAsync(speaker, step, System.Threading.CancellationToken.None)
            : _server?.AnswerAsync(speaker, step, words, System.Threading.CancellationToken.None);

        // The world fills the screen and the HUD floats over it, in both orientations. Portrait used to give the world a
        // row of its own above the controls, which left a third of the screen black behind the buttons (사용자,
        // 2026-09-18). Nothing the player aims at goes under a thumb all the same: the character stands in the middle of
        // the part the controls leave uncovered (WorldView.FocusY).
        _controlRow = BuildControlRow();

        _tabMap = new TabMapPanel(_world, _server, _guide);
        _tabMap.Close.Pressed += () => SetWindow(GameWindow.TabMap, false);

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

        // 맨 위에 둔다 — 판 밖 어디를 눌러도 닫히도록 화면 전체를 받는다.
        _exit.LogOut.Pressed += () =>
        {
            _exit.Shut();
            LogOut();
        };
        _exit.Quit.Pressed += () =>
        {
            _exit.Shut();
            QuitGame();
        };
        AddChild(_exit);

        if (Main.OpeningSettings)
        {
            SetWindow(GameWindow.Settings, true);
        }
    }

    /// <summary>
    /// Shows or hides one big window. Opening one first puts away whichever was open (<see cref="OneWindow" />), and the
    /// world stops taking taps and steps only while a window that lies over it is up.
    /// </summary>
    private void SetWindow(GameWindow window, bool open)
    {
        if (open)
        {
            if (_windows.Open(window) is { } before)
            {
                PutAway(before);
            }

            WindowOf(window).Visible = true;

            if (window == GameWindow.TabMap)
            {
                _tabMap.Open();
            }
        }
        else
        {
            _windows.Shut(window);
            WindowOf(window).Visible = false;
        }

        _world.Frozen = _windows.Freezing;
    }

    private Control WindowOf(GameWindow window) => window switch
    {
        GameWindow.Pack => _pack,
        GameWindow.Talk => _talk,
        GameWindow.Chat => _chat,
        GameWindow.WorldMap => _field,
        GameWindow.Settings => _settings,
        GameWindow.TabMap => _tabMap,
        _ => _botGear
    };

    /// <summary>
    /// Puts a window away because another is taking its place. An NPC's talk and the world map have to tell the server
    /// — it keeps walking us through a menu, or holds every other packet, until it hears they were shut.
    /// </summary>
    private void PutAway(GameWindow window)
    {
        switch (window)
        {
            case GameWindow.Talk:
                _talk.Visible = false;
                _ = _server?.ShutDialogueAsync(System.Threading.CancellationToken.None);
                break;

            case GameWindow.WorldMap:
                CancelField();
                break;

            default:
                WindowOf(window).Visible = false;
                break;
        }
    }

    /// <summary>Takes the world map down and tells the server "none" (map 0), which is what gives the hands back.</summary>
    private void CancelField()
    {
        _field.Visible = false;
        _closedAtFieldShown = _server?.FieldShown;
        _ = _server?.CloseFieldAsync(System.Threading.CancellationToken.None);
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
        // 봇 칸은 파티 기둥 밖, 화면 왼쪽 가장자리에 딱 붙는다(사용자, 2026-09-26) — 위 줄 바로 아래(PlaceParty).
        over.AddChild(_party.BotSlot);

        // 파티원 칸들 — 봇 칸 아래(세로) · 봇 칸 옆(가로), 왼쪽 가장자리부터(사용자, 2026-09-26: 그룹원 체력 정보 창).
        over.AddChild(_party.Members);
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

        // 월드맵 카드 — 세로는 제 높이만큼만 아래에 붙어 위쪽 맵을 남기고(카드가 많으면 창 안에서 굴린다), 가로는 오른쪽 기둥을 다 쓴다.
        _field.SizeFlagsVertical = Main.Portrait ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill;

        // 대화 창은 제 높이만큼만 아래에 붙는다 — 소지품 한 장과 같다. 긴 이야기는 창 안에서 굴린다.
        _chat.SizeFlagsVertical = SizeFlags.ShrinkEnd;

        // 길 찾기 창도 제 높이만큼만 — 세로는 그 위로 걸어가는 캐릭터가 보이고, 가로는 오른쪽 기둥 전체를 쓴다.
        _tabMap.SizeFlagsVertical = Main.Portrait ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill;

        // 길을 걷는 동안의 표시 — 창을 닫아도 어디로 가는지와 멈추는 단추가 남는다. 창들보다 먼저 넣어 창 아래로 간다.
        over.AddChild(_guideChip = BuildGuideChip());

        List<VBoxContainer> holders = [];

        foreach (Control panel in new Control[] { _pack, _talk, _chat, _field, _settings, _tabMap, _botGear })
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

            if (panel == _botGear)
            {
                _botGearHolder = holder;
            }

            holder.SetAnchorsPreset(LayoutPreset.FullRect);
            holder.OffsetLeft = 0;
            holder.OffsetTop = Main.TouchMinimum + (Main.Gutter * 3);
            holder.OffsetRight = 0;
            holder.OffsetBottom = 0;

            // 가로 소지품·장비 창은 화면 높이를 거의 다 쓴다 — 위 줄 아래에서 시작하면 장비 고리 여섯 줄이 한 화면에 안
            // 들어 굴려야 했고, 사용자가 그건 못 쓴다고 했다(2026-09-23). 열려 있는 동안 오른쪽 위 줄을 덮고, 닫기는 창 안에 있다.
            // 가로 봇 장비창은 고리 옆에 목록을 두어 넓다 — 오른쪽 기둥에 안 들어가 가운데에, 화면 높이를 다 쓴다.
            if (panel == _botGear && !Main.Portrait)
            {
                holder.OffsetTop = 0;
                holder.Alignment = BoxContainer.AlignmentMode.Center;
                _botGear.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                continue;
            }

            if ((panel == _pack || panel == _tabMap) && !Main.Portrait)
            {
                holder.OffsetTop = 0;
                continue;
            }

            // 대화(기록) 창은 화면 가운데 아래에(사용자, 2026-09-26) — 가로는 오른쪽 기둥 대신 가운데에 제 폭만큼. 세로는 원래 폭 전체.
            if (panel == _chat && !Main.Portrait)
            {
                _chat.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                _chat.CustomMinimumSize = new Vector2(Mathf.Min(460, GetViewportRect().Size.X - (Main.Gutter * 4)), 0);
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
            holder.AnchorLeft = Main.Portrait || holder == _settingsHolder || holder == _botGearHolder || holder == _chatHolder ? 0 : column;
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

        // 자동 사냥이 스스로 멈추면(맵 이동·쓰러짐·회복 수단 없음) 한 줄로 알린다.
        _world.AutoHuntStopped += Notify;
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

        // 세로는 이름을 막대 위 한 줄로 — 옆에 두면 이름이 긴 만큼 판이 넓어져 같은 줄의 미니맵이 화면 밖으로 밀렸다(2026-09-26).
        // 가로도 같게(2026-09-26) — 월드맵 마름모가 맨 왼쪽에 서면서 640 가로에서 이름 옆에 막대를 두면 위 줄이 넘쳤다.
        VBoxContainer mine = new();
        mine.AddThemeConstantOverride("separation", 0);
        // 첫 줄: 이름과 그 옆 상태 아이콘 줄(버프·디버프) — 둘 다 없으면 줄째 접힌다.
        HBoxContainer headline = new() { MouseFilter = MouseFilterEnum.Ignore };
        headline.AddThemeConstantOverride("separation", Main.Gutter);
        _myStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _myStatus.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        headline.AddChild(_who);
        headline.AddChild(_myStatus);
        mine.AddChild(headline);
        mine.AddChild(BuildVitals());

        // 이름 줄은 이름이 오기 전에는 접는다 — 빈 줄이 판 위에 남는다. 글자는 조금 작게 — 가로 360 에서 위 줄이 한 줄 늘어난
        // 만큼 조작 줄을 밀어내지 않게(판 네 줄이 80 안에 들어야 한다).
        _who.Visible = false;
        _who.AddThemeFontSizeOverride("font_size", 12);
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

        // 고른 이가 없으면 판째로 숨긴다 — 빈 판이 바닥 한가운데를 가린다. 가로는 위 줄 가운데, 세로는 둘째 줄 왼쪽(첫 줄
        // 오른쪽은 미니맵 자리다).
        CenterContainer middle = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _targetPlate = Plated(picked);
        _targetPlate.Visible = false;
        middle.AddChild(_targetPlate);

        // 곳 이름은 미니맵 아래 구석에 적는다(MinimapView) — 가로 위 줄에 따로 두던 판은 뺐다(2026-09-26).
        _place = Aux(string.Empty);

        // 위 줄 단추는 셋만(2026-09-26): [인벤토리] · [월드맵] · [설정]. [종료]는 설정 → 계정 탭으로, [길]은 미니맵이 되었다.
        HBoxContainer actions = new() { MouseFilter = MouseFilterEnum.Ignore };
        actions.AddThemeConstantOverride("separation", Main.Gutter);

        Button pack = new()
        {
            Text = "인벤토리",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        Greybox.Plain(pack);
        pack.Pressed += () => Carrying(!_pack.Visible);
        actions.AddChild(pack);

        // 월드맵은 인벤토리·설정과 같은 보통 단추(2026-09-26 3차 — 2차의 마름모 단추는 요청을 잘못 읽은 것이었다. 맨 왼쪽으로
        // 가는 것은 미니맵이다). 누르면 카드형 월드맵.
        _map = new Button
        {
            Text = "월드맵",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        Greybox.Plain(_map);
        _map.Pressed += () => _ = _server?.OpenFieldAsync(System.Threading.CancellationToken.None);
        actions.AddChild(_map);
        actions.MoveChild(_map, 0);

        Button settings = new()
        {
            Text = "설정",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        Greybox.Plain(settings);
        settings.Pressed += () => SetWindow(GameWindow.Settings, !_settings.Visible);
        actions.AddChild(settings);

        if (Main.Portrait)
        {
            // 세로: 첫 줄 = 둥근 미니맵(맨 왼쪽) · 내 판, 둘째 줄 = 고른 이 · 월드맵 · 인벤토리 · 설정.
            row.AddChild(_minimap);
            row.MoveChild(_minimap, 0);

            HBoxContainer second = new() { MouseFilter = MouseFilterEnum.Ignore };
            second.AddThemeConstantOverride("separation", Main.Gutter);
            middle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            second.AddChild(middle);
            second.AddChild(actions);

            foreach (Button action in new Button[] { _map, pack, settings })
            {
                action.CustomMinimumSize = new Vector2(64, Main.TouchMinimum);
            }

            VBoxContainer top = new() { MouseFilter = MouseFilterEnum.Ignore };
            top.AddThemeConstantOverride("separation", Main.Gutter);
            top.AddChild(row);
            top.AddChild(second);

            return top;
        }

        // 가로: 미니맵(맨 왼쪽) · 내 판 · 고른 이(가운데) · 월드맵 · 인벤토리 · 설정, 한 줄.
        row.AddChild(_minimap);
        row.MoveChild(_minimap, 0);
        row.AddChild(middle);
        row.AddChild(actions);

        return row;
    }

    /// <summary>Exits and standing NPCs for every drawn map (<c>scripts/build-client-guide.py</c>). Empty when not shipped.</summary>
    private static MapGuide LoadGuide()
    {
        const string path = "res://assets/world/guide.txt";

        return Godot.FileAccess.FileExists(path) ? MapGuide.Read(Godot.FileAccess.GetFileAsString(path)) : MapGuide.Empty;
    }

    /// <summary>
    /// The small plate that says where we are being walked to, with a 멈춤 on it — under the top row, in the middle,
    /// where nothing else sits. Only while guiding and the map is closed; the map window says the same itself.
    /// </summary>
    private Control BuildGuideChip()
    {
        HBoxContainer inside = new();
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        _guideText = new Label { VerticalAlignment = VerticalAlignment.Center };
        _guideText.AddThemeColorOverride("font_color", Greybox.Text);

        Button stop = new() { Text = "멈춤", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        Greybox.Plain(stop);
        stop.Pressed += () => _world.StopGuiding();

        inside.AddChild(_guideText);
        inside.AddChild(stop);

        CenterContainer holder = new() { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        holder.AnchorLeft = 0;
        holder.AnchorRight = 1;
        holder.AddChild(Plated(inside));
        _topRow.Resized += () =>
        {
            holder.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + Main.Gutter;
            holder.OffsetBottom = holder.OffsetTop + Main.TouchMinimum + 12;
        };

        return holder;
    }

    /// <summary>자동 사냥을 켜고 끄고, 켰다·껐다 한 줄로 알린다 — 공격 단추를 0.5초 길게 눌러도(<see cref="AbilityBar.AutoHuntToggleRequested" />)
    /// <c>--auto-hunt</c> 로 스스로 눌러도 같은 문을 지난다.</summary>
    private void ToggleAutoHunt()
    {
        _world.SetAutoHunt(!_world.AutoHunting);
        Notify(_world.AutoHunting
            ? $"자동 사냥을 켰습니다 — 이 자리에서 {Main.AutoHuntSettings.Radius}칸 안."
            : "자동 사냥을 껐습니다.");
    }

    /// <summary>
    /// 공격 단추의 모양을 자동 사냥과 맞춘다 — 켜짐은 테두리 + "자동" 글자, 손이 잠시 조작 중이면 흐리게
    /// (<see cref="AbilityBar.ShowAutoHunt" />). <c>--auto-hunt</c> 면 자리를 잡은 뒤 한 번 스스로 켠다.
    /// </summary>
    private void KeepAutoHuntButton()
    {
        if (Main.AutoHuntOnStart && _autoHuntSettling >= 0 && _world.MapId > 0 && _server?.Vitals is not null
            && ++_autoHuntSettling == 120)
        {
            _autoHuntSettling = -1;
            ToggleAutoHunt();
            GD.Print("GREYBOX_AUTOHUNT 켬");
        }

        bool on = _world.AutoHunting;
        bool paused = _world.AutoHuntPaused;

        if (on != _autoHuntDrawn || paused != _autoHuntPausedDrawn)
        {
            _autoHuntDrawn = on;
            _autoHuntPausedDrawn = paused;
            _abilities.ShowAutoHunt(on, paused);
        }
    }

    /// <summary>Keeps the guide plate in step, and — hands-free only — opens the map, taps a place on it, and closes it.</summary>
    private void KeepGuiding(double delta)
    {
        string? going = _world.Guiding;
        _guideChip.Visible = going is not null && !_tabMap.Visible && _world.Route.Count > 0;

        if (_guideChip.Visible)
        {
            _guideText.Text = $"→ {(going!.Length > 0 ? going : "고른 자리")} · {_world.Route.Count}걸음";
        }

        if (!Main.OpeningTabMap)
        {
            return;
        }

        // 옆 단추와 같은 규칙 — 미니맵 자신의 눌림으로 연다. 월드가 자리를 잡고 이 맵의 벽을 읽은 뒤에.
        if (_tabMapOpenFor < 0 && (_world.MapId > 0 || _server is null) && _tabMapSettling++ == 90)
        {
            _minimap.EmitSignal(BaseButton.SignalName.Pressed);
            _tabMapOpenFor = 0;
        }

        if (_tabMapOpenFor < 0)
        {
            return;
        }

        _tabMapOpenFor += delta;

        if (Main.TabMapGo.Length > 0 && !_tabMapWent && _tabMapOpenFor >= 2 && _tabMap.PointOf(Main.TabMapGo) is { } spot)
        {
            _tabMapWent = true;
            _tabMap.TapAt(spot);
        }

        if (Main.TabMapZoom && _tabMapOpenFor >= 1 && !_tabMap.Zoomed)
        {
            _tabMap.Zoom.EmitSignal(BaseButton.SignalName.Pressed);
        }

        if (Main.TabMapCloseAfter >= 0 && _tabMap.Visible && _tabMapOpenFor >= Main.TabMapCloseAfter)
        {
            _tabMap.Close.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    /// <summary>
    /// Closes the network before asking Main for a deferred tree change. The exit callback below is a
    /// second safety net, so WorldClient and every owner beneath it make Dispose idempotent.
    /// </summary>
    /// <remarks>
    /// 원작처럼 먼저 나간다고 말하고(0x0B) 서버가 캐릭터를 뺐다는 답을 잠깐(1초까지) 기다린 뒤 닫는다 — 소켓만 끊고 떠나면
    /// 캐릭터가 사냥터에 남아 보였다(사용자, 2026-09-24).
    /// </remarks>
    private async void LogOut()
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        _settings.Exit.Disabled = true;
        _world.Frozen = true;

        if (_server is { } server)
        {
            await server.LogOutAsync(System.Threading.CancellationToken.None);
        }

        LoggedOut?.Invoke();
    }

    /// <summary>
    /// Closes the network and then the app. iOS does not let an app close itself (<see cref="ExitChoice" />), so
    /// there it logs out instead.
    /// </summary>
    private async void QuitGame()
    {
        if (!ExitChoice.CanQuit)
        {
            LogOut();
            return;
        }

        _world.Frozen = true;

        if (_server is { } server)
        {
            await server.LogOutAsync(System.Threading.CancellationToken.None);
        }

        GetTree().Quit();
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

        // 길 찾기 창이 열려 있으면 캐릭터를 창이 안 덮은 쪽 한가운데에 세운다 — 걸어가는 모습이 보이게.
        // 세로는 위 줄과 창 사이, 가로는 창 왼쪽.
        if (_tabMap.Visible && _tabMap.Size.Y > 0)
        {
            Rect2 covered = _tabMap.GetGlobalRect();

            if (Main.Portrait)
            {
                _world.FocusY = ((_topRow.GetGlobalRect().End.Y + covered.Position.Y) / 2) - _world.GetGlobalRect().Position.Y;
            }
            else
            {
                _world.FocusX = (covered.Position.X / 2) - _world.GetGlobalRect().Position.X;
            }
        }
        else
        {
            _world.FocusX = null;
        }

        // 서버가 어디라고 말하기 전에는 빈 판이 오른쪽 위에 남는다.
        if (_placePlate is not null)
        {
            _placePlate.Visible = _place.Text.Length > 0;
        }

        // The server names us in 0x33; nothing else on this screen knows who we are.
        // 배치 검사는 이름이 붙은 판을 잰다 — 이름 없는 판으로 재면 미니맵 자리가 넉넉해 보였다(실제 서버에서 넘쳤다, 2026-09-26).
        // 평소 서버 없는 화면에는 지어낸 이름을 적지 않는다(아래 설명 그대로).
        if ((_server?.Self?.Name ?? (LayoutCheck.Requested() ? LayoutCheck.PretendName : null)) is { Length: > 0 } called)
        {
            _who.Text = Mine.Level > 0 ? $"{called} Lv{Mine.Level}" : called;
            _who.Visible = true;
        }

        ShowVitals();

        // 내 상태 아이콘 줄 — 원작 상태 아이콘(0x3A)과 서버가 알리는 상태(0x5E 종류 3, 5.99 호르라마·에나르마 포함).
        _myStatus.Show(_server is { } me
            ? StatusBadges.Of(me.Ailments, me.StatusesOf(me.Serial))
            : LayoutCheck.PretendStatuses);

        ShowTarget();
        _abilities.Show(
            _server?.Skills ?? LayoutCheck.PretendSkills,
            _server?.Spells ?? LayoutCheck.PretendSpells,
            _server?.Self?.Name ?? string.Empty);

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
        KeepGuiding(delta);
        KeepAutoHuntButton();
        _settings.ShowCompanion(_server?.Companion is not null);

        if (Main.CompanionOnStart && _companionSettling >= 0 && _world.MapId > 0 && _server?.Vitals is not null
            && ++_companionSettling == 120)
        {
            _companionSettling = -1;
            _settings.Companion.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print("GREYBOX_COMPANION 부름");
        }
        RehearseAHold(delta);
        RehearseASkill(delta);

        // 손 없이 확인할 때만 — 설정을 열고, 몇 프레임 뒤(자리를 잡은 뒤) 제목 줄의 [로그아웃] 을 실제로 눌러(EmitSignal) 고르는 판을 띄운다.
        if (Main.OpeningExit)
        {
            if (_exitSettling == 90)
            {
                SetWindow(GameWindow.Settings, true);
            }
            else if (_exitSettling == 96)
            {
                _settings.Exit.EmitSignal(BaseButton.SignalName.Pressed);
            }

            _exitSettling++;
        }

        RehearseMinimap();
        RehearsePackPick();

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

                // 서버 없이는 아무도 창을 보내 주지 않는다 — 사진·배치 검사용으로 서버가 보낼 여섯 곳을 그대로 띄운다.
                if (_server is null)
                {
                    _field.Show(LayoutCheck.PretendField, Main.MapTab == "사냥터" ? "우드랜드1-1" : "노비스마을");
                    SetWindow(GameWindow.WorldMap, true);
                }
            }

            if (_field.Visible && (_mapOpenSeconds += delta) >= MapCloseAfterSeconds)
            {
                // --map-go 는 닫지 않고 그 줄을 눌러 그리로 간다.
                if (Main.MapGo.Length > 0)
                {
                    _field.RowNamed(Main.MapGo)?.EmitSignal(BaseButton.SignalName.Pressed);
                }
                else
                {
                    _field.Close.EmitSignal(BaseButton.SignalName.Pressed);
                }
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
            // 서버가 띄운 창도 창 하나 규칙을 지난다 — 열려 있던 창은 닫힌다.
            _field.Show(field, _world.PlaceName);
            SetWindow(GameWindow.WorldMap, true);
            _closedAtFieldShown = null;
        }
        else if (_server is not null && _server.Field is null)
        {
            // 보내기가 실패해 서버가 영영 맵을 안 바꾸면(고르기도, 닫기도) 창이 다시 안 뜬다 — 두 번
            // 이동하거나 닫았는데 도로 열리는 것보다 안 뜨는 편이 낫다고 보고, 그때는 사람이 다시 접속한다.
            if (_field.Visible)
            {
                SetWindow(GameWindow.WorldMap, false);
            }

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
        KeepBot();

        if (_server is not { } server)
        {
            // --party-preview: 서버 없이 파티원 다섯(+ 나)을 지어 파티원 칸을 그려 본다 — 사진·배치 검사용.
            if (Main.PartyPreview)
            {
                string[] names = ["나", "가나다라마바", "검객", "궁수아이디", "도사", "치유사"];
                _party.Show(new PartyRoster([.. names.Select((name, at) => new PartyMember(name, at == 1))]), "나", name =>
                    new MemberLook(90 - (name.Length * 9), 70 - (name.Length * 7),
                        StatusBadges.OfIcons(name.Length % 2 == 0 ? [11, 52] : [82])));
            }

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

        // 서버가 1초마다 보내는 그룹원 체력·마력 %·상태(0x5E 종류 6, 이름으로 짝짓는다 — 멀리 있어도). 아직 없으면 보이는 이의
        // 체력바 %(0x13)만.
        _party.Show(roster, self, name => server.MemberStatus(name) is { } told
            ? new MemberLook(told.HealthPercent, told.ManaPercent, StatusBadges.OfIcons(told.Icons))
            : new MemberLook(server.Others.FirstOrDefault(other => other.Name == name) is { } seen ? server.Health(seen.Serial) : null, null, []));

        Character? picked = server.Others.FirstOrDefault(other => other.Serial == _world.Target);
        bool leading = !roster.Grouped || roster.Members.Any(member => member.Leader && member.Name == self);

        _party.CanInvite(picked is { Name.Length: > 0 } && leading &&
                         !roster.Members.Any(member => member.Name == picked.Name));

        RehearseParty(delta);
    }

    /// <summary>
    /// 봇 칸과 봇 장비창을 서버 소식(0x5E)에 맞춘다. 봇이 없으면 둘 다 숨긴다. <c>--bot-preview</c> 는 서버 없이 지어낸 봇으로
    /// 그려 본다(사진·배치 검사용), <c>--bot-gear</c> 는 창까지 연다.
    /// </summary>
    private void KeepBot()
    {
        CompanionTie? bot = _server?.Companion;
        CompanionKit? kit = _server?.CompanionKit;
        (int? health, int? mana) = BotKit.Bars(_server?.CompanionLife);
        IReadOnlyList<InventoryItem> pack = _server?.Pack ?? [];

        if (bot is null && Main.BotPreview)
        {
            bot = new CompanionTie(1, "동료사제");
            (health, mana) = (72, 45);
            kit = new CompanionKit(
                [new WornItem(1, 33318, "홀리파나", "홀리파나", 3000, 3000), new WornItem(2, 32873, "레더로브", "레더로브", 2000, 2000)],
                [new CarriedItem("쿠룸", 32813, 4), new CarriedItem("마라디움", 32815, 2)]);
            pack =
            [
                new InventoryItem(1, 32900, 0, "홀리머큐리아", 0, 3000, 3000),
                new InventoryItem(2, 32878, 0, "맨틀", 0, 2500, 2500),
                new InventoryItem(3, 32813, 0, "쿠룸", 10, 0, 0),
                new InventoryItem(4, 32815, 0, "마라디움", 6, 0, 0),
            ];

            if (Main.BotGearOpen && !_botGearRehearsed)
            {
                _botGearRehearsed = true;
                SetWindow(GameWindow.BotGear, true);
                _botGear.Choose(1);
            }
        }

        IReadOnlyList<StatusBadge> botStatus = bot is null ? []
            : Main.BotPreview && _server is null ? [new StatusBadge(11, 100, 6, false), new StatusBadge(52, 40, 4, false), new StatusBadge(82, 8, 1, true)]
            : StatusBadges.Of([], _server?.StatusesOf(bot.Serial));
        _party.ShowBot(bot?.Name, health, mana, botStatus);

        if (bot is null)
        {
            if (_botGear.Visible)
            {
                SetWindow(GameWindow.BotGear, false);
            }

            return;
        }

        if (_botGear.Visible)
        {
            Character? doll = _server?.Others.FirstOrDefault(other => other.Serial == bot.Serial);
            _botGear.Show(bot.Name, kit, pack, doll);
        }
    }

    private bool _botGearRehearsed;

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
        float top = _topRow.GetGlobalRect().End.Y - _over.GetGlobalRect().Position.Y + Main.Gutter;
        Control bot = _party.BotSlot;

        // 봇 칸: 화면 왼쪽 끝에 붙인다 — HUD 여백(틈 8)만큼 왼쪽으로 뺀다. 가로 아이폰은 노치 쪽 안전선까지만(SafeInsets 에는
        // 틈 8 이 들어 있어 뺀다).
        bot.OffsetLeft = Main.SafeInsets.Left - Main.Gutter - _over.GetGlobalRect().Position.X;
        bot.OffsetRight = bot.OffsetLeft + PartyColumn.BotWide;
        bot.OffsetTop = top;
        bot.OffsetBottom = top + bot.GetCombinedMinimumSize().Y;

        // 파티원 칸들: 세로는 봇 칸 아래로 쌓고, 가로는 봇 칸 오른쪽으로 늘어놓는다(왼쪽 아래는 방향판) — 화면 폭의 반까지만.
        Control members = _party.Members;
        float edge = bot.OffsetLeft;

        if (Main.Portrait)
        {
            members.OffsetLeft = edge;
            members.OffsetTop = bot.Visible ? bot.OffsetBottom + 4 : top;

            // 한 줄로 쌓아 방향판에 닿으면 두 줄로 — 낮은 세로 화면(360x640)에 다섯이면 그렇다.
            float room = _pad.GetGlobalRect().Position.Y - _over.GetGlobalRect().Position.Y - members.OffsetTop - Main.Gutter;
            float[] tall = [.. members.GetChildren().OfType<Control>().Select(child => child.GetCombinedMinimumSize().Y)];
            float stacked = tall.Sum(one => one + 4);
            int columns = stacked > room ? 2 : 1;
            members.OffsetRight = edge + (columns * PartyColumn.MemberWide) + ((columns - 1) * 4);

            // 높이는 직접 센다 — 흐르는 칸은 폭이 바뀐 다음 프레임에야 제 높이를 다시 재서, 그 한 프레임 동안 한 줄 높이로 남았다.
            members.OffsetBottom = members.OffsetTop + (columns == 1
                ? stacked
                : tall.Chunk(2).Sum(pair => pair.Max() + 4));
        }
        else
        {
            members.OffsetLeft = bot.Visible ? bot.OffsetRight + 4 : edge;
            members.OffsetTop = top;
            members.OffsetRight = edge + (GetViewportRect().Size.X * 0.72f);
        }

        if (!Main.Portrait)
        {
            members.OffsetBottom = members.OffsetTop + members.GetCombinedMinimumSize().Y;
        }
        float below = members.Visible ? members.OffsetBottom : bot.Visible ? bot.OffsetBottom : top - Main.Gutter;

        // 파티 기둥(초대 단추·묻기): 세로는 그 아래, 가로는 방향판 오른쪽 옆 — 파티원 칸 줄 아래.
        _party.OffsetLeft = Main.Portrait
            ? 0
            : _pad.GetGlobalRect().End.X - _over.GetGlobalRect().Position.X + Main.Gutter;
        _party.OffsetRight = _party.OffsetLeft + PartyColumn.Wide;
        _party.OffsetTop = Main.Portrait ? below + Main.Gutter : Mathf.Max(top, members.Visible ? members.OffsetBottom + Main.Gutter : top);
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
    private int _minimapTold;
    private int _minimapZoomWait;
    private int _minimapPressed;
    private int _packPickWait;

    /// <summary><c>--minimap</c>: every two seconds, where the minimap stands and what it shows — no thumb needed.</summary>
    private void RehearseMinimap()
    {
        // --minimap-zoom: 60 프레임 뒤부터 10 프레임마다 [+]/[−] 를 한 번씩.
        if (Main.MinimapZoom != 0 && _minimapPressed < Math.Abs(Main.MinimapZoom) && ++_minimapZoomWait >= 60 && _minimapZoomWait % 10 == 0)
        {
            _minimapPressed++;
            (Main.MinimapZoom > 0 ? _minimap.ZoomIn : _minimap.ZoomOut).EmitSignal(BaseButton.SignalName.Pressed);
        }

        if (Main.CheckingMinimap && (_world.MapId > 0 || _server is null) && ++_minimapTold % 120 == 30)
        {
            GD.Print($"GREYBOX_MINIMAP {_minimap.GetGlobalRect()} {_minimap.Describe()}");
        }
    }

    /// <summary><c>--pack-pick N</c>: once the pack is open and filled, taps its N-th picture so the action row shows.</summary>
    private void RehearsePackPick()
    {
        if (Main.PackPick > 0 && _pack.Visible && _packPickWait >= 0 && ++_packPickWait == 45)
        {
            _packPickWait = -1;
            GD.Print(_pack.PickNth(Main.PackPick) ? $"GREYBOX_PACK_PICK {Main.PackPick}" : "GREYBOX_PACK_PICK 없음");
        }
    }

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
            (3, "금전 120전을 주웠습니다."),
            (2, "경험치가 1164 올랐습니다"),
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
            _chat.Show(_history);
        }

        SetWindow(GameWindow.Chat, open);
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
                _world.StopGuiding();
                _world.SteeredByHand();
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
    /// it, and a thumb aimed at the list must not walk the character. Whatever window was open is put away first
    /// (<see cref="SetWindow" />) — an NPC's window the way its own close button does.
    /// </summary>
    private void Carrying(bool open)
    {
        SetWindow(GameWindow.Pack, open);

        if (open)
        {
            _pack.Show(_server?.Pack ?? LayoutCheck.PretendPack, _server?.Worn ?? LayoutCheck.PretendWorn, _server?.Self ?? LayoutCheck.PretendSelf, Mine.Gold);
        }
    }

    /// <summary>
    /// Opens the window an NPC sent, or shuts it when the server did. While it is open the world takes no taps or steps,
    /// as with the pack, and whatever window was open is put away (<see cref="SetWindow" />) so two never lie on top of each other.
    /// </summary>
    private void Talk(Dialogue? talk)
    {
        // 우리가 닫기를 보내면 서버도 닫기(0x30)로 답한다. 그사이 소지품을 열었으면 그 화면을 건드리지 않는다.
        if (talk is null && !_talk.Visible)
        {
            return;
        }

        SetWindow(GameWindow.Talk, talk is not null);

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
    /// Health over mana, each a filling gauge with its exact numbers beside it — the wireframes always asked for
    /// both together (docs/mobile-test-v1-wireframes.md: "HP는 막대와 숫자를 함께 표시"), and each keeps its own
    /// theme colour (health's orange, mana's blue — data/ui-vault/색) so the two are told apart without reading.
    /// </summary>
    private Control BuildVitals()
    {
        VBoxContainer vitals = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        vitals.AddThemeConstantOverride("separation", 0);

        // 경험치는 막대 없이 숫자만 둔다 — 서버는 다음 레벨까지 얼마 남았는지만 말하고 그 레벨에 얼마가 드는지는
        // 말하지 않는다. 막대를 그리려면 길이를 지어내야 한다.
        _experience = Aux(string.Empty);

        vitals.AddChild(Gauge("체력", Greybox.Health, out _healthBar, out _healthText));
        vitals.AddChild(Gauge("마력", Greybox.Mana, out _manaBar, out _manaText));
        vitals.AddChild(_experience);

        return vitals;
    }

    /// <summary>
    /// One vital: a name, a bar that fills in its theme colour, and the exact numbers beside it, small. The
    /// over-the-head bar (HealthBar) still carries how a fight is going; this one is the place the numbers are
    /// always exact, so the bar and the numbers are read together rather than the same thing drawn twice.
    /// </summary>
    private static Control Gauge(string name, Color paint, out ProgressBar bar, out Label text)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);

        // 구슬만 두었더니 무엇을 뜻하는지 알 수 없다는 말을 들었다(사용자, 2026-09-18). 이름을 되살린다 —
        // 색은 거드는 것이지 뜻을 나르는 것이 아니다.
        Label named = Aux(name);

        bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? 48 : 72, GaugeHeight),
            MaxValue = 1,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        bar.AddThemeStyleboxOverride("background", Greybox.Surface());
        bar.AddThemeStyleboxOverride("fill", Greybox.Fill(paint));

        text = Aux(string.Empty);
        text.AddThemeFontSizeOverride("font_size", GaugeFontSize);

        row.AddChild(named);
        row.AddChild(bar);
        row.AddChild(text);

        return row;
    }

    /// <summary>Puts the newest health and mana on the gauges, only when they have changed.</summary>
    private void ShowVitals()
    {
        Vitals mine = Mine;

        if (mine == _shownVitals)
        {
            return;
        }

        _shownVitals = mine;
        Fill(_healthBar, _healthText, mine.Health, mine.MaximumHealth);
        Fill(_manaBar, _manaText, mine.Mana, mine.MaximumMana);

        string points = mine.Unspent > 0 ? $" · 점수 {mine.Unspent}" : string.Empty;

        _experience.Text = mine.Level <= 0
            ? string.Empty
            : (mine.ExperienceToGo <= 0 ? "EXP 다 올랐습니다" : $"EXP 다음까지 {mine.ExperienceToGo:N0}") + points;
    }

    /// <summary>
    /// Fills one vital's bar and writes its number. The number turns colour as it falls — but the numbers
    /// themselves are the reading, so somebody who cannot tell the colours apart loses nothing.
    /// </summary>
    private static void Fill(ProgressBar bar, Label text, int left, int most)
    {
        bar.MaxValue = most > 0 ? most : 1;
        bar.Value = most > 0 ? Mathf.Clamp(left, 0, most) : 0;

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
        _abilities.Standing = () => (_server?.Path, _server?.Vitals?.Level ?? 0);
        _abilities.SkillUsed += slot =>
        {
            _world.FoughtByHand();
            _world.UseSkill(slot);
        };
        _world.BarSkills = _abilities.PlacedSkills;
        _abilities.SpellUsed += slot => UseSpell(slot);
        _abilities.LoadSlots = Main.LoadAbilitySlots;
        _abilities.SaveSlots = Main.SaveAbilitySlots;

        // One tap is one blow. It does not chase and it does not repeat — the server decides whether it
        // landed, and says so in words we show below rather than guessing at damage here.
        _abilities.AttackReleased += () =>
        {
            // 사람이 직접 치면 자동 사냥은 3초 쉰다 — 끄지 않는다. 손을 떼면 다시 돈다.
            _world.FoughtByHand();
            _world.Strike();
        };

        // 0.5초 길게 누르면 자동 사냥을 켜고 끈다(위 줄의 [자동] 단추는 없앴다 — 사용자 요청, 2026-09-26).
        _abilities.AutoHuntToggleRequested += ToggleAutoHunt;

        // 자동 포션은 창 안에 숨기지 않는다 — 싸우는 중에 한 번에 닿아야 한다(사용자, 2026-09-23). 위 줄에 있던 것을
        // 기술 부채꼴 맨 위, 가장 높은 기술 칸 위로 옮겼다 — 기술 칸(48)보다 조금 작게(사용자, 2026-09-23 "기술창 제일
        // 상단쪽에 … 기술창 보다 조금 작게"). 마실 포션의 그림에 줄을 작게 적는다. 누르면 켜고 끄기, 길게 누르면 다른
        // 포션을 고른다. 줄은 설정 창에서.
        _abilities.Hold(new PotionChip(AutoPotion.Healing,
            () => Main.HealthPotion, rule => Main.SetPotions(rule, Main.ManaPotion), () => _server?.Pack ?? LayoutCheck.PretendPack), 0);
        _abilities.Hold(new PotionChip(AutoPotion.Restoring,
            () => Main.ManaPotion, rule => Main.SetPotions(Main.HealthPotion, rule), () => _server?.Pack ?? LayoutCheck.PretendPack), 1);
        _abilities.HoldComa(new ComaButton(() => _server, Notify));

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

        if (spell.TargetType is SpellTargetType.Prompt or SpellTargetType.FourDigit
            or SpellTargetType.ThreeDigit or SpellTargetType.TwoDigit or SpellTargetType.OneDigit)
        {
            Notify($"{spell.Name}: 입력 창이 필요한 마법입니다.");
            return;
        }

        // 대상 마법인데 고른 이가 없으면 나에게(SpellAim) — 전에는 "마법 대상을 먼저 누르세요" 로 거절해 호르라마·쿠로를 제게 못 걸었다.
        _world.UseSpell(spell.Slot, SpellAim.Target(spell.TargetType, _world.Target, _server?.Serial ?? 0));
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
            button.ButtonDown += () =>
            {
                // 방향판을 누르면 길 안내는 멈춘다 — 손이 이긴다. 자동 사냥은 잠시 쉬고, 선 자리가 새 중심이 된다.
                _world.StopGuiding();
                _world.SteeredByHand();
                _world.Walk(where);
            };
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
