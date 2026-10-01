using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 캐릭터 만들기 화면, 원작 <c>dlgcre00.png</c> 의 배치를 세로 한 줄로 편다 — 이름·비밀번호·확인,
/// 남/여, HAIR·COLOR 를 눈으로 보고 고르는 격자, 가운데 미리보기, 만들기/취소.
/// </summary>
/// <remarks>
/// 원작의 E-MAIL 칸과 PHRASE MACRO 표는 뺐다 — 서버 프로토콜(<see cref="Hades718LoginProtocol"/> 의
/// <c>CreateAccountRequest</c>)이 이름·비밀번호만 받는다. 원작은 HAIR·COLOR 를 ◀ ▶ 로 숫자를 하나씩
/// 넘기게 했지만, 숫자만 보고는 무슨 모양·무슨 색인지 알 수 없다(사용자, 2026-09-19) — 그래서 여기서는
/// 격자를 깔아 눈으로 보고 누르게 바꿨다. 머리·색은 격자로 고르고, 미리보기 양옆 화살표는 몸의 방향만 바꾼다.
/// </remarks>
public sealed partial class CreateScreen : Control
{
    private const int TitleFontSize = 22;
    private const int AuxFontSize = 14;
    private const int FormWidth = 300;

    private readonly ConcurrentQueue<string> _reported = new();
    private readonly CancellationTokenSource _closing = new();

    private Task<WorldSession>? _attempt;

    /// <summary>취소를 눌렀을 때. Main 이 로그인 화면으로 돌려보낸다.</summary>
    public Action? Cancelled { get; set; }

    private MarginContainer _safeArea = null!;
    private LineEdit _username = null!;
    private LineEdit _password = null!;
    private LineEdit _confirm = null!;
    private Label _status = null!;
    private Button _create = null!;
    private Button _back = null!;

    private Button _male = null!;
    private Button _female = null!;
    private Control _genderRow = null!;

    private Control _pathRow = null!;
    private readonly Dictionary<byte, Button> _pathTiles = new();

    private Control _buttonsRow = null!;

    // 1=남 · 2=여 — 서버가 읽는 값 그대로(ClientFormat04.cs). 머리색은 0~71.
    private byte _gender = 1;
    private int _hairStyle = 1;
    private int _hairColor;
    // 1=전사 · 2=도적 · 3=마법사 · 4=성직자 · 5=무도가. A new character must deliberately pick one;
    // Peasant (0) is never silently persisted by this screen.
    private byte? _path;
    private int _appearanceTab;

    /// <summary>Called on the main thread when creation has already completed the normal login into the world.</summary>
    public Action<WorldSession>? Entered { get; set; }

    public CreateScreen()
    {
        Name = "CreateScreen";
        AnchorRight = 1;
        AnchorBottom = 1;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    /// <summary>레이아웃 검사가 훑는 자리들.</summary>
    public IReadOnlyList<(string Name, Control Part)> Parts =>
    [
        ("이름", _username), ("비밀번호", _password), ("비밀번호 확인", _confirm),
        ("성별", _genderRow), ("직업", _pathRow), ("미리보기", _preview), ("머리", _hairRow), ("색", _colorRow),
        ("단추 줄", _buttonsRow)
    ];

    public override void _Ready()
    {
        AddChild(Greybox.EntryBackground());
        _safeArea = Main.SafeAreaContainer();
        AddChild(_safeArea);

        _safeArea.AddChild(BuildForm());

        ApplyPickedLook();
        ApplyPickedPath();
        ApplyRehearsal();
        RefreshGender();
        RefreshPathSelection();
        PopulateHairGrid();
        RefreshColorSelection();
        RefreshPreview();
        RefreshCreateState();
        Main.LayoutChanged += RebuildForOrientation;

        if (Main.CreateNow)
        {
            BeginCreate();
        }
    }

    /// <summary>Exchange the row/column shell after rotation without losing entered credentials or look.</summary>
    private void RebuildForOrientation()
    {
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(_safeArea)) return;
        string username = _username.Text, password = _password.Text, confirm = _confirm.Text, status = _status.Text;
        foreach (Node child in _safeArea.GetChildren())
        {
            _safeArea.RemoveChild(child);
            child.QueueFree();
        }
        _safeArea.AddChild(BuildForm());
        _username.Text = username;
        _password.Text = password;
        _confirm.Text = confirm;
        _status.Text = status;
        RefreshGender();
        RefreshPathSelection();
        PopulateHairGrid();
        RefreshColorSelection();
        RefreshPreview();
        RefreshCreateState();
    }

    /// <summary>
    /// 손 없이 확인할 때 미리 고른 성별·머리·색을 그대로 받는다(<c>--pick-look</c>). 그 성별에 없는
    /// 머리 번호가 왔으면 가장 가까운 번호로 옮긴다 — 사람이 고르다 성별을 바꾼 것과 같은 자리다.
    /// </summary>
    private void ApplyPickedLook()
    {
        if (Main.PickedLook is not { } look)
        {
            return;
        }

        _gender = look.Gender;
        _hairStyle = HairStyles.ClosestFor(look.HairStyle, _gender);
        _hairColor = look.HairColor;
    }

    /// <summary>
    /// 손 없이 확인할 때 이름·비밀번호도 미리 채운다 — 새 인자를 만들지 않고 로그인 화면과 같은
    /// <c>--login 이름:비밀번호</c>(<see cref="Main.Rehearsal"/>)를 그대로 쓴다. 계정 만들기도 이름·
    /// 비밀번호가 필요하다는 점은 로그인과 같다.
    /// </summary>
    private void ApplyRehearsal()
    {
        if (Main.Rehearsal.Username.Length == 0)
        {
            return;
        }

        _username.Text = Main.Rehearsal.Username;
        _password.Text = Main.Rehearsal.Password;
        _confirm.Text = Main.Rehearsal.Password;
    }

    private Control BuildForm()
    {
        PanelContainer panel = new()
        {
            // 세로는 원래의 300px 한 열을 그대로 쓴다. 짧은 가로 화면은 그 한 열을 억지로 줄이지
            // 않고, 안전영역 전체를 세 칸(입력 | 미리보기 | 꾸미기)에 준다.
            CustomMinimumSize = new Vector2(Main.Portrait ? FormWidth : 0, 0),
            SizeFlagsHorizontal = Main.Portrait ? SizeFlags.ShrinkCenter : SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", Greybox.Stone());

        MarginContainer padding = new();
        padding.AddThemeConstantOverride("margin_left", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_top", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_right", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_bottom", Main.Gutter * 2);

        Label title = new()
        {
            Text = "캐릭터 만들기",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true
        };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        title.AddThemeColorOverride("font_color", Greybox.Title);
        HBoxContainer heading = new();
        heading.AddThemeConstantOverride("separation", Main.Gutter);
        heading.AddChild(new TextureRect
        {
            Texture = new AtlasTexture
            {
                Atlas = GD.Load<Texture2D>("res://assets/ui/create-wizard-cutout.png"),
                Region = new Rect2(190, 45, 925, 1095)
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            CustomMinimumSize = new Vector2(64, Main.Portrait ? 64 : 48),
            MouseFilter = MouseFilterEnum.Ignore
        });
        heading.AddChild(title);
        _title = heading;

        _username = Field(secret: false, placeholder: "이름");
        _password = Field(secret: true, placeholder: "비밀번호");
        _confirm = Field(secret: true, placeholder: "확인");

        // 엔터(키보드의 완료)는 이름 → 비밀번호 → 확인 → 만들기(아직 못 만들면 키보드만 내린다).
        _username.TextSubmitted += _ => _password.Edit();
        _password.TextSubmitted += _ => _confirm.Edit();
        _confirm.TextSubmitted += _ => SubmitFromKeyboard();

        BoxContainer authRow = Main.Portrait ? new HBoxContainer() : new VBoxContainer();
        authRow.AddThemeConstantOverride("separation", Main.Gutter);
        if (!Main.Portrait)
            foreach (LineEdit field in new[] { _username, _password, _confirm }) field.PlaceholderText = string.Empty;
        authRow.AddChild(Main.Portrait ? _username : LabelledField("이름", _username));
        authRow.AddChild(Main.Portrait ? _password : LabelledField("비밀번호", _password));
        authRow.AddChild(Main.Portrait ? _confirm : LabelledField("확인", _confirm));

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        _status.AddThemeColorOverride("font_color", Greybox.Muted);

        _create = new Button { Text = "만들기", CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Greybox.Commit(_create);

        _back = new Button { Text = "취소", CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StyleButton(_back);

        HBoxContainer buttons = new();
        buttons.AddThemeConstantOverride("separation", Main.Gutter);
        buttons.AddChild(_back);
        buttons.AddChild(_create);
        _buttonsRow = buttons;

        Control preview = BuildPreview();
        Control gender = BuildGenderRow();
        Control path = BuildPathRow();
        Control hair = BuildHairGrid();
        Control color = BuildColorGrid();

        Control form = Main.Portrait
            ? BuildPortraitForm(heading, authRow, preview, gender, path, hair, color, buttons)
            : BuildLandscapeForm(heading, authRow, preview, gender, path, hair, color, buttons);

        padding.AddChild(form);
        PanelContainer interior = new();
        StyleBoxFlat surface = Greybox.Sheet();
        surface.SetContentMarginAll(0);
        interior.AddThemeStyleboxOverride("panel", surface);
        interior.AddChild(padding);
        panel.AddChild(interior);

        _username.TextChanged += _ => RefreshCreateState();
        _password.TextChanged += _ => RefreshCreateState();
        _confirm.TextChanged += _ => RefreshCreateState();
        _back.Pressed += () => Cancelled?.Invoke();
        _create.Pressed += BeginCreate;

        return panel;
    }

    /// <summary>기존 세로 순서. 가로 전용 변경이 긴 세로 화면의 정보 흐름을 바꾸지 않게 따로 둔다.</summary>
    private Control BuildPortraitForm(
        Control title, Control auth, Control preview, Control gender, Control path, Control hair, Control color, Control buttons)
    {
        VBoxContainer form = Column();
        form.AddChild(title);
        form.AddChild(auth);
        form.AddChild(preview);
        form.AddChild(gender);
        form.AddChild(path);
        form.AddChild(hair);
        form.AddChild(color);
        form.AddChild(_status);
        form.AddChild(buttons);
        return form;
    }

    /// <summary>원작의 세로 입력 칸, 모바일의 큰 인물·아래 성별·오른쪽 아래 완료 구조.</summary>
    private Control BuildLandscapeForm(
        Control title, Control auth, Control preview, Control gender, Control path, Control hair, Control color, Control buttons)
    {
        VBoxContainer form = Column();
        form.AddChild(title);

        HBoxContainer content = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", Main.Gutter * 2);
        auth.CustomMinimumSize = new Vector2(208, 0);
        auth.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        auth.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        content.AddChild(auth);

        VBoxContainer character = Column();
        character.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        character.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        character.AddChild(preview);
        character.AddChild(gender);
        content.AddChild(character);

        VBoxContainer appearance = Column();
        appearance.CustomMinimumSize = new Vector2(160, 0);
        appearance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HBoxContainer tabs = new();
        tabs.AddThemeConstantOverride("separation", 0);
        Control[] sections = [path, hair, color];
        List<Button> selectors = [];
        foreach ((int index, string label) in new[] { (0, "직업"), (1, "머리"), (2, "색") })
        {
            Button tab = new()
            {
                Name = $"AppearanceTab{index}", Text = label, ToggleMode = true,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            StyleButton(tab);
            foreach (string state in new[] { "normal", "hover", "focus", "pressed", "hover_pressed" })
            {
                bool selected = state is "pressed" or "hover_pressed";
                StyleBoxFlat underline = Greybox.Sheet();
                underline.SetBorderWidthAll(0);
                underline.BorderWidthBottom = selected ? 3 : 1;
                if (selected) underline.BorderColor = Greybox.Title;
                underline.SetContentMarginAll(4);
                tab.AddThemeStyleboxOverride(state, underline);
            }
            tab.AddThemeColorOverride("font_color", Greybox.Muted);
            tab.AddThemeColorOverride("font_pressed_color", Greybox.Text);
            tab.Pressed += () =>
            {
                _appearanceTab = index;
                for (int i = 0; i < sections.Length; i++)
                {
                    sections[i].Visible = i == index;
                    selectors[i].ButtonPressed = i == index;
                }
            };
            selectors.Add(tab);
            tabs.AddChild(tab);
        }
        appearance.AddChild(tabs);
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i].Visible = i == _appearanceTab;
            selectors[i].ButtonPressed = i == _appearanceTab;
            appearance.AddChild(sections[i]);
        }
        content.AddChild(appearance);
        form.AddChild(content);

        // 하단 위치는 입력·꾸밈 탭과 무관하게 고정한다.
        _back.CustomMinimumSize = new Vector2(88, Main.TouchMinimum);
        _back.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _create.CustomMinimumSize = new Vector2(144, Main.TouchMinimum);
        _create.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.CustomMinimumSize = new Vector2(0, Main.TouchMinimum);
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.MaxLinesVisible = 2;
        _status.ClipText = true;
        buttons.AddChild(_status);
        buttons.MoveChild(_status, 1);
        form.AddChild(buttons);
        return form;
    }

    private static Control LabelledField(string label, LineEdit field)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);
        Label caption = Aux(label);
        caption.CustomMinimumSize = new Vector2(64, 0);
        caption.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(caption);
        row.AddChild(field);
        return row;
    }

    /// <summary>화면의 같은 세로 묶음이 쓰는 간격 — 기존 세로 폼의 값과 같다.</summary>
    private static VBoxContainer Column()
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", Main.Gutter / 2);
        return column;
    }

    private Control BuildGenderRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        _male = new Button { Text = "남", ToggleMode = true, CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _female = new Button { Text = "여", ToggleMode = true, CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StyleButton(_male);
        StyleButton(_female);

        _male.Pressed += () => SelectGender(1);
        _female.Pressed += () => SelectGender(2);

        row.AddChild(_male);
        row.AddChild(_female);

        _genderRow = row;
        return row;
    }

    /// <summary>
    /// All five classes remain visible: one row upright, two rows on a short landscape phone.
    /// </summary>
    private Control BuildPathRow()
    {
        GridContainer choices = new() { Columns = Main.Portrait ? 5 : 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        choices.AddThemeConstantOverride("h_separation", Main.Gutter);
        choices.AddThemeConstantOverride("v_separation", Main.Gutter);

        foreach ((byte path, string label) in new[]
        {
            ((byte)1, "전사"), ((byte)2, "도적"), ((byte)3, "법사"), ((byte)4, "사제"), ((byte)5, "무도")
        })
        {
            Button tile = new()
            {
                Text = label,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            StyleButton(tile);
            tile.Pressed += () => SelectPath(path);
            _pathTiles[path] = tile;
            choices.AddChild(tile);
        }

        _pathRow = choices;
        return choices;
    }

    private void ApplyPickedPath()
    {
        if (Main.PickedPath is byte path && path is >= 1 and <= 5)
        {
            _path = path;
        }
    }

    private void SelectPath(byte path)
    {
        _path = path;
        RefreshPathSelection();
        RefreshPreview();
        RefreshCreateState();
    }

    private void RefreshPathSelection()
    {
        foreach ((byte path, Button tile) in _pathTiles)
        {
            tile.ButtonPressed = _path == path;
        }
    }

    /// <summary>
    /// 성별을 바꾼다. 지금 고른 머리 번호가 새 성별에 없으면(18·32·33 은 남자 전용) 가장 가까운
    /// 번호로 스스로 옮긴다 — 묻지도, 경고하지도 않는다(2026-09-19 결정). HAIR 격자도 새 성별의
    /// 목록으로 다시 짓는다.
    /// </summary>
    private void SelectGender(byte gender)
    {
        if (_gender == gender)
        {
            return;
        }

        _gender = gender;
        _hairStyle = HairStyles.ClosestFor(_hairStyle, gender);

        RefreshGender();
        PopulateHairGrid();
        RefreshPreview();
    }

    private void RefreshGender()
    {
        _male.ButtonPressed = _gender == 1;
        _female.ButtonPressed = _gender == 2;
    }

    /// <summary>
    /// 계정을 만들고, 같은 연결에서 지금 고른 성별·머리·색으로 캐릭터를 만든다
    /// (<see cref="HadesLoginClient.CreateCharacterAsync"/>). 로그인 화면과 같은 말투로 진행 상황을
    /// 큐에 쌓고 <see cref="_Process"/> 에서 읽는다 — Godot 노드는 메인 스레드에서만 건드릴 수 있다.
    /// </summary>
    private void BeginCreate()
    {
        if (_attempt is not null)
        {
            return;
        }

        _create.Disabled = true;
        _status.Text = "만드는 중…";

        _attempt = HadesLoginClient.CreateCharacterAsync(
            Main.ServerAddress,
            Main.ServerPort,
            _username.Text,
            _password.Text,
            (byte)_hairStyle,
            _gender,
            (byte)_hairColor,
            _path!.Value,
            new Progress<string>(_reported.Enqueue),
            _closing.Token);
    }

    private void DrainCreate()
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
            // The submitted secret lives only in the current attempt/field. The screen is removed as the
            // live world takes ownership of its session, so it is not retained for a later login.
            _password.Text = string.Empty;
            _confirm.Text = string.Empty;
            _status.Text = "노비스마을에 들어왔습니다.";
            WorldSession session = finished.Result;
            Entered?.Invoke(session);
            return;
        }

        Exception failure = finished.Exception?.GetBaseException() ?? new IOException("알 수 없는 오류");

        _status.Text = failure.Message;
        _create.Disabled = false;
    }

    /// <summary>
    /// 이름·비밀번호·확인 칸 하나. 옆에 따로 캡션을 두지 않고 칸 안 흐린 힌트 글자
    /// (<c>PlaceholderText</c>)로 무슨 칸인지 알린다 — 몸 미리보기를 가운데·크게 두려고 세 칸을
    /// 세로로 쌓지 않고 한 줄에 나란히 두기로 하면서(사용자, 2026-09-19), 옆 캡션까지 있으면 칸이
    /// 너무 좁아져 뺐다. 높이는 그대로 <see cref="Main.TouchMinimum"/> — 터치 최소보다 낮추지
    /// 않는다.
    /// </summary>
    private static LineEdit Field(bool secret, string placeholder)
    {
        LineEdit field = new()
        {
            Secret = secret,
            PlaceholderText = placeholder,
            VirtualKeyboardType = secret ? LineEdit.VirtualKeyboardTypeEnum.Password : LineEdit.VirtualKeyboardTypeEnum.Default,
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        foreach (string state in new[] { "normal", "focus", "read_only" })
        {
            StyleBoxFlat background = Greybox.Sheet();
            background.SetContentMarginAll(4);
            background.SetCornerRadiusAll(8);
            if (state == "focus") background.BorderColor = Greybox.Muted;
            field.AddThemeStyleboxOverride(state, background);
        }
        return field;
    }

    // 로그인과 같은 단색 바탕. 선택은 돌 타일 대신 테두리와 글자 밝기로 표시한다.
    private static void StyleButton(Button button)
    {
        Greybox.Plain(button);
        foreach (string state in new[] { "normal", "hover", "focus", "pressed", "hover_pressed", "disabled" })
        {
            StyleBoxFlat background = Greybox.Sheet();
            background.SetContentMarginAll(4);
            background.SetCornerRadiusAll(8);
            if (state is "pressed" or "hover_pressed" or "focus") background.BorderColor = Greybox.Muted;
            button.AddThemeStyleboxOverride(state, background);
        }
        button.AddThemeColorOverride("font_disabled_color", Greybox.Muted);
        button.AddThemeColorOverride("font_hover_pressed_color", Greybox.Text);
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }

    /// <summary>이름과 비밀번호(확인 포함)가 다 있어야 만들 수 있다 — 로그인 화면과 같은 규칙.</summary>
    private void RefreshCreateState()
    {
        if (_attempt is not null)
        {
            return;
        }

        bool ready = _username.Text.Length > 0 && _password.Text.Length > 0 && _password.Text == _confirm.Text && _path is not null;

        _create.Disabled = !ready;
        _status.Text = ready
            ? "직업과 꾸밈을 확인한 뒤 만들 수 있습니다."
            : _path is null ? "직업을 하나 고르세요." : "이름과 비밀번호(확인 포함)를 입력하세요.";
    }

    /// <summary>키보드의 엔터를 마지막 칸에서 누르면 — 만들 수 있으면 만들고, 아니면 키보드만 내려 직업·꾸밈을 보인다.</summary>
    private void SubmitFromKeyboard()
    {
        if (!_create.Disabled)
        {
            BeginCreate();
        }
    }

    // 키보드 때문에 화면을 올린 만큼, 그리고 그동안 접어 두는 제목(칸이 맨 위로 올라가면 반쯤 잘려 보였다).
    private float _slide;
    private Control _title = null!;

    /// <summary>
    /// 로그인 화면과 같은 방식으로 키보드를 피한다 — 화면 전체를 줄이지 않고, 치고 있는 칸(자리가 되면 [만들기]까지)이
    /// 키보드 위에 오도록 그만큼만 올린다(<see cref="KeyboardFit.Slide"/>). 예전에는 화면을 키보드만큼 줄여 격자·미리보기가
    /// 찌그러졌다.
    /// </summary>
    public override void _Process(double delta)
    {
        float covered = TouchInput.Covered;
        float slide = 0;

        // 세로 한 열은 칸이 이미 맨 위 가까이에 있어 제목을 접어도 얻는 것이 없다 — 가로만 접는다.
        _title.Visible = covered <= 0 || Main.Portrait;

        if (covered > 0 && TouchInput.Editing(GetViewport()) is { } field && IsAncestorOf(field))
        {
            Rect2 typed = field.GetGlobalRect();
            Rect2 finish = _buttonsRow.GetGlobalRect();

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

        DrainCreate();
    }

    public override void _ExitTree()
    {
        Main.LayoutChanged -= RebuildForOrientation;
        _closing.Cancel();
        _closing.Dispose();
    }
}
