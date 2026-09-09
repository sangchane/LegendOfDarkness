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
        MarginContainer safeArea = Main.SafeAreaContainer();
        AddChild(safeArea);

        safeArea.AddChild(BuildStatusRow());
        safeArea.AddChild(BuildForm());
        safeArea.AddChild(BuildVersionLine());

        RefreshSubmitState();
    }

    /// <summary>Environment on the left, server reachability on the right, both along the top edge.</summary>
    private static Control BuildStatusRow()
    {
        HBoxContainer row = new()
        {
            Name = "StatusRow",
            AnchorRight = 1,
            GrowHorizontal = GrowDirection.Both
        };

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
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
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

        form.AddChild(title);
        form.AddChild(Aux("사용자명"));
        form.AddChild(_username);
        form.AddChild(Aux("비밀번호"));
        form.AddChild(_password);
        form.AddChild(_status);
        form.AddChild(_submit);

        padding.AddChild(form);
        panel.AddChild(padding);
        center.AddChild(panel);

        _username.TextChanged += _ => RefreshSubmitState();
        _password.TextChanged += _ => RefreshSubmitState();

        return center;
    }

    private static LineEdit Field(bool secret) => new()
    {
        Secret = secret,
        CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
    };

    /// <summary>Client version and the font actually in use, which is what tells us Korean will render.</summary>
    private Control BuildVersionLine()
    {
        VBoxContainer bottom = new()
        {
            Name = "VersionLine",
            AnchorTop = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.End,
            GrowVertical = GrowDirection.Begin
        };

        bottom.AddChild(Aux($"클라이언트 0.1 greybox · 글꼴 {Main.FontName}"));

        return bottom;
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }

    /// <summary>No request goes out until both fields carry something, per the wireframe transition rules.</summary>
    private void RefreshSubmitState()
    {
        bool ready = _username.Text.Length > 0 && _password.Text.Length > 0;

        _submit.Disabled = !ready;
        _status.Text = ready ? "로그인할 준비가 됐습니다." : "사용자명과 비밀번호를 입력하세요.";
    }
}
