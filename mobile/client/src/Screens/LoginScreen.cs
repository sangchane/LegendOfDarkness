using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// Login greybox, built to section 3 of the wireframes. Our own labels are Korean and the server's own
/// messages are left as they arrive, which is decision 1 in that document.
/// </summary>
/// <remarks>
/// 카카오 로그인(사용자 2026-10-11, <c>autopilot/game-kakao-login/SPEC.md</c>) — 캐릭터 비밀번호 대신 카카오로 받은 게임 표를
/// 비밀번호 칸에 보낸다(<see cref="KakaoSignIn" />). 표가 없으면 노란 [카카오로 로그인] 하나, 있으면 내 캐릭터 카드 — 지금 입은 옷
/// 그대로 선 그림·이름, 누르면 들어가고 위 귀퉁이 아이콘이 생김새·삭제, 아래 [새 캐릭터]·[카카오 로그아웃](<c>autopilot/game-kakao-accounts/SPEC.md</c>).
/// 데스크톱 <c>--login</c>·<c>login.cfg</c>(맥 서버 시험용 이름·비밀번호)는 이름·비밀번호 칸으로 표 대신 쓴다.
/// </remarks>
public partial class LoginScreen : Control
{
    // 판은 안전 영역을 꽉 채우고 안의 것도 그만큼 키운다(사용자 2026-10-11: 판이 가운데 작게 떠 위아래가 비었다).
    private const int TitleFontSize = 28;

    private static int LogoHeight => Main.Portrait ? 140 : 120;
    private const int AuxFontSize = 14;
    private const int FootFontSize = 10;

    /// <summary>로그인 화면 단추 높이 — 손가락 최소(<see cref="Main.TouchMinimum" />)보다 크게.</summary>
    private const int ButtonHeight = 56;

    // 캐릭터 카드 — 원작 그림 2배(정수배라 흐려지지 않는다) + 이름·Lv 두 줄. 폭은 판 안에 두 장 반쯤 보이게 — 반 장이 굴릴 수 있다고 알린다.
    private const int CardWidth = 150;
    private const int FigureHeight = 120;
    private const int FigureScale = 2;
    private const int CaptionHeight = 48;
    private const int CaptionWidth = 72;

    private const string BrowserText = "브라우저에서 카카오 로그인을 마치고 돌아오세요.";
    private const string PendingText = "초대 번호를 넣거나 관리자 승인을 기다려 주세요.";

    private readonly ConcurrentQueue<string> _reported = new();
    private readonly CancellationTokenSource _closing = new();

    private Task<WorldSession>? _attempt;
    private WorldSession? _session;

    /// <summary>Called on the main thread once the character is in the world.</summary>
    public Action<WorldSession>? Entered { get; set; }

    /// <summary>계정이 없다 — 만들기 화면으로 가고 싶을 때(Main 이 화면을 바꿔 준다).</summary>
    public Action? WantsToCreate { get; set; }

    /// <summary>[생김새] — 만들기 화면을 바꾸기 모드로(Main 이 화면을 바꿔 준다).</summary>
    public Action<KakaoCharacter>? WantsToChangeLook { get; set; }

    private MarginContainer _safeArea = null!;
    private Label _status = null!;
    private LineEdit _username = null!;

    /// <summary>시험 계정(<see cref="Rehearsing" />)의 비밀번호 — 이름 칸과 함께 시험 계정에서만 보인다.</summary>
    private LineEdit _password = null!;
    private Button _submit = null!;
    private Button _create = null!;
    private Button _signOut = null!;
    private Button _kakao = null!;
    private CheckBox _autoLogin = null!;
    private Label _who = null!;

    // 상태마다 보이고 숨는 줄.
    private Control _whoRow = null!;
    private Control _nameRow = null!;
    private Control _passwordRow = null!;
    private Control _actions = null!;
    private Control _linkRow = null!;

    // 내 캐릭터 목록 — 굴림 칸·카드들·카드와 그 아이콘(접속하는 동안 잠근다)·지우기 확인 판.
    private ScrollContainer _listScroll = null!;
    private VBoxContainer _list = null!;
    private readonly List<Button> _rowButtons = [];
    private Control? _confirm;

    /// <summary>받은 목록, 없으면 아직 못 받음(<see cref="_listNote" /> 가 까닭).</summary>
    private IReadOnlyList<KakaoCharacter>? _characters;
    private string _listNote = string.Empty;
    private bool _loadingList;

    /// <summary>들어가는 캐릭터 이름 — 들어가면 마지막 이름으로 남긴다.</summary>
    private string _entering = string.Empty;

    /// <summary>지우는 중(0x02 <c>표|delete</c>)과 그 이름.</summary>
    private Task? _deleting;
    private string _deletingName = string.Empty;

    /// <summary>표와 마지막 이름으로 손 없이 들어가나 — 저장된 값에서 시작한다.</summary>
    private bool _autoLoginWanted;

    // 카카오 기다림 — 받은 표(없으면 기다리지 않는다)·받은 시각·묻는 중인지.
    private string? _ticket;
    private ulong _ticketStarted;
    private bool _asking;
    // 표를 받는 중 — 화면을 돌려 판을 다시 지어도 카카오 단추가 다시 눌리지 않게(리뷰 2026-10-11).
    private bool _openingKakao;
    private Godot.Timer _poll = null!;

    // 키보드가 올라와 있는 동안 접어 두는 줄(아래 환경·안내 한 줄), 그리고 화면을 올린 만큼.
    private Control _versionLine = null!;
    private float _slide;

    /// <summary>Launch-time rehearsal may submit once; a screen reached by explicit logout only fills the fields.</summary>
    public bool AutomaticLogin { get; init; } = true;

    /// <summary>데스크톱 시험 계정(<c>--login</c>·<c>login.cfg</c>) — 표 대신 그 비밀번호로 들어간다(카카오를 끈 맥 서버).</summary>
    private static bool Rehearsing => Main.Rehearsal.Username.Length > 0;

    private static bool SignedIn => Rehearsing || Main.Kakao is not null;

    public LoginScreen()
    {
        Name = "LoginScreen";
        AnchorRight = 1;
        AnchorBottom = 1;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    /// <summary>레이아웃 검사가 훑는 자리들(숨은 것은 건너뛴다). 지우기 확인 판이 떠 있으면 판을 덮으니 그것만.</summary>
    public IReadOnlyList<(string Name, Control Part)> Parts => _confirm is not null
        ? [("삭제 확인", _confirm)]
        :
        [
            ("카카오 단추", _kakao), ("카카오 계정", _whoRow), ("이름", _nameRow), ("비밀번호", _passwordRow), ("캐릭터 목록", _listScroll),
            ("안내", _status), ("들어가기 줄", _actions), ("새 캐릭터 줄", _linkRow), ("아래 안내 줄", _versionLine)
        ];

    public override void _Ready()
    {
        AddChild(Greybox.EntryBackground());
        _safeArea = Main.SafeAreaContainer();
        AddChild(_safeArea);
        AddChild(_loading);

        _poll = new Godot.Timer { WaitTime = KakaoSignIn.PollSeconds };
        _poll.Timeout += AskForToken;
        AddChild(_poll);

        _autoLoginWanted = Main.AutoLogin;
        Build();

        // --login / login.cfg (desktop rehearsal) wins over a saved Kakao sign-in when both are somehow present.
        _username.Text = Rehearsing ? Main.Rehearsal.Username : string.Empty;
        _password.Text = Rehearsing ? Main.Rehearsal.Password : string.Empty;
        _status.Text = SignedIn ? string.Empty : "카카오로 로그인하면 캐릭터 비밀번호 없이 들어갑니다.";
        ApplyKakaoPreview();
        RefreshSubmitState();

        if (AutomaticLogin && Rehearsing)
        {
            BeginLogin(_username.Text);
        }
        else if (AutomaticLogin && Main.Kakao is not null && _autoLoginWanted && Main.LastCharacter.Length > 0)
        {
            BeginLogin(Main.LastCharacter);
        }

        if (!Rehearsing && Main.KakaoPreview.Length == 0)
        {
            LoadCharacters();
        }

        Main.LayoutChanged += Rebuild;
        CheckForUpdate();
    }

    /// <summary>
    /// 앱 안 번호(빌드 스크립트가 넣는 <c>app-version.txt</c>)보다 내려받기 페이지의 번호가 크면 새로 받으라고 알린다
    /// (<see cref="AppUpdate" />). 맥에서 띄운 판은 번호 파일이 없어 묻지 않는다.
    /// </summary>
    private void CheckForUpdate()
    {
        const string VersionFile = "res://app-version.txt";

        if (!Godot.FileAccess.FileExists(VersionFile))
        {
            return;
        }

        string mine = Godot.FileAccess.GetFileAsString(VersionFile);
        string platform = OS.GetName() switch { "Windows" => "windows", "Android" => "android", _ => "ios" };
        HttpRequest ask = new();
        AddChild(ask);
        ask.RequestCompleted += (result, code, _, body) =>
        {
            ask.QueueFree();

            if (result == (long)HttpRequest.Result.Success && code == 200
                && AppUpdate.Outdated(mine, System.Text.Encoding.UTF8.GetString(body)))
            {
                ShowUpdateNotice();
            }
        };
        ask.Request($"{AppUpdate.DownloadPage}version-{platform}.txt", ["Cache-Control: no-cache"]);
    }

    private void ShowUpdateNotice()
    {
        CenterContainer cover = new() { AnchorRight = 1, AnchorBottom = 1, ZIndex = 100 };
        VBoxContainer list = new();
        list.AddThemeConstantOverride("separation", Main.Gutter);
        list.AddChild(new Label { Text = "새 버전이 나왔습니다.\n내려받기 페이지에서 새로 받아 주세요.", HorizontalAlignment = HorizontalAlignment.Center });

        Button open = new() { Text = "새로 받기", CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        Greybox.Commit(open);
        open.Pressed += () => OS.ShellOpen(AppUpdate.DownloadPage);
        Button later = new() { Text = "나중에", CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        later.Pressed += cover.QueueFree;
        list.AddChild(open);
        list.AddChild(later);

        PanelContainer inner = new();
        inner.AddThemeStyleboxOverride("panel", Greybox.Plate());
        inner.AddChild(list);
        PanelContainer plate = new() { CustomMinimumSize = new Vector2(300, 0) };
        plate.AddThemeStyleboxOverride("panel", Greybox.Stone());
        plate.AddChild(inner);
        cover.AddChild(plate);
        AddChild(cover);
    }

    private void Build()
    {
        _safeArea.AddChild(BuildForm());
        ShowState();
    }

    /// <summary>돌리면 판을 다시 짓는다 — 친 글자·안내·카카오 기다림·목록은 그대로.</summary>
    private void Rebuild()
    {
        string username = _username.Text, password = _password.Text, status = _status.Text;
        foreach (Node child in _safeArea.GetChildren())
        {
            _safeArea.RemoveChild(child);
            child.QueueFree();
        }
        Build();
        _username.Text = username;
        _password.Text = password;
        RefreshSubmitState();
        _status.Text = status;
    }

    /// <summary>표가 있나·시험 계정인가에 맞춰 줄을 보이고 숨긴다.</summary>
    private void ShowState()
    {
        bool signedIn = SignedIn;

        _kakao.Visible = !signedIn;
        _kakao.Disabled = _openingKakao;
        _whoRow.Visible = signedIn;
        _nameRow.Visible = Rehearsing;
        _passwordRow.Visible = Rehearsing;
        _actions.Visible = Rehearsing;
        _listScroll.Visible = signedIn && !Rehearsing;
        _linkRow.Visible = signedIn;
        _signOut.Visible = !Rehearsing;
        _autoLogin.Visible = !Rehearsing;
        _who.Text = Rehearsing ? "시험 계정 — 비밀번호로 들어갑니다" : $"카카오: {Main.Kakao?.Name}";
        FillList();
    }

    /// <summary>
    /// Starts the login and leaves it running. Nothing here touches the tree from the task: progress lands
    /// in a queue and _Process drains it, because a Godot node may only be changed on the main thread.
    /// </summary>
    private void BeginLogin(string name)
    {
        if (_attempt is not null || _deleting is not null || Secret() is not { Length: > 0 } secret || name.Length == 0)
        {
            return;
        }

        _entering = name;
        _status.Text = "접속하는 중…";
        _loginStarted = Time.GetTicksMsec();

        _attempt = HadesLoginClient.LoginAsync(
            Main.ServerAddress,
            Main.ServerPort,
            name,
            secret,
            new Progress<string>(_reported.Enqueue),
            _closing.Token);
        RefreshSubmitState(keepStatus: true);
    }

    /// <summary>비밀번호 칸에 갈 것 — 시험 계정은 그 비밀번호, 아니면 게임 표.</summary>
    private string? Secret() => Rehearsing ? _password.Text : Main.Kakao?.Token;

    /// <summary>Reports what the login is doing, and what became of it.</summary>
    private readonly LoadingBand _loading = new(map: false);
    private ulong _loginStarted;

    private void DrainLogin()
    {
        while (_reported.TryDequeue(out string? line))
        {
            _status.Text = line;
        }

        // 접속하는 동안 원작 「Loading ...」 띠 — 홈은 3초에 걸쳐 90%까지 차고, 끝나면 사라진다.
        if (_attempt is not null && !_attempt.IsCompleted)
        {
            _loading.Show(Mathf.Min(0.9f, (Time.GetTicksMsec() - _loginStarted) / 3000f));
        }

        if (_attempt is null || !_attempt.IsCompleted)
        {
            return;
        }

        _loading.Visible = false;
        Task<WorldSession> finished = _attempt;
        _attempt = null;

        if (finished.IsCompletedSuccessfully)
        {
            _session = finished.Result;
            _status.Text = $"{_session.Character.CharacterName} 님, 월드에 들어왔습니다.";
            _submit.Text = "접속됨";

            if (!Rehearsing)
            {
                Main.SetLastCharacter(_entering, _autoLoginWanted);
            }

            // The world takes the connection from here, so this screen must not close it.
            WorldSession handed = _session;
            _session = null;
            Entered?.Invoke(handed);

            return;
        }

        // The server's own words when it has them, ours when the socket failed before it could speak.
        Exception failure = finished.Exception?.GetBaseException() ?? new IOException("알 수 없는 오류");
        Main.NoteActivityError(failure.GetType().Name);

        // 표가 지났거나 서명이 틀리다 — 표를 지우고 카카오 단추로(서버 KakaoLogin 문구).
        if (!Rehearsing && KakaoSignIn.IsSignedOut(failure.Message))
        {
            Main.SetKakao(null);
            ShowState();
        }

        _status.Text = failure.Message;
        RefreshSubmitState(keepStatus: true);
    }

    /// <summary>지우기가 끝났으면 — 됐으면 목록을 새로, 아니면 서버의 말(표가 끝났으면 표를 지우고 처음으로).</summary>
    private void DrainDelete()
    {
        if (_deleting is not { IsCompleted: true } finished)
        {
            return;
        }

        _deleting = null;

        if (finished.IsCompletedSuccessfully)
        {
            _status.Text = $"{_deletingName} 을(를) 지웠습니다.";

            // 지운 이름으로 자동 로그인하지 않게.
            if (_deletingName == Main.LastCharacter)
            {
                Main.SetLastCharacter(string.Empty, false);
            }

            LoadCharacters();
        }
        else
        {
            Exception failure = finished.Exception?.GetBaseException() ?? new IOException("알 수 없는 오류");
            Main.NoteActivityError(failure.GetType().Name);

            if (KakaoSignIn.IsSignedOut(failure.Message))
            {
                Main.SetKakao(null);
                ShowState();
            }

            _status.Text = failure.Message;
        }

        RefreshSubmitState(keepStatus: true);
    }

    private Control BuildForm()
    {
        // 판이 안전 영역을 꽉 채운다 — 가로에서는 굴림 칸 안에 들어가, 폭·높이를 다 쓰라고 하지 않으면 제 최소 크기로 붙었다.
        PanelContainer panel = new()
        {
            Name = "Form",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", Greybox.Stone());

        MarginContainer padding = new();
        padding.AddThemeConstantOverride("margin_left", Main.Gutter);
        padding.AddThemeConstantOverride("margin_top", Main.Gutter);
        padding.AddThemeConstantOverride("margin_right", Main.Gutter);
        padding.AddThemeConstantOverride("margin_bottom", Main.Gutter);

        TextureRect crest = Greybox.EntryTitle(new Vector2(0, LogoHeight));

        Label title = new()
        {
            Text = "어둠의 전설",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        title.AddThemeColorOverride("font_color", Greybox.Title);

        _username = Field(LineEdit.VirtualKeyboardTypeEnum.Default);
        // 비밀번호 칸은 iOS 에 비밀번호라고 알린다(자동 완성 줄의 열쇠).
        _password = Field(LineEdit.VirtualKeyboardTypeEnum.Password);
        _password.Secret = true;

        // 엔터(키보드의 완료)는 다음 칸으로, 마지막 칸에서는 들어가기로 — 손가락이 키보드를 떠나지 않아도 된다.
        _username.TextSubmitted += _ =>
        {
            if (_passwordRow.Visible) _password.Edit();
            else SubmitFromKeyboard();
        };
        _password.TextSubmitted += _ => SubmitFromKeyboard();

        _status = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        _status.AddThemeColorOverride("font_color", Greybox.Muted);

        _submit = new Button
        {
            Text = "들어가기",
            CustomMinimumSize = new Vector2(0, ButtonHeight)
        };

        Greybox.Commit(_submit);
        StyleBoxFlat disabled = Greybox.Sheet();
        disabled.SetCornerRadiusAll(Greybox.Round);
        disabled.SetContentMarginAll(0);
        _submit.AddThemeStyleboxOverride("disabled", disabled);

        _create = PlainButton("새 캐릭터");
        _signOut = PlainButton("카카오 로그아웃");
        _kakao = KakaoButton();

        _autoLogin = new CheckBox
        {
            ToggleMode = true,
            ButtonPressed = _autoLoginWanted,
            CustomMinimumSize = new Vector2(0, ButtonHeight)
        };
        _autoLogin.Text = "자동 로그인";

        _autoLogin.Pressed += () =>
        {
            _autoLoginWanted = _autoLogin.ButtonPressed;

            // 끄면 그 자리에서 — 켜는 것은 이 이름으로 실제로 들어갔을 때 남는다.
            if (!_autoLoginWanted && Main.AutoLogin)
            {
                Main.SetLastCharacter(Main.LastCharacter, false);
            }
        };

        Control form = Main.Portrait
            ? PortraitForm(crest, title)
            : LandscapeForm(crest, title);
        form.SizeFlagsVertical = SizeFlags.ExpandFill;

        // 환경·안내 한 줄은 판 안 맨 아래(사용자 2026-10-11: 테두리 밖에 떠 있었다).
        VBoxContainer inside = FormColumn();
        inside.AddChild(form);
        inside.AddChild(_versionLine = BuildVersionLine());
        padding.AddChild(inside);
        PanelContainer interior = new();
        StyleBoxFlat surface = Greybox.Sheet();
        surface.SetContentMarginAll(0);
        interior.AddThemeStyleboxOverride("panel", surface);
        interior.AddChild(padding);
        panel.AddChild(interior);

        _username.TextChanged += _ => RefreshSubmitState();
        _password.TextChanged += _ => RefreshSubmitState();
        _submit.Pressed += () => BeginLogin(_username.Text);
        _create.Pressed += () =>
        {
            // 켠 자동 로그인을 만들기 화면이 이어받게 — 만들기는 저장된 값을 쓴다(리뷰 2026-10-11).
            if (_autoLoginWanted != Main.AutoLogin)
            {
                Main.SetLastCharacter(Main.LastCharacter, _autoLoginWanted);
            }

            WantsToCreate?.Invoke();
        };
        _signOut.Pressed += SignOut;
        _kakao.Pressed += StartKakao;

        if (Main.Portrait)
        {
            return panel;
        }

        // A short landscape screen (360) can have less height than the form. Keep the controls at their accessible
        // size and let the form scroll rather than shrinking or clipping them. The keyboard no longer shrinks this
        // area — the screen slides instead (_Process) — so the scroll is only for short screens.
        ScrollContainer formScroll = new()
        {
            Name = "FormScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        formScroll.AddChild(panel);

        return formScroll;
    }

    /// <summary>The established phone flow stays one vertical column.</summary>
    private Control PortraitForm(TextureRect crest, Label title)
    {
        VBoxContainer form = FormColumn();
        form.Alignment = BoxContainer.AlignmentMode.Center;
        form.AddChild(crest);
        form.AddChild(title);
        form.AddChild(BuildWho());
        AddCredentials(form);

        return form;
    }

    /// <summary>
    /// A short, two-column version of the same form for landscape: identity on the left and the actual
    /// task on the right. It removes two tall rows without shrinking fields or touch targets.
    /// </summary>
    private Control LandscapeForm(TextureRect crest, Label title)
    {
        HBoxContainer form = new();
        form.AddThemeConstantOverride("separation", Main.Gutter * 2);

        VBoxContainer identity = FormColumn();
        identity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        identity.SizeFlagsStretchRatio = 2;
        identity.Alignment = BoxContainer.AlignmentMode.Center; // 로고는 오른쪽 칸 높이의 가운데(사용자 2026-10-03: 위에 붙지 않게)
        identity.AddChild(crest);
        identity.AddChild(title);
        identity.AddChild(BuildWho());

        VBoxContainer credentials = FormColumn();
        credentials.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        credentials.SizeFlagsStretchRatio = 3;
        credentials.Alignment = BoxContainer.AlignmentMode.Center;
        AddCredentials(credentials);

        form.AddChild(identity);
        form.AddChild(credentials);

        return form;
    }

    private static VBoxContainer FormColumn()
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", Main.Gutter);

        return column;
    }

    /// <summary>
    /// 카카오 이름과 자동 로그인 — 세로는 한 줄, 가로는 왼쪽 로고 아래 두 줄(오른쪽 칸이 한 줄 줄어 짧은 가로 화면에 판이 다 든다).
    /// </summary>
    private Control BuildWho()
    {
        _who = Aux(string.Empty);
        _who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _who.VerticalAlignment = VerticalAlignment.Center;
        _who.HorizontalAlignment = Main.Portrait ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        _who.ClipText = true;
        _who.AddThemeColorOverride("font_color", Greybox.Title);
        _autoLogin.SizeFlagsHorizontal = Main.Portrait ? SizeFlags.Fill : SizeFlags.ShrinkCenter;

        BoxContainer who = Main.Portrait ? new HBoxContainer() : new VBoxContainer();
        who.AddThemeConstantOverride("separation", Main.Gutter);
        who.AddChild(_who);
        who.AddChild(_autoLogin);

        return _whoRow = who;
    }

    /// <summary>
    /// 세 상태의 줄을 모두 쌓고 <see cref="ShowState" /> 가 고른다. 표 없음: [카카오로 로그인] · 안내.
    /// 표 있음: 내 캐릭터 목록 · 안내 · [새 캐릭터][카카오 로그아웃]. 시험 계정: 이름 · 비밀번호 · 안내 · [들어가기] · [새 캐릭터].
    /// </summary>
    private void AddCredentials(Container form)
    {
        // Captions sit beside their fields rather than above them, so the keyboard never turns each input
        // into two rows. Both orientations use this same compact field treatment.
        form.AddChild(_nameRow = FieldRow("캐릭터", _username));
        form.AddChild(_passwordRow = FieldRow("비밀번호", _password));
        form.AddChild(_kakao);

        // 카드는 가로로 늘어놓고 넘치면 판 안에서 가로로 굴린다(사용자 2026-10-11). 목록이 넓게 펴져야(ExpandFill) 안내 글이 판 폭으로 접힌다.
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", Main.Gutter);
        _listScroll = new ScrollContainer { VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _listScroll.AddChild(_list);
        form.AddChild(_listScroll);

        form.AddChild(_status);
        form.AddChild(_actions = _submit);
        _submit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        form.AddChild(_linkRow = ButtonRow(_create, _signOut));
    }

    /// <summary>
    /// 목록을 그린다 — 캐릭터마다 카드 한 장(<see cref="CharacterCard" />). 비면 만들라는 말, 못 받았으면 까닭과 [다시 불러오기].
    /// </summary>
    private void FillList()
    {
        foreach (Node child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        _rowButtons.Clear();

        if (_characters is null || _characters.Count == 0)
        {
            Label note = Aux(_characters is null ? _listNote : "캐릭터가 없습니다. 새 캐릭터를 만드세요.");
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            note.AddThemeColorOverride("font_color", Greybox.Text);
            _list.AddChild(note);

            if (_characters is null && !_loadingList)
            {
                Button again = PlainButton("다시 불러오기");
                again.Pressed += LoadCharacters;
                _list.AddChild(again);
            }
        }
        else
        {
            HBoxContainer cards = new();
            cards.AddThemeConstantOverride("separation", Main.Gutter);

            foreach (KakaoCharacter character in _characters)
            {
                cards.AddChild(CharacterCard(character));
            }

            _list.AddChild(cards);
        }

        RefreshSubmitState(keepStatus: true);
    }

    /// <summary>
    /// 캐릭터 카드(사용자 2026-10-11) — 지금 입은 옷 그대로 선 그림(게임이 사람을 그리는 <see cref="WorldView.Dress" />) · 이름 · Lv.
    /// 카드를 누르면 들어간다. 위 귀퉁이 아이콘 둘(생김새·삭제)은 카드의 자식이 아니라 카드와 나란한 형제다 — 목록 안 단추는
    /// 끌기를 굴림 칸에 넘기려고 누름을 부모에게도 넘기므로(<see cref="TouchInput" /> Adopt), 자식이던 아이콘의 누름이 카드에도
    /// 가서 들어가기가 함께 불렸다(사용자 2026-10-11 실기기). 검사: <c>--kakao 표 --card-taps</c>.
    /// </summary>
    private Control CharacterCard(KakaoCharacter character)
    {
        Control slot = new() { CustomMinimumSize = new Vector2(CardWidth, FigureHeight + CaptionHeight), MouseFilter = MouseFilterEnum.Pass };
        Button card = new() { Name = "Card", ClipContents = true };
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        slot.AddChild(card);
        Greybox.Plain(card);
        card.Pressed += () => BeginLogin(character.Name);

        // 만들기 화면 미리보기와 같은 자리 맞춤 — 발 기준점이 아니라 그림 한가운데가 칸 가운데(1배라 흐려지지 않는다).
        Control stage = new() { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        stage.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        stage.OffsetBottom = FigureHeight;
        Node2D wrapper = new()
        {
            Position = new Vector2(CardWidth / 2f + CreateScreen.BodyCentreOffsetX * FigureScale, FigureHeight / 2f + CreateScreen.BodyCentreOffsetY * FigureScale),
            Scale = new Vector2(FigureScale, FigureScale)
        };
        Actor figure = new(character.Name, WorldView.Dress(new Character(0, default, Direction.South, character.Look, character.Name)));
        wrapper.AddChild(figure);
        stage.AddChild(wrapper);
        card.AddChild(stage);
        figure.Face(Direction.South);

        Label name = new() { Text = character.Name, HorizontalAlignment = HorizontalAlignment.Center, ClipText = true };
        name.AddThemeFontSizeOverride("font_size", 17);
        name.AddThemeColorOverride("font_color", Greybox.Title);
        Label level = Aux($"Lv {character.Level} · {character.Job}");
        level.HorizontalAlignment = HorizontalAlignment.Center;
        level.ClipText = true;
        VBoxContainer caption = new() { MouseFilter = MouseFilterEnum.Ignore };
        caption.AddThemeConstantOverride("separation", 0);
        caption.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        caption.OffsetTop = -CaptionHeight;
        caption.AddChild(name);
        caption.AddChild(level);
        card.AddChild(caption);

        // 한글 시스템 글꼴에 연필(✎)이 없어(맥에서 HasChar 거짓) 연필은 그려 넣는다. ×는 글꼴에 있다.
        Button look = CornerIcon(slot, left: true, string.Empty, "생김새 바꾸기");
        Button delete = CornerIcon(slot, left: false, "×", "지우기");
        look.Pressed += () => WantsToChangeLook?.Invoke(character);
        delete.Pressed += () => AskDelete(character.Name);
        _rowButtons.AddRange([card, look, delete]);

        return slot;
    }

    /// <summary>카드 위 귀퉁이 아이콘 — 손가락 칸은 <see cref="Main.TouchMinimum" />, 보이는 것은 그 안의 작은 어두운 동그라미.</summary>
    private static Button CornerIcon(Control card, bool left, string glyph, string tip)
    {
        Button button = new() { Name = left ? "Look" : "Delete", TooltipText = tip, Size = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        button.SetAnchorsAndOffsetsPreset(left ? LayoutPreset.TopLeft : LayoutPreset.TopRight);
        button.OffsetLeft = left ? 0 : -Main.TouchMinimum;
        button.OffsetRight = left ? Main.TouchMinimum : 0;
        button.OffsetBottom = Main.TouchMinimum;
        foreach (string state in new[] { "normal", "hover", "focus", "pressed", "hover_pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        const int Dot = 26;
        Label mark = new()
        {
            Text = glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(left ? 4 : Main.TouchMinimum - Dot - 4, 4),
            Size = new Vector2(Dot, Dot)
        };
        StyleBoxFlat plate = Greybox.Sheet();
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(Dot / 2 - 1);
        plate.SetContentMarginAll(0);
        mark.AddThemeStyleboxOverride("normal", plate);
        mark.AddThemeFontSizeOverride("font_size", 14);
        mark.AddThemeColorOverride("font_color", Greybox.Text);
        if (glyph.Length == 0)
        {
            // 연필 — 몸통 사선·뾰족한 끝·끝의 지우개(글자보다 먼저 그린 판 위에 얹힌다).
            mark.Draw += () =>
            {
                mark.DrawLine(new Vector2(9, 17), new Vector2(17, 9), Greybox.Text, 4);
                mark.DrawColoredPolygon([new Vector2(6, 20), new Vector2(7.5f, 15.5f), new Vector2(10.5f, 18.5f)], Greybox.Text);
                mark.DrawLine(new Vector2(17, 9), new Vector2(19.5f, 6.5f), Greybox.Accent, 4);
            };
        }
        button.AddChild(mark);
        card.AddChild(button);

        return button;
    }

    /// <summary>내 캐릭터 목록을 홈페이지에 묻는다. 표가 끝났거나(401) 대기·거부(403)면 표를 지우고 카카오 단추로.</summary>
    private void LoadCharacters()
    {
        if (Main.Kakao is not { } kakao || _loadingList)
        {
            return;
        }

        _loadingList = true;
        _characters = null;
        _listNote = "내 캐릭터를 불러오는 중…";
        FillList();

        // 걸린 채 멈추지 않게 — 넘으면 「못 닿음」으로 끝난다.
        HttpRequest ask = new() { Timeout = 10 };
        AddChild(ask);
        ask.RequestCompleted += (result, code, _, body) =>
        {
            ask.QueueFree();
            _loadingList = false;

            // 그사이 로그아웃했거나 다른 카카오로 들어왔다 — 옛 답은 버린다.
            if (Main.Kakao != kakao)
            {
                return;
            }

            string answer = System.Text.Encoding.UTF8.GetString(body);

            if (result == (long)HttpRequest.Result.Success && code is 401 or 403)
            {
                Main.SetKakao(null);
                ShowState();
                _status.Text = "카카오 로그인이 끝났습니다. [카카오로 로그인]을 다시 눌러 주세요.";
                return;
            }

            _characters = result == (long)HttpRequest.Result.Success ? KakaoSignIn.ReadCharacters(code, answer) : null;
            _listNote = result == (long)HttpRequest.Result.Success
                ? KakaoSignIn.ErrorOf(answer, "캐릭터 목록을 불러오지 못했습니다. 잠시 뒤 다시 해 주세요.")
                : "홈페이지에 닿지 못했습니다. 인터넷 연결을 확인해 주세요.";
            FillList();
        };

        if (ask.Request(KakaoSignIn.CharactersUrl, ["Content-Type: application/json"], Godot.HttpClient.Method.Post, KakaoSignIn.CharactersRequest(kakao.Token)) != Godot.Error.Ok)
        {
            ask.QueueFree();
            _loadingList = false;
            _listNote = "홈페이지에 닿지 못했습니다. 인터넷 연결을 확인해 주세요.";
            FillList();
        }
    }

    /// <summary>[삭제] — 한 번 묻고, [지우기]면 0x02 <c>표|delete</c>.</summary>
    private void AskDelete(string name)
    {
        CenterContainer cover = new() { AnchorRight = 1, AnchorBottom = 1, ZIndex = 100 };
        VBoxContainer list = new();
        list.AddThemeConstantOverride("separation", Main.Gutter);
        list.AddChild(new Label
        {
            Text = $"{name} 을(를) 지웁니다.\n되돌리려면 관리자에게 말해야 합니다.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        Button cancel = PlainButton("취소");
        Button delete = new() { Text = "지우기", CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        Greybox.Commit(delete);
        cancel.Pressed += CloseConfirm;
        delete.Pressed += () =>
        {
            CloseConfirm();

            if (Main.Kakao is not { } kakao || _attempt is not null || _deleting is not null)
            {
                return;
            }

            _deletingName = name;
            _status.Text = "지우는 중…";
            _deleting = HadesLoginClient.DeleteCharacterAsync(Main.ServerAddress, Main.ServerPort, name, kakao.Token, _closing.Token);
            RefreshSubmitState(keepStatus: true);
        };
        list.AddChild(ButtonRow(cancel, delete));

        PanelContainer inner = new();
        inner.AddThemeStyleboxOverride("panel", Greybox.Plate());
        inner.AddChild(list);
        PanelContainer plate = new() { CustomMinimumSize = new Vector2(300, 0) };
        plate.AddThemeStyleboxOverride("panel", Greybox.Stone());
        plate.AddChild(inner);
        cover.AddChild(plate);
        AddChild(cover);
        _confirm = plate;
    }

    private void CloseConfirm()
    {
        _confirm?.GetParent().QueueFree();
        _confirm = null;
    }

    /// <summary>두 단추를 한 줄에 반씩(사용자 2026-10-03: 가로 화면에서 한 줄이 빠져 창이 화면 안에 다 든다).</summary>
    private static Control ButtonRow(Button left, Button right)
    {
        HBoxContainer actions = new();
        actions.AddThemeConstantOverride("separation", Main.Gutter);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(left);
        actions.AddChild(right);

        return actions;
    }

    private static Button PlainButton(string text)
    {
        Button button = new() { Text = text, CustomMinimumSize = new Vector2(0, ButtonHeight) };
        Greybox.Plain(button);

        return button;
    }

    /// <summary>
    /// 카카오 로그인 단추 — 카카오 디자인 가이드대로 노란 바탕 #FEE500, 왼쪽 검은 말풍선, 가운데 검정 85% 글자, 모서리 12.
    /// 원작 테마의 회색 채움 금지와 따로 간다(정체성 단추라 가이드를 지킨다).
    /// </summary>
    private static Button KakaoButton()
    {
        Color yellow = new("#FEE500");
        Color label = new(0, 0, 0, 0.85f);
        Button button = new() { Text = "카카오로 로그인", CustomMinimumSize = new Vector2(0, ButtonHeight) };

        foreach (string state in new[] { "normal", "hover", "focus", "pressed", "hover_pressed", "disabled" })
        {
            StyleBoxFlat face = new()
            {
                BgColor = state switch
                {
                    "pressed" or "hover_pressed" => yellow.Darkened(0.1f),
                    "disabled" => yellow with { A = 0.6f },
                    _ => yellow
                }
            };
            face.SetCornerRadiusAll(12);
            face.SetContentMarginAll(4);
            button.AddThemeStyleboxOverride(state, face);
        }

        foreach (string colour in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color", "font_disabled_color" })
        {
            button.AddThemeColorOverride(colour, label);
        }

        Control symbol = new()
        {
            AnchorBottom = 1,
            OffsetLeft = 14,
            OffsetRight = 14 + 24,
            MouseFilter = MouseFilterEnum.Ignore
        };
        symbol.Draw += () =>
        {
            Vector2 middle = symbol.Size / 2 + new Vector2(0, -1);
            Vector2[] bubble = new Vector2[20];

            for (int at = 0; at < bubble.Length; at++)
            {
                float turn = Mathf.Tau * at / bubble.Length;
                bubble[at] = middle + new Vector2(Mathf.Cos(turn) * 10, Mathf.Sin(turn) * 8);
            }

            symbol.DrawColoredPolygon(bubble, Colors.Black);
            symbol.DrawColoredPolygon([middle + new Vector2(-6, 4), middle + new Vector2(-2, 6), middle + new Vector2(-7, 11)], Colors.Black);
        };
        button.AddChild(symbol);

        return button;
    }

    private static Control FieldRow(string caption, LineEdit field)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label caption_label = Aux(caption);
        caption_label.CustomMinimumSize = new Vector2(CaptionWidth, 0);
        caption_label.VerticalAlignment = VerticalAlignment.Center;

        field.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        row.AddChild(caption_label);
        row.AddChild(field);

        return row;
    }

    private static LineEdit Field(LineEdit.VirtualKeyboardTypeEnum keyboard)
    {
        LineEdit field = new()
        {
            VirtualKeyboardType = keyboard,
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
        };
        StyleField(field, bright: false);

        return field;
    }

    /// <summary>입력칸 — 단색 판, 쓰는 중이면 흐린 테두리, <paramref name="bright" /> 면 밝은 테두리(지금 넣을 칸).</summary>
    private static void StyleField(LineEdit field, bool bright)
    {
        foreach (string state in new[] { "normal", "focus", "read_only" })
        {
            StyleBoxFlat inputBackground = Greybox.Sheet();
            inputBackground.SetContentMarginAll(4);
            inputBackground.SetCornerRadiusAll(8);
            if (state == "focus") inputBackground.BorderColor = Greybox.Muted;
            if (bright) inputBackground.BorderColor = Greybox.Title;
            field.AddThemeStyleboxOverride(state, inputBackground);
        }
    }

    /// <summary>The keyboard's Return on the last field: log in when ready, else go back to the empty name.</summary>
    private void SubmitFromKeyboard()
    {
        if (!_submit.Disabled)
        {
            BeginLogin(_username.Text);
        }
        else if (_username.Text.Length == 0)
        {
            _username.Edit();
        }
    }

    /// <summary>[카카오 로그아웃] — 이 기기의 표를 지우고 카카오 단추로.</summary>
    private void SignOut()
    {
        Main.SetKakao(null);
        _characters = null;
        ShowState();
        _status.Text = "카카오에서 로그아웃했습니다.";
    }

    /// <summary>
    /// [카카오로 로그인] — 홈페이지에서 표를 받아 브라우저로 카카오 로그인을 열고 2초마다 묻는다. 기다리는 중에 다시 누르면
    /// 새 표로 처음부터(브라우저를 닫아 버렸을 때).
    /// </summary>
    private void StartKakao()
    {
        ForgetTicket();
        _openingKakao = true;
        _status.Text = "카카오 로그인을 여는 중…";
        ShowState();

        // 걸린 채 멈추지 않게 — 넘으면 「못 닿음」으로 끝난다.
        HttpRequest ask = new() { Timeout = 10 };
        AddChild(ask);
        ask.RequestCompleted += (result, code, _, body) =>
        {
            ask.QueueFree();
            _openingKakao = false;
            _kakao.Disabled = false;
            string answer = System.Text.Encoding.UTF8.GetString(body);

            if (result == (long)HttpRequest.Result.Success && KakaoSignIn.ReadTicket(code, answer) is { } ticket)
            {
                _ticket = ticket.Ticket;
                _ticketStarted = Time.GetTicksMsec();
                _status.Text = BrowserText;
                ShowState();
                _poll.Start();
                OS.ShellOpen(ticket.Browser);
                return;
            }

            _status.Text = result == (long)HttpRequest.Result.Success
                ? KakaoSignIn.ErrorOf(answer, "카카오 로그인을 열지 못했습니다. 잠시 뒤 다시 해 주세요.")
                : "홈페이지에 닿지 못했습니다. 인터넷 연결을 확인해 주세요.";
        };

        if (ask.Request(KakaoSignIn.TicketUrl, [], Godot.HttpClient.Method.Post) != Godot.Error.Ok)
        {
            ask.QueueFree();
            _openingKakao = false;
            _kakao.Disabled = false;
            _status.Text = "홈페이지에 닿지 못했습니다. 인터넷 연결을 확인해 주세요.";
        }
    }

    /// <summary>2초마다 — 게임 표를 달라고 묻는다. 한 번에 하나만.</summary>
    private void AskForToken()
    {
        if (_ticket is not { } ticket || _asking)
        {
            return;
        }

        if (Time.GetTicksMsec() - _ticketStarted > KakaoSignIn.WaitSeconds * 1000)
        {
            StartOver("10분이 지났습니다. [카카오로 로그인]을 다시 눌러 주세요.");
            return;
        }

        _asking = true;
        // 걸린 채 멈추면 다음 물음도 10분 검사도 오지 않는다 — 넘으면 「못 닿음」(기다림)으로 끝난다.
        HttpRequest ask = new() { Timeout = 10 };
        AddChild(ask);
        ask.RequestCompleted += (result, code, _, body) =>
        {
            ask.QueueFree();
            _asking = false;

            // 그사이 처음부터 다시 했다 — 옛 표의 답은 버린다.
            if (_ticket != ticket)
            {
                return;
            }

            Heard(KakaoSignIn.ReadToken(result == (long)HttpRequest.Result.Success ? code : 0, System.Text.Encoding.UTF8.GetString(body)));
        };

        if (ask.Request(KakaoSignIn.TokenUrl, ["Content-Type: application/json"], Godot.HttpClient.Method.Post, KakaoSignIn.TokenRequest(ticket)) != Godot.Error.Ok)
        {
            ask.QueueFree();
            _asking = false;
        }
    }

    private void Heard(KakaoReply reply)
    {
        switch (reply.State)
        {
            case KakaoWait.Waiting:
                // 못 닿은 것도 여기.
                _status.Text = BrowserText;
                break;

            case KakaoWait.Pending:
                _status.Text = reply.Name.Length > 0 ? $"{reply.Name} 님 — {PendingText}" : PendingText;
                break;

            case KakaoWait.Granted:
                ForgetTicket();
                Main.SetKakao(new KakaoAccount(reply.Token, reply.Name));
                ShowState();
                _status.Text = $"카카오: {reply.Name} — 캐릭터를 골라 들어가세요. 처음이면 [새 캐릭터].";
                LoadCharacters();
                break;

            case KakaoWait.Refused:
                StartOver(reply.Error);
                break;

            default:
                StartOver("로그인 표가 없거나 지났습니다. [카카오로 로그인]을 다시 눌러 주세요.");
                break;
        }
    }

    private void StartOver(string message)
    {
        ForgetTicket();
        _status.Text = message;
        ShowState();
    }

    private void ForgetTicket()
    {
        _poll.Stop();
        _ticket = null;
    }

    /// <summary>
    /// <c>--kakao 기다림|대기|표|빈목록|삭제</c> — 홈페이지 없이 그 상태를 그려 본다(<see cref="Main.KakaoPreview" />). 묻지는 않는다.
    /// </summary>
    private void ApplyKakaoPreview()
    {
        switch (Main.KakaoPreview)
        {
            case "기다림":
                _ticket = "미리보기";
                _status.Text = BrowserText;
                break;
            case "대기":
                _ticket = "미리보기";
                _status.Text = $"카카오닉네임 님 — {PendingText}";
                break;
            case "표" or "삭제":
                // 셋째 카드는 판 폭을 넘겨 가로 굴림까지 그려 보려고.
                _characters =
                [
                    Main.PretendCharacter,
                    new KakaoCharacter("새싹이", 1, "Peasant", "Female", 2, 0, new Appearance(2, 32, 2, 1, 0, 10, 0, 0, 0, 0, 0, 0, 0)),
                    new KakaoCharacter("기다란이름의전사", 99, "Warrior", "Male", 1, 2, new Appearance(1, 16, 5, 1, 1, 5, 2, 0, 0, 0, 0, 0, 0))
                ];
                break;
            case "빈목록":
                _characters = [];
                break;
            default:
                return;
        }

        ShowState();

        if (Main.KakaoPreview == "삭제")
        {
            AskDelete(Main.PretendCharacter.Name);
        }
    }

    /// <summary>
    /// 환경·앱 번호·서버와 기록 보관 안내를 아래 아주 작은 한 줄로(사용자 2026-10-11: 위아래 글이 자리를 너무 차지한다).
    /// 안내 문구는 운영상 남긴다.
    /// </summary>
    private static Control BuildVersionLine()
    {
        // 앱 번호(빌드 시각) — 내려받기 페이지의 번호와 같으면 최신이다(사용자 2026-10-03: 폰이 어느 판인지 보이게).
        string app = Godot.FileAccess.FileExists("res://app-version.txt") ? Godot.FileAccess.GetFileAsString("res://app-version.txt").Trim() : "맥";
        Label notice = Aux($"테스트 환경 · 로컬 · 앱 {app} · 서버 {Main.ServerAddress}:{Main.ServerPort} · "
                           + "접속·기기/앱 버전·화면/버튼·오류·게임 활동 기록은 운영 확인용으로 90일 보관합니다.");
        notice.AddThemeFontSizeOverride("font_size", FootFontSize);
        notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        notice.HorizontalAlignment = HorizontalAlignment.Center;
        return notice;
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }

    /// <summary>
    /// Keeps the field being typed into and the login button above the on-screen keyboard, which the wireframes
    /// require. The screen slides up by only as much as that takes (<see cref="KeyboardFit.Slide"/>) and the rows that
    /// only inform fold away meanwhile. It used to shrink the area the form centres in by the whole keyboard: on a
    /// landscape iPhone that left about 100 of 393 for a 320-tall form, which then scrolled one field at a time.
    /// </summary>
    public override void _Process(double delta)
    {
        float covered = TouchInput.Covered;
        bool typing = covered > 0;

        _versionLine.Visible = !typing;
        _status.Visible = !typing;

        float slide = 0;

        if (typing && TouchInput.Editing(GetViewport()) is { } field && IsAncestorOf(field))
        {
            // 지금 자리는 이미 올린 만큼 올라가 있다 — 올리기 전 자리로 되돌려 잰다.
            Rect2 typed = field.GetGlobalRect();
            Rect2 finish = _actions.GetGlobalRect();

            slide = KeyboardFit.Slide(
                typed.Position.Y + _slide,
                typed.End.Y + _slide,
                finish.End.Y + _slide,
                GetViewportRect().Size.Y - covered,
                Main.SafeInsets.Top,
                Main.Gutter);
        }

        if (!Mathf.IsEqualApprox(slide, _slide))
        {
            _slide = slide;
            OffsetTop = -slide;
            OffsetBottom = -slide;
        }

        DrainLogin();
        DrainDelete();
    }

    public override void _ExitTree()
    {
        Main.LayoutChanged -= Rebuild;
        _closing.Cancel();
        _session?.Dispose();
        _closing.Dispose();
    }

    /// <summary>
    /// No request goes out until it can succeed, per the wireframe transition rules: a name and the rehearsal
    /// password, or a token for the list's buttons.
    /// </summary>
    private void RefreshSubmitState(bool keepStatus = false)
    {
        // 접속·지우는 동안 표를 지우거나 다른 캐릭터를 누르지 못하게 — 이미 보낸 표로 들어가 상태가 꼬였다(리뷰 2026-10-11).
        bool busy = _attempt is not null || _session is not null || _deleting is not null;
        _signOut.Disabled = busy;
        _create.Disabled = busy;
        _rowButtons.ForEach(button => button.Disabled = busy);

        if (busy)
        {
            _submit.Disabled = true;
            return;
        }

        bool ready = _username.Text.Length > 0 && Secret() is { Length: > 0 };

        _submit.Disabled = !ready;

        if (keepStatus || !Rehearsing)
        {
            return;
        }

        _status.Text = ready ? "로그인할 준비가 됐습니다." : "사용자명과 비밀번호를 입력하세요.";
    }
}
