using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Godot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// Login greybox, built to section 3 of the wireframes. Our own labels are Korean and the server's own
/// messages are left as they arrive, which is decision 1 in that document.
/// </summary>
public partial class LoginScreen : Control
{
    private const int TitleFontSize = 22;

    /// <summary>원작 문장. 그림 한 장이면 충분하다.</summary>
    private const string Crest = "res://assets/ui/crest.png";

    private const int CrestHeight = 68;
    private const int AuxFontSize = 14;
    private const int FormWidth = 300;
    private const int LandscapeFormWidth = 560;
    private const int CaptionWidth = 72;

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
    private LineEdit _password = null!;
    private Button _submit = null!;
    private Button _create = null!;
    private ScrollContainer? _formScroll;

    /// <summary>Launch-time rehearsal may submit once; a screen reached by explicit logout only fills the fields.</summary>
    public bool AutomaticLogin { get; init; } = true;

    public LoginScreen()
    {
        Name = "LoginScreen";
        AnchorRight = 1;
        AnchorBottom = 1;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    public override void _Ready()
    {
        _safeArea = Main.SafeAreaContainer();
        AddChild(_safeArea);

        // One child per container: a MarginContainer gives every child its whole rect, so three siblings
        // would sit on top of one another instead of at the top, middle and bottom.
        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);
        _safeArea.AddChild(rows);

        rows.AddChild(BuildStatusRow());
        rows.AddChild(BuildForm());
        rows.AddChild(BuildVersionLine());

        RefreshSubmitState();

        if (Main.Rehearsal.Username.Length > 0)
        {
            _username.Text = Main.Rehearsal.Username;
            _password.Text = Main.Rehearsal.Password;

            if (AutomaticLogin)
            {
                BeginLogin();
            }
        }
    }

    /// <summary>Environment on the left, which server we will talk to on the right.</summary>
    private static Control BuildStatusRow()
    {
        HBoxContainer row = new() { Name = "StatusRow" };

        row.AddChild(Aux("테스트 환경 · 로컬"));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(Aux($"서버 {Main.ServerAddress}:{Main.ServerPort}"));

        return row;
    }

    /// <summary>
    /// Starts the login and leaves it running. Nothing here touches the tree from the task: progress lands
    /// in a queue and _Process drains it, because a Godot node may only be changed on the main thread.
    /// </summary>
    private void BeginLogin()
    {
        if (_attempt is not null)
        {
            return;
        }

        _submit.Disabled = true;
        _status.Text = "접속하는 중…";

        _attempt = HadesLoginClient.LoginAsync(
            Main.ServerAddress,
            Main.ServerPort,
            _username.Text,
            _password.Text,
            new Progress<string>(_reported.Enqueue),
            _closing.Token);
    }

    /// <summary>Reports what the login is doing, and what became of it.</summary>
    private void DrainLogin()
    {
        while (_reported.TryDequeue(out string? line))
        {
            _status.Text = line;
        }

        if (_attempt is null || !_attempt.IsCompleted)
        {
            return;
        }

        Task<WorldSession> finished = _attempt;
        _attempt = null;

        if (finished.IsCompletedSuccessfully)
        {
            _session = finished.Result;
            _status.Text = $"{_session.Character.CharacterName} 님, 월드에 들어왔습니다.";
            _submit.Text = "접속됨";

            // The world takes the connection from here, so this screen must not close it.
            WorldSession handed = _session;
            _session = null;
            Entered?.Invoke(handed);

            return;
        }

        // The server's own words when it has them, ours when the socket failed before it could speak.
        Exception failure = finished.Exception?.GetBaseException() ?? new IOException("알 수 없는 오류");

        _status.Text = failure.Message;
        _submit.Disabled = false;
    }

    private Control BuildForm()
    {
        CenterContainer center = new()
        {
            Name = "FormCenter",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        PanelContainer panel = new()
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? FormWidth : LandscapeFormWidth, 0)
        };
        panel.AddThemeStyleboxOverride("panel", Greybox.Surface());

        MarginContainer padding = new();
        padding.AddThemeConstantOverride("margin_left", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_top", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_right", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_bottom", Main.Gutter * 2);

        // 로고 하나와 이름뿐이다 — 돌 무늬는 쓰지 않는다(사용자, 2026-09-18). 처음 보는 화면이라
        // 무엇을 하는 곳인지만 분명하면 된다.
        TextureRect crest = new()
        {
            Texture = GD.Load<Texture2D>(Crest),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            CustomMinimumSize = new Vector2(0, CrestHeight)
        };

        Label title = new()
        {
            Text = "어둠의 전설",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        title.AddThemeColorOverride("font_color", Greybox.Title);

        _username = Field(secret: false);
        _password = Field(secret: true);

        _status = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        _status.AddThemeColorOverride("font_color", Greybox.Muted);

        _submit = new Button
        {
            Text = "로그인",
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
        };

        _create = new Button
        {
            Text = "계정 만들기",
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
        };
        Greybox.Plain(_create);

        Control form = Main.Portrait
            ? PortraitForm(crest, title)
            : LandscapeForm(crest, title);

        padding.AddChild(form);
        panel.AddChild(padding);
        center.AddChild(panel);

        _username.TextChanged += _ => RefreshSubmitState();
        _password.TextChanged += _ => RefreshSubmitState();
        _submit.Pressed += BeginLogin;
        _create.Pressed += () => WantsToCreate?.Invoke();

        if (Main.Portrait)
        {
            return center;
        }

        // A landscape keyboard can leave less height than four 48px controls need. Keep the controls at
        // their accessible size and scroll the focused field into view instead of shrinking or clipping them.
        _formScroll = new ScrollContainer
        {
            Name = "FormScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _formScroll.AddChild(center);

        return _formScroll;
    }

    /// <summary>The established phone flow stays one vertical column.</summary>
    private Control PortraitForm(TextureRect crest, Label title)
    {
        VBoxContainer form = FormColumn();
        form.AddChild(crest);
        form.AddChild(title);
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
        identity.AddChild(crest);
        identity.AddChild(title);

        VBoxContainer credentials = FormColumn();
        credentials.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        credentials.SizeFlagsStretchRatio = 3;
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

    private void AddCredentials(Container form)
    {
        // Captions sit beside their fields rather than above them, so the keyboard never turns each input
        // into two rows. Both orientations use this same compact field treatment.
        form.AddChild(FieldRow("사용자명", _username));
        form.AddChild(FieldRow("비밀번호", _password));
        form.AddChild(_status);
        form.AddChild(_submit);
        form.AddChild(_create);
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

    private static LineEdit Field(bool secret) => new()
    {
        Secret = secret,
        CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
    };

    /// <summary>Client version and the font actually in use, which is what tells us Korean will render.</summary>
    private Control BuildVersionLine()
    {
        return Aux($"클라이언트 0.1 greybox · 글꼴 {Main.FontName}");
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }

    /// <summary>
    /// Keeps the field being typed into and the login button above the on-screen keyboard, which the
    /// wireframes require. Shrinking the area the form centres in lifts it by exactly what the keyboard takes.
    /// </summary>
    public override void _Process(double delta)
    {
        int keyboard = DisplayServer.VirtualKeyboardGetHeight();
        Vector2I screen = DisplayServer.ScreenGetSize();

        int lift = keyboard > 0 && screen.Y > 0
            ? Mathf.RoundToInt(keyboard / (float)screen.Y * GetViewportRect().Size.Y)
            : 0;

        _safeArea.AddThemeConstantOverride("margin_bottom", Main.SafeInsets.Bottom + lift);

        if (_formScroll is not null && GetViewport().GuiGetFocusOwner() is Control focused
            && _formScroll.IsAncestorOf(focused))
        {
            _formScroll.EnsureControlVisible(focused);
        }

        DrainLogin();
    }

    public override void _ExitTree()
    {
        _closing.Cancel();
        _session?.Dispose();
        _closing.Dispose();
    }

    /// <summary>No request goes out until both fields carry something, per the wireframe transition rules.</summary>
    private void RefreshSubmitState()
    {
        if (_attempt is not null || _session is not null)
        {
            return;
        }

        bool ready = _username.Text.Length > 0 && _password.Text.Length > 0;

        _submit.Disabled = !ready;
        _status.Text = ready ? "로그인할 준비가 됐습니다." : "사용자명과 비밀번호를 입력하세요.";
    }
}
