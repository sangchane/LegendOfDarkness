using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// Main game greybox, built to section 5 of the wireframes. The world fills the whole screen while every
/// label and control stays inside the safe area, and the two thumb clusters sit where a thumb can reach.
/// </summary>
public partial class GameScreen : Control
{
    private const int AuxFontSize = 14;
    private Label _wealth = null!;
    private Label _level = null!;

    // 위 판·위 메뉴 — 롤(LoL) 클라이언트 색(사용자 2026-10-02, docs/hud-renewal-references.md).
    private static readonly Color LolBack = new(0.004f, 0.039f, 0.075f, 0.88f); // #010A13
    private static readonly Color LolGold = new("#C8AA6E");
    private static readonly Color LolGoldDark = new("#785A28");
    private static readonly Color LolCoin = new("#C89B3C");
    private static readonly Color LolText = new("#F0E6D2");
    private static readonly Color LolMuted = new("#A09B8C");
    private static readonly Color HudHealth = new("#D6463C");
    private static readonly Color HudMana = new("#2C7BD6");
    private bool _shopPreviewed;

    /// <summary>체력·마력 막대의 높이 — 숫자를 막대 안에 얹으므로(2026-09-27) 글자 한 줄이 들 만큼.</summary>
    private const int GaugeHeight = 14;

    /// <summary>막대 안 숫자의 글자 크기. 작게 두어(사용자 지시) 막대를 더한 만큼 판이 넓어지지 않게 한다.</summary>
    private const int GaugeFontSize = 11;

    /// <summary>
    /// 막대 폭 — 전의 막대(세로 48 · 가로 72)와 옆 숫자("99999 / 99999" 약 75)를 합친 것보다 좁게, "99999/99999" 가 안에 들게
    /// (사용자, 2026-09-27: "숫자를 게이지 위에 겹쳐 공간을 더 활용").
    /// </summary>
    private static int GaugeWidth => Main.Portrait ? 96 : 116;

    /// <summary>
    /// How tall the ticker's row is in portrait: two one-row lines, and the 대화 button beside them. It does not grow —
    /// the character stands in the middle of what is left above it (FocusY).
    /// </summary>
    private const int LogHeight = Main.TouchMinimum;

    private WorldView _world = null!;
    private Label _who = null!;
    private Label _place = null!;
    private Label _target = null!;
    private Label _targetPercent = null!;
    private PackPanel _pack = null!;
    private GearPanel _gearPanel = null!;
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
    private readonly UsersPanel _users = new();
    private bool _usersAsking;
    private double _usersAskIn;
    private int _usersAsked;
    private Control? _botGearHolder;
    private Control? _gearHolder;
    private VBoxContainer? _packHolder;
    private Control? _talkHolder;

    // 월드맵 창을 언제 다시 띄우나(고른 뒤·닫은 뒤) — 규칙은 알맹이에.
    private readonly WorldMapGate _mapGate = new();

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
    private readonly DirectionHold _hold = new();

    /// <summary>How see-through the pad gets while walking, how long it waits after the last step, how fast it fades.</summary>
    private const float WalkingAlpha = 0.35f;
    private const double SettleSeconds = 0.25;
    private const double FadeSeconds = 0.12;

    // 내 체력·마력. 서버가 준 값이 바뀔 때만 다시 쓴다.
    private ProgressBar _healthBar = null!;
    private ProgressBar _manaBar = null!;
    private Label _healthText = null!;
    private Label _manaText = null!;
    private ProgressBar _experienceBar = null!;

    private Label _experienceText = null!;
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

    // 설정 창 제목 줄의 [로그아웃] 이 여는 작은 판 — 로그아웃 · 게임 종료 · 취소.
    private readonly ExitChoice _exit = new();

    // --exit-menu 로 설정 창을 열고 제목 줄의 [로그아웃] 을 누르기까지 센 프레임.
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
        [("위 줄", _topRow), ("미니맵", _minimap), ("조작 줄", _controlRow), ("방향판", _pad), ("목걸이 줄", _necklaces), ("파티원", _party.Members), ("나가기", _party.Leave), ("인벤토리", _pack), ("장비", _gearPanel), ("월드", _world)];

    /// <summary>Opens the gear window, or the pack. Only a layout check asks — a thumb presses the top row's buttons.</summary>
    public void ShowGear(bool gear)
    {
        if (gear)
        {
            Dressing(true);
        }
        else
        {
            Carrying(true);
        }
    }

    public override void _Ready()
    {
        // 앱이 뒤로 가도 자동 사냥이 이어지게 — 게임 화면이 떠 있는 동안만(KeepAlive).
        AddChild(new KeepAlive());

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
        _world.Exits = _guide;
        _minimap = new MinimapView(_world, _server, _guide);
        _minimap.Pressed += () => SetWindow(GameWindow.TabMap, !_tabMap.Visible);

        _topRow = BuildTopRow();
        _pack = new PackPanel();
        _pack.Close.Pressed += () => Carrying(false);
        _pack.Used += slot => Main.Fire(_server?.UseAsync(slot, System.Threading.CancellationToken.None));
        _pack.Dropped += (slot, count) => _ = Throw(slot, count);
        _pack.Tidy.Pressed += () => _ = Straighten();

        _gearPanel = new GearPanel();
        _gearPanel.TakenOff += place => Main.Fire(_server?.TakeOffAsync(place, System.Threading.CancellationToken.None));
        _gearPanel.Close.Pressed += () => Dressing(false);
        _gearPanel.GroupToggled += () => _ = ToggleGroup();
        _gearPanel.GroupAsked += name => Main.Fire(_server?.AskToGroupAsync(name, System.Threading.CancellationToken.None));

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
                Main.Fire(_server?.AcceptGroupAsync(name, System.Threading.CancellationToken.None));
            }
            else
            {
                // 원작에는 "싫다"는 말이 없다 — 답하지 않는 것이 거절이다. 청한 쪽에는 아무것도 가지 않는다.
                Route(new Notice(MessageChannel.Party, MessagePlace.LogOnly, $"{name}님의 파티 초대를 거절했습니다.", string.Empty));
            }
        };
        _party.Left += () => Main.Fire(_server?.LeaveGroupAsync(System.Threading.CancellationToken.None));

        _field = new FieldPanel(_guide);
        _field.Chosen += area =>
        {
            _mapGate.Chose(area);
            SetWindow(GameWindow.WorldMap, false);
            Main.Fire(_server?.ChooseFieldAsync(area, System.Threading.CancellationToken.None));
        };
        _field.Close.Pressed += () =>
        {
            CancelField();
            _windows.Shut(GameWindow.WorldMap);
            _world.Frozen = _windows.Freezing;
        };

        _settings = new SettingsPanel();
        _settings.Close.Pressed += () => SetWindow(GameWindow.Settings, false);
        _users.Close.Pressed += () => SetWindow(GameWindow.Users, false);

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
            ? CallCompanion()
            : _server.DismissCompanionAsync(System.Threading.CancellationToken.None);
        _settings.BotMagicChanged += () => Main.Fire(SendBotMagic());

        // 봇 칸을 누르면 봇 장비창. 주기·벗기기는 우리 확장 0xF1 2·3, 결과는 서버 알림과 봇 장비 안내(0x5E 종류 5).
        _party.BotOpened += () => SetWindow(GameWindow.BotGear, !_botGear.Visible);
        _botGear.Close.Pressed += () => SetWindow(GameWindow.BotGear, false);
        _botGear.Given += (slot, count) => Main.Fire(_server?.GiveToCompanionAsync(slot, count, System.Threading.CancellationToken.None));
        _botGear.TakenOff += place => Main.Fire(_server?.TakeOffCompanionAsync(place, System.Threading.CancellationToken.None));

        _talk = new TalkPanel();
        _talk.Close.Pressed += ShutTalk;
        _talk.WornNow = () => _server?.Worn ?? LayoutCheck.PretendWorn;
        _talk.Traded += (merchant, selling, lines) => _ = Trade(merchant, selling, lines);
        _talk.MenuRequested += merchant => Main.Fire(_server?.ShopMenuAsync(merchant, System.Threading.CancellationToken.None));
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
        AddChild(_loadingMap);

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
        if (Main.ActivityHost is { } host) await host.FinishActivity(false);

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
        if (Main.ActivityHost is { } host) await host.FinishActivity(true);

        if (_server is { } server)
        {
            await server.LogOutAsync(System.Threading.CancellationToken.None);
        }

        GetTree().Quit();
    }

    public override void _ExitTree() => _server?.Dispose();

    /// <summary>Keeps the place name and whoever is picked out in step with the world below.</summary>
    public override void _Process(double delta)
    {
        ShowMapLoading(delta);

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
        if ((_server?.Self?.Name ?? (LayoutCheck.PretendSelf is not null ? LayoutCheck.PretendName : null)) is { Length: > 0 } called)
        {
            _who.Text = called;
            _who.TooltipText = called;
            _wealth.Text = GoldFormat.Short(Mine.Gold);
            _level.Text = $"{Mine.Level}";
            _who.Visible = true;
        }

        if (!_shopPreviewed && _server is null && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--shop-preview") >= 0)
        {
            _shopPreviewed = true;
            bool selling = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--shop-sell") >= 0;
            Dialogue preview = new(1, "델란", "필요한 수량을 고른 뒤 아래에서 거래를 마치세요.")
            {
                Kind = selling ? DialogueKind.PackSlots : DialogueKind.Goods,
                Step = selling ? (ushort)0x0500 : (ushort)4,
                Slots = [1, 2, 3],
                // 수치는 [정보] 사진용 — 레더튜닉은 갑옷 자리(2)라 --stuff 의 시험용 갑옷과 견준다.
                Goods = [new(32813, 0, 150, "쿠룸", Stats: new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 150, 0, HealthRestore: 250)),
                    new(32882, 0, 950, "레더튜닉", "Warrior", 1, 1, new(-6, 0, 0, 2, 0, 0, 0, 0, 0, 30, 0, 0, 0, 11, 1, 0, 5, 0, 0, 950, 2)),
                    new(999999, 0, 1200, "그림 없는 도복", "Monk", 2, 1), new(32813, 0, 500, "마라디움")]
            };
            SetWindow(GameWindow.Talk, true);
            _talk.Show(preview, [new(1, 32813, 0, "쿠룸", 12, 0, 0), new(2, 32882, 0, "레더튜닉", 1, 30, 100)], Mine.Gold);
            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--shop-check") >= 0) _talk.CheckShop();
            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--shop-info") >= 0) _talk.PressInfo("레더튜닉");
        }

        ShowVitals();

        // 내 상태 아이콘 줄 — 원작 상태 아이콘(0x3A)과 서버가 알리는 상태(0x5E 종류 3, 5.99 호르라마·에나르마 포함).
        _myStatus.Show(_server is { } me
            ? StatusBadges.Of(me.Ailments, me.StatusesOf(me.Serial))
            : LayoutCheck.PretendStatuses);

        ShowTarget();
        // 직업 표(class-kit.txt)에 있는 것만 막대·배치 목록·자동 사냥에 — 무도가는 열 가지(사용자 2026-10-04).
        int? path = _server?.Path;
        _abilities.Show(
            [.. (_server?.Skills ?? LayoutCheck.PretendSkills).Where(one => Main.Kit.Shows(path, false, one.Name))],
            [.. (_server?.Spells ?? LayoutCheck.PretendSpells).Where(one => Main.Kit.Shows(path, true, one.Name))],
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
                    Main.Fire(_server?.AskProfileAsync(System.Threading.CancellationToken.None));
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
        LogUnread();
        KeepWalking(delta);
        KeepGuiding(delta);
        KeepAutoHuntButton();
        _settings.ShowCompanion(_server?.Companion is not null);
        _settings.ShowBotLevel(_server?.Vitals?.Level ?? 0);

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

        if (Main.OpeningPack && !_pack.Visible && !_gearPanel.Visible && _settling++ == settle)
        {
            ShowGear(Main.OnGear);
        }

        // 손 없이 확인할 때만. 같은 규칙 — 옆 단추(인벤토리)가 쓰는 것을 그대로 쓴다: 눌러 보는 것은
        // 잇는 서버 말이 아니라 단추 자신의 눌림(EmitSignal) — 배선까지 확인된다. 뜬 것을 잠시 두었다가
        // 닫기까지 눌러, 열린 화면과 닫아 조작이 돌아온 화면을 --shot-after 만 달리해 --map 하나로 잡는다.
        if (Main.OpeningMap)
        {
            if (_server?.Field is null && !_mapGate.Closing && _mapSettling++ == settle)
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
        _map.Disabled = _mapGate.Closing || _field.Visible;

        // 월드맵은 서버가 띄우는 것이지 사람이 여는 것이 아니다. 온 것을 그대로 보여 준다(언제 다시 띄우나는 WorldMapGate).
        if (_server?.Field is { } field && _mapGate.ShouldShow(_server.FieldShown, _field.Visible))
        {
            // 서버가 띄운 창도 창 하나 규칙을 지난다 — 열려 있던 창은 닫힌다.
            _field.Show(field, _world.PlaceName);
            SetWindow(GameWindow.WorldMap, true);
            _mapGate.Shown();
        }
        else if (_server is not null && _server.Field is null)
        {
            if (_field.Visible)
            {
                SetWindow(GameWindow.WorldMap, false);
            }

            _mapGate.Withdrawn();
        }

        // 창이 열려 있는 동안은 새 줄과 탭을 따라가고, 글자를 치는 동안 화면 키보드에 가리지 않게 창을 들어 올린다
        // (시안 2.1절 — 로그인 화면과 같은 방식).
        if (_chat.Visible)
        {
            _chat.Show(_history);
            _chatHolder.OffsetBottom = -Lifted();
        }

        // [접속자] 를 눌러 서버가 접속자 목록(0x36)을 보내 왔다. 서버는 새로고침(0x38) 뒤 0.3초 안의 0x18 을 말없이 버리므로
        // 창이 열린 채 답이 없으면 1초마다, 열 번까지 다시 묻는다.
        if (_server?.TakeUsers() is { } users)
        {
            _users.Show(users);
            _usersAsking = false;
        }
        else if (_usersAsking && _users.Visible && (_usersAskIn -= delta) <= 0)
        {
            AskUsers();
        }

        // 사람을 눌러 서버가 그 사람 장비창(0x34)을 보내 왔다.
        if (_server?.TakeSeen() is { } seen)
        {
            Dressing(true, seen);
        }

        if (_gearPanel.Visible && !_gearPanel.ShowingOther)
        {
            _gearPanel.Show(
                _server?.Worn ?? LayoutCheck.PretendWorn,
                Mine,
                _server is null ? 5 : _server.Path,
                _server?.Self?.Name ?? (LayoutCheck.PretendSelf is not null ? LayoutCheck.PretendName : string.Empty),
                _server?.GroupOpen ?? false,
                GearRoom());
        }

        // 내 장비창과 소지품을 같이 열면 둘을 붙인다(사용자 2026-10-01: 거리가 멀다). 세로는 소지품이 장비 그림 바로 아래에서
        // 시작하고, 가로는 장비 그림이 소지품 기둥 바로 왼쪽에 선다.
        bool together = _gearPanel.Visible && _pack.Visible;
        _pack.UnderGear(together);

        if (Main.Portrait && _packHolder is not null && _gearHolder is not null)
        {
            _packHolder.OffsetTop = together ? _gearHolder.OffsetTop + _gearPanel.Size.Y + Main.Gutter : _gearHolder.OffsetTop;
            _packHolder.Alignment = together ? BoxContainer.AlignmentMode.Begin : BoxContainer.AlignmentMode.End;
        }
        else if (!Main.Portrait && _gearHolder is not null)
        {
            _gearHolder.OffsetRight = together ? _pack.GlobalPosition.X - GetViewportRect().Size.X - Main.Gutter : 0;
        }

        if (_pack.Visible)
        {
            _pack.Worn = _server?.Worn ?? LayoutCheck.PretendWorn;
            _pack.Show(_server?.Pack ?? LayoutCheck.PretendPack, Mine.Gold);

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
                ShowGear(true);
            }
        }
    }

    /// <summary>
    /// Whoever is picked out, and how hurt they are. The bar is never alone — the number is beside it,
    /// because health must not be readable by colour or length alone.
    /// </summary>
    private void ShowTarget()
    {
        int? left = _world.Target == 0 ? null : _server?.Health(_world.Target);

        // 퍼센트·바는 고른 이가 있으면 늘 자리를 차지한다 — 체력을 알 때만 붙여 판이 넓어졌다 좁아졌다 했다(사용자 2026-10-05).
        _target.Text = _world.TargetName;
        _targetPercent.Text = left is { } percent ? $"{percent}%" : "–";
        _targetHealth.Value = left ?? 100;
        _targetHealth.Modulate = left is null ? new Color(1, 1, 1, 0.3f) : Colors.White;
        // 괴물 머리 위에 체력바가 있으니 위 판은 손으로 누른 때만 — 자동 사냥이 고른 것은 띄우지 않는다(사용자 2026-10-05).
        _targetPlate.Visible = _target.Text.Length > 0 && _world.TargetByHand;
    }

}
