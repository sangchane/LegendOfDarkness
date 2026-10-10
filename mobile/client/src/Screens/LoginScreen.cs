using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// Login greybox, built to section 3 of the wireframes. Our own labels are Korean and the server's own
/// messages are left as they arrive, which is decision 1 in that document.
/// </summary>
/// <remarks>
/// 카카오 로그인(사용자 2026-10-11, <c>autopilot/game-kakao-login/SPEC.md</c>) — 캐릭터 비밀번호 대신 카카오로 받은 게임 표를
/// 비밀번호 칸에 보낸다(<see cref="KakaoSignIn" />). 표가 없으면 노란 [카카오로 로그인] 하나, 있으면 캐릭터 이름과 [들어가기]·[새 캐릭터]·
/// [옛 캐릭터 잇기]·[카카오 로그아웃]. 데스크톱 <c>--login</c>·<c>login.cfg</c>(맥 서버 시험용 이름·비밀번호)는 그 비밀번호를 표 대신 쓴다.
/// </remarks>
public partial class LoginScreen : Control
{
    private const int TitleFontSize = 22;

    private static int LogoHeight => Main.Portrait ? 112 : 88;
    private const int AuxFontSize = 14;
    private const int FormWidth = 300;
    private const int LandscapeFormWidth = 560;
    private const int CaptionWidth = 72;

    private const string BrowserText = "브라우저에서 카카오 로그인을 마치고 돌아오세요.";
    private const string CodeText = "브라우저에 보이는 번호 6자리를 넣고 [확인]을 누르세요.";
    private const string PendingText = "초대 번호를 넣거나 관리자 승인을 기다려 주세요.";

    private readonly ConcurrentQueue<string> _reported = new();
    private readonly CancellationTokenSource _closing = new();

    private Task<WorldSession>? _attempt;
    private WorldSession? _session;

    /// <summary>Called on the main thread once the character is in the world.</summary>
    public Action<WorldSession>? Entered { get; set; }

    /// <summary>계정이 없다 — 만들기 화면으로 가고 싶을 때(Main 이 화면을 바꿔 준다).</summary>
    public Action? WantsToCreate { get; set; }

    private MarginContainer _safeArea = null!;
    private Label _status = null!;
    private LineEdit _username = null!;

    /// <summary>옛 캐릭터 잇기의 옛 비밀번호 — 시험 계정(<see cref="Rehearsing" />)이면 그 비밀번호.</summary>
    private LineEdit _password = null!;

    /// <summary>브라우저에 뜬 번호 6자리.</summary>
    private LineEdit _code = null!;
    private Button _submit = null!;
    private Button _create = null!;
    private Button _link = null!;
    private Button _signOut = null!;
    private Button _kakao = null!;
    private Button _confirmCode = null!;
    private CheckBox _autoLogin = null!;
    private Label _who = null!;

    // 상태마다 보이고 숨는 줄.
    private Control _whoRow = null!;
    private Control _nameRow = null!;
    private Control _passwordRow = null!;
    private Control _codeRow = null!;
    private Control _actions = null!;
    private Control _linkRow = null!;

    /// <summary>표와 마지막 이름으로 손 없이 들어가나 — 저장된 값에서 시작한다.</summary>
    private bool _autoLoginWanted;

    /// <summary>[옛 캐릭터 잇기]로 옛 비밀번호 칸을 열었나.</summary>
    private bool _linking;

    // 카카오 기다림 — 받은 표(없으면 기다리지 않는다)·받은 시각·보낸 번호·번호를 기다리는지·묻는 중인지.
    private string? _ticket;
    private ulong _ticketStarted;
    private string? _sentCode;
    private bool _codeAsked;
    private bool _asking;
    private Godot.Timer _poll = null!;

    // 키보드가 올라와 있는 동안 접어 두는 줄(위 환경 줄·아래 판 번호·안내 한 줄), 그리고 화면을 올린 만큼.
    private Control _statusRow = null!;
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

    /// <summary>레이아웃 검사가 훑는 자리들(숨은 것은 건너뛴다).</summary>
    public IReadOnlyList<(string Name, Control Part)> Parts =>
    [
        ("카카오 단추", _kakao), ("번호 줄", _codeRow), ("카카오 계정", _whoRow), ("이름", _nameRow), ("옛 비밀번호", _passwordRow),
        ("안내", _status), ("들어가기 줄", _actions), ("잇기 줄", _linkRow)
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
        _username.Text = Rehearsing ? Main.Rehearsal.Username : Main.LastCharacter;
        _password.Text = Rehearsing ? Main.Rehearsal.Password : string.Empty;
        _status.Text = SignedIn ? string.Empty : "카카오로 로그인하면 캐릭터 비밀번호 없이 들어갑니다.";
        ApplyKakaoPreview();
        RefreshSubmitState();

        if (AutomaticLogin && (Rehearsing || (Main.Kakao is not null && _autoLoginWanted && Main.LastCharacter.Length > 0)))
        {
            BeginLogin();
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
        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);
        _safeArea.AddChild(rows);
        rows.AddChild(_statusRow = BuildStatusRow());
        rows.AddChild(BuildForm());
        rows.AddChild(_versionLine = BuildVersionLine());
        ShowState();
    }

    /// <summary>돌리면 판을 다시 짓는다 — 친 글자·안내·카카오 기다림은 그대로.</summary>
    private void Rebuild()
    {
        string username = _username.Text, password = _password.Text, code = _code.Text, status = _status.Text;
        foreach (Node child in _safeArea.GetChildren())
        {
            _safeArea.RemoveChild(child);
            child.QueueFree();
        }
        Build();
        _username.Text = username;
        _password.Text = password;
        _code.Text = code;
        _confirmCode.Disabled = !KakaoSignIn.IsCode(code);
        RefreshSubmitState();
        _status.Text = status;
    }

    /// <summary>표가 있나·기다리나·잇는 중인가에 맞춰 줄을 보이고 숨긴다.</summary>
    private void ShowState()
    {
        bool signedIn = SignedIn;

        _kakao.Visible = !signedIn;
        _codeRow.Visible = !signedIn && _ticket is not null;
        _whoRow.Visible = signedIn;
        _nameRow.Visible = signedIn;
        _actions.Visible = signedIn;
        _passwordRow.Visible = signedIn && (Rehearsing || _linking);
        _linkRow.Visible = signedIn && !Rehearsing;
        _autoLogin.Visible = !Rehearsing;
        _who.Text = Rehearsing ? "시험 계정 — 비밀번호로 들어갑니다" : $"카카오: {Main.Kakao?.Name}";
        _link.Text = _linking ? "잇기 닫기" : "옛 캐릭터 잇기";
        // 브라우저 로그인이 끝나 번호를 기다리면 번호 칸 테두리를 밝힌다.
        StyleField(_code, _codeAsked && _sentCode is null);
    }

    /// <summary>Environment on the left, which server we will talk to on the right.</summary>
    private static Control BuildStatusRow()
    {
        HBoxContainer row = new() { Name = "StatusRow" };

        row.AddChild(Aux("테스트 환경 · 로컬"));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        // 앱 번호(빌드 시각) — 내려받기 페이지의 번호와 같으면 최신이다(사용자 2026-10-03: 폰이 어느 판인지 보이게).
        string app = Godot.FileAccess.FileExists("res://app-version.txt") ? Godot.FileAccess.GetFileAsString("res://app-version.txt").Trim() : "맥";
        row.AddChild(Aux($"앱 {app} · 서버 {Main.ServerAddress}:{Main.ServerPort}"));

        return row;
    }

    /// <summary>
    /// Starts the login and leaves it running. Nothing here touches the tree from the task: progress lands
    /// in a queue and _Process drains it, because a Godot node may only be changed on the main thread.
    /// </summary>
    private void BeginLogin()
    {
        if (_attempt is not null || Secret() is not { Length: > 0 } secret || _username.Text.Length == 0)
        {
            return;
        }

        _submit.Disabled = true;
        _status.Text = "접속하는 중…";
        _loginStarted = Time.GetTicksMsec();

        _attempt = HadesLoginClient.LoginAsync(
            Main.ServerAddress,
            Main.ServerPort,
            _username.Text,
            secret,
            new Progress<string>(_reported.Enqueue),
            _closing.Token);
    }

    /// <summary>비밀번호 칸에 갈 것 — 시험 계정은 그 비밀번호, 아니면 게임 표(잇는 중이면 <c>표|옛비밀번호</c>).</summary>
    private string? Secret()
    {
        if (Rehearsing)
        {
            return _password.Text;
        }

        if (Main.Kakao is not { } kakao)
        {
            return null;
        }

        return _linking ? (_password.Text.Length > 0 ? KakaoSignIn.Link(kakao.Token, _password.Text) : null) : kakao.Token;
    }

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
            // 옛 비밀번호는 한 번 쓰고 버린다 — 이은 뒤로는 표만으로 들어간다.
            _password.Text = string.Empty;

            if (!Rehearsing)
            {
                Main.SetLastCharacter(_username.Text, _autoLoginWanted);
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
            _linking = false;
            ShowState();
        }

        _status.Text = failure.Message;
        RefreshSubmitState(keepStatus: true);
    }

    private Control BuildForm()
    {
        CenterContainer center = new()
        {
            Name = "FormCenter",
            // 가로에서는 굴림 칸 안에 들어가, 폭을 다 쓰라고 하지 않으면 제 최소 폭으로 왼쪽에 붙었다.
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        PanelContainer panel = new()
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? FormWidth : LandscapeFormWidth, 0)
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
        // 비밀번호 칸은 iOS 에 비밀번호라고 알린다(자동 완성 줄의 열쇠). 번호 칸은 숫자 자판.
        _password = Field(LineEdit.VirtualKeyboardTypeEnum.Password);
        _password.Secret = true;
        _code = Field(LineEdit.VirtualKeyboardTypeEnum.Number);
        _code.MaxLength = 6;
        _code.PlaceholderText = "번호 6자리";

        // 엔터(키보드의 완료)는 다음 칸으로, 마지막 칸에서는 들어가기로 — 손가락이 키보드를 떠나지 않아도 된다.
        _username.TextSubmitted += _ =>
        {
            if (_passwordRow.Visible) _password.Edit();
            else SubmitFromKeyboard();
        };
        _password.TextSubmitted += _ => SubmitFromKeyboard();
        _code.TextSubmitted += _ => ConfirmCode();

        _status = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        _status.AddThemeColorOverride("font_color", Greybox.Muted);

        _submit = new Button
        {
            Text = "들어가기",
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
        };

        Greybox.Commit(_submit);
        StyleBoxFlat disabled = Greybox.Sheet();
        disabled.SetCornerRadiusAll(Greybox.Round);
        disabled.SetContentMarginAll(0);
        _submit.AddThemeStyleboxOverride("disabled", disabled);

        _create = PlainButton("새 캐릭터");
        _link = PlainButton("옛 캐릭터 잇기");
        _signOut = PlainButton("카카오 로그아웃");
        _kakao = KakaoButton();

        _confirmCode = new Button
        {
            Text = "확인",
            Disabled = true,
            CustomMinimumSize = new Vector2(88, Main.TouchMinimum)
        };
        Greybox.Commit(_confirmCode);

        _autoLogin = new CheckBox
        {
            ToggleMode = true,
            ButtonPressed = _autoLoginWanted,
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
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

        padding.AddChild(form);
        PanelContainer interior = new();
        StyleBoxFlat surface = Greybox.Sheet();
        surface.SetContentMarginAll(0);
        interior.AddThemeStyleboxOverride("panel", surface);
        interior.AddChild(padding);
        panel.AddChild(interior);
        center.AddChild(panel);

        _username.TextChanged += _ => RefreshSubmitState();
        _password.TextChanged += _ => RefreshSubmitState();
        _code.TextChanged += text => _confirmCode.Disabled = !KakaoSignIn.IsCode(text);
        _submit.Pressed += BeginLogin;
        _create.Pressed += () => WantsToCreate?.Invoke();
        _link.Pressed += ToggleLink;
        _signOut.Pressed += SignOut;
        _kakao.Pressed += StartKakao;
        _confirmCode.Pressed += ConfirmCode;

        if (Main.Portrait)
        {
            return center;
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
        formScroll.AddChild(center);

        return formScroll;
    }

    /// <summary>The established phone flow stays one vertical column.</summary>
    private Control PortraitForm(TextureRect crest, Label title)
    {
        VBoxContainer form = FormColumn();
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
    /// 두 상태의 줄을 모두 쌓고 <see cref="ShowState" /> 가 고른다. 표 없음: [카카오로 로그인] · 번호 줄 · 안내.
    /// 표 있음: 캐릭터 · (옛 비밀번호) · 안내 · [들어가기][새 캐릭터] · [옛 캐릭터 잇기][카카오 로그아웃].
    /// </summary>
    private void AddCredentials(Container form)
    {
        // Captions sit beside their fields rather than above them, so the keyboard never turns each input
        // into two rows. Both orientations use this same compact field treatment.
        form.AddChild(_nameRow = FieldRow("캐릭터", _username));
        form.AddChild(_passwordRow = FieldRow(Rehearsing ? "비밀번호" : "옛 비밀번호", _password));
        form.AddChild(_kakao);

        HBoxContainer code = new();
        code.AddThemeConstantOverride("separation", Main.Gutter);
        Control codeField = FieldRow("번호", _code);
        codeField.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        code.AddChild(codeField);
        code.AddChild(_confirmCode);
        form.AddChild(_codeRow = code);

        form.AddChild(_status);
        form.AddChild(_actions = ButtonRow(_submit, _create));
        form.AddChild(_linkRow = ButtonRow(_link, _signOut));
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
        Button button = new() { Text = text, CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
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
        Button button = new() { Text = "카카오로 로그인", CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };

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
            BeginLogin();
        }
        else if (_username.Text.Length == 0)
        {
            _username.Edit();
        }
    }

    /// <summary>[옛 캐릭터 잇기] — 옛 비밀번호 칸을 열고 닫는다. 닫으면 친 것은 버린다.</summary>
    private void ToggleLink()
    {
        _linking = !_linking;
        _password.Text = string.Empty;
        ShowState();
        RefreshSubmitState();
    }

    /// <summary>[카카오 로그아웃] — 이 기기의 표를 지우고 카카오 단추로.</summary>
    private void SignOut()
    {
        Main.SetKakao(null);
        _linking = false;
        _password.Text = string.Empty;
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
        _kakao.Disabled = true;
        _status.Text = "카카오 로그인을 여는 중…";
        ShowState();

        // 걸린 채 멈추지 않게 — 넘으면 「못 닿음」으로 끝난다.
        HttpRequest ask = new() { Timeout = 10 };
        AddChild(ask);
        ask.RequestCompleted += (result, code, _, body) =>
        {
            ask.QueueFree();
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
            _kakao.Disabled = false;
            _status.Text = "홈페이지에 닿지 못했습니다. 인터넷 연결을 확인해 주세요.";
        }
    }

    /// <summary>2초마다 — 번호를 넣었으면 그 번호와 함께 게임 표를 달라고 묻는다. 한 번에 하나만.</summary>
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

        if (ask.Request(KakaoSignIn.TokenUrl, ["Content-Type: application/json"], Godot.HttpClient.Method.Post, KakaoSignIn.TokenRequest(ticket, _sentCode)) != Godot.Error.Ok)
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
                // 못 닿은 것도 여기 — 번호를 기다리는 안내는 덮지 않는다.
                if (!_codeAsked)
                {
                    _status.Text = BrowserText;
                }

                break;

            case KakaoWait.NeedsCode:
                _codeAsked = true;
                _status.Text = CodeText;
                ShowState();
                break;

            case KakaoWait.WrongCode when reply.Left > 0:
                _sentCode = null;
                _code.Text = string.Empty;
                _confirmCode.Disabled = true;
                _status.Text = reply.Error;
                ShowState();
                break;

            case KakaoWait.WrongCode:
                StartOver($"{reply.Error} [카카오로 로그인]을 다시 눌러 주세요.");
                break;

            case KakaoWait.Pending:
                _status.Text = reply.Name.Length > 0 ? $"{reply.Name} 님 — {PendingText}" : PendingText;
                break;

            case KakaoWait.Granted:
                ForgetTicket();
                Main.SetKakao(new KakaoAccount(reply.Token, reply.Name));
                ShowState();
                RefreshSubmitState();
                _status.Text = $"카카오: {reply.Name} — 캐릭터 이름을 넣고 들어가세요. 처음이면 [새 캐릭터], 예전 캐릭터는 [옛 캐릭터 잇기].";
                break;

            case KakaoWait.Refused:
                StartOver(reply.Error);
                break;

            default:
                StartOver("로그인 표가 없거나 지났습니다. [카카오로 로그인]을 다시 눌러 주세요.");
                break;
        }
    }

    /// <summary>[확인] — 브라우저에 뜬 번호를 다음 물음부터 함께 보낸다(지금 묻는 중이 아니면 바로).</summary>
    private void ConfirmCode()
    {
        if (_ticket is null || !KakaoSignIn.IsCode(_code.Text))
        {
            return;
        }

        _sentCode = _code.Text;
        _code.ReleaseFocus();
        _status.Text = "번호를 확인하는 중…";
        ShowState();
        AskForToken();
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
        _sentCode = null;
        _codeAsked = false;
        _code.Text = string.Empty;
        _confirmCode.Disabled = true;
    }

    /// <summary>
    /// <c>--kakao 기다림|번호|대기|잇기</c> — 홈페이지 없이 그 상태를 그려 본다(<see cref="Main.KakaoPreview" />). 묻지는 않는다.
    /// </summary>
    private void ApplyKakaoPreview()
    {
        switch (Main.KakaoPreview)
        {
            case "기다림":
                _ticket = "미리보기";
                _status.Text = BrowserText;
                break;
            case "번호":
                _ticket = "미리보기";
                _codeAsked = true;
                _status.Text = CodeText;
                break;
            case "대기":
                _ticket = "미리보기";
                _codeAsked = true;
                _sentCode = _code.Text = "123456";
                _confirmCode.Disabled = false;
                _status.Text = $"카카오닉네임 님 — {PendingText}";
                break;
            case "잇기":
                _linking = true;
                break;
            default:
                return;
        }

        ShowState();
    }

    /// <summary>Client version and the font actually in use, which is what tells us Korean will render.</summary>
    private Control BuildVersionLine()
    {
        var notice = Aux("접속·기기/앱 버전·화면/버튼·오류·게임 활동 기록은 운영 확인용으로 90일 보관합니다.");
        notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        notice.CustomMinimumSize = new Vector2(0, 32);
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

        _statusRow.Visible = !typing;
        _versionLine.Visible = !typing;
        _status.Visible = !typing;

        float slide = 0;

        if (typing && TouchInput.Editing(GetViewport()) is { } field && IsAncestorOf(field))
        {
            // 지금 자리는 이미 올린 만큼 올라가 있다 — 올리기 전 자리로 되돌려 잰다.
            Rect2 typed = field.GetGlobalRect();
            Rect2 finish = (SignedIn ? _actions : _codeRow).GetGlobalRect();

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
    }

    public override void _ExitTree()
    {
        Main.LayoutChanged -= Rebuild;
        _closing.Cancel();
        _session?.Dispose();
        _closing.Dispose();
    }

    /// <summary>
    /// No request goes out until it can succeed, per the wireframe transition rules: a name, and a token (or the
    /// rehearsal password), and while linking the old password too.
    /// </summary>
    private void RefreshSubmitState(bool keepStatus = false)
    {
        if (_attempt is not null || _session is not null)
        {
            _submit.Disabled = true;
            return;
        }

        bool ready = _username.Text.Length > 0 && Secret() is { Length: > 0 };

        _submit.Disabled = !ready;

        if (keepStatus || !SignedIn)
        {
            return;
        }

        _status.Text = (Rehearsing, _linking, ready) switch
        {
            (true, _, true) => "로그인할 준비가 됐습니다.",
            (true, _, false) => "사용자명과 비밀번호를 입력하세요.",
            (_, true, true) => "옛 비밀번호가 맞으면 이 캐릭터가 카카오 계정에 이어집니다. 한 번만 하면 됩니다.",
            (_, true, false) => "이을 캐릭터 이름과 그 캐릭터의 옛 비밀번호를 넣으세요.",
            (_, _, true) => "들어갈 준비가 됐습니다.",
            _ => "캐릭터 이름을 넣으세요. 처음이면 [새 캐릭터]."
        };
    }
}
