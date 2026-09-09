using Godot;

namespace LodClient;

/// <summary>
/// Login greybox, built to section 3 of the wireframes. Our own labels are Korean and the server's own
/// messages are left as they arrive, which is decision 1 in that document.
/// </summary>
public partial class LoginScreen : Control
{
    private const int TitleFontSize = 22;
    private const int AuxFontSize = 14;
    private const int FormWidth = 300;
    private const int CaptionWidth = 72;

    private MarginContainer _safeArea = null!;
    private Label _status = null!;
    private LineEdit _username = null!;
    private LineEdit _password = null!;
    private Button _submit = null!;

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
    }

    /// <summary>Environment on the left, server reachability on the right, both along the top edge.</summary>
    private static Control BuildStatusRow()
    {
        HBoxContainer row = new() { Name = "StatusRow" };

        row.AddChild(Aux("테스트 환경 · 로컬"));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(Aux("서버 상태: 연결 가능"));

        return row;
    }

    private Control BuildForm()
    {
        CenterContainer center = new()
        {
            Name = "FormCenter",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        PanelContainer panel = new() { CustomMinimumSize = new Vector2(FormWidth, 0) };
        panel.AddThemeStyleboxOverride("panel", Greybox.Surface());

        MarginContainer padding = new();
        padding.AddThemeConstantOverride("margin_left", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_top", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_right", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_bottom", Main.Gutter * 2);

        VBoxContainer form = new();
        form.AddThemeConstantOverride("separation", Main.Gutter);

        Label title = new()
        {
            Text = "어둠의 전설",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);

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

        // Captions sit beside their fields rather than above them: in landscape the form has little height
        // to spare, and stacked captions pushed it into the space the on-screen keyboard takes.
        form.AddChild(title);
        form.AddChild(FieldRow("사용자명", _username));
        form.AddChild(FieldRow("비밀번호", _password));
        form.AddChild(_status);
        form.AddChild(_submit);

        padding.AddChild(form);
        panel.AddChild(padding);
        center.AddChild(panel);

        _username.TextChanged += _ => RefreshSubmitState();
        _password.TextChanged += _ => RefreshSubmitState();

        return center;
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
    }

    /// <summary>No request goes out until both fields carry something, per the wireframe transition rules.</summary>
    private void RefreshSubmitState()
    {
        bool ready = _username.Text.Length > 0 && _password.Text.Length > 0;

        _submit.Disabled = !ready;
        _status.Text = ready ? "로그인할 준비가 됐습니다." : "사용자명과 비밀번호를 입력하세요.";
    }
}
