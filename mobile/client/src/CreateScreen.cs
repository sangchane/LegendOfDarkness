using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 캐릭터 만들기 화면, 원작 <c>dlgcre00.png</c> 의 배치를 세로 한 줄로 편다 — 이름·비밀번호·확인,
/// 남/여, HAIR·COLOR 를 ◀ ▶ 로 하나씩 넘기는 것, 가운데 미리보기, 만들기/취소.
/// </summary>
/// <remarks>
/// 원작의 E-MAIL 칸과 PHRASE MACRO 표는 뺐다 — 서버 프로토콜(<see cref="Hades718LoginProtocol"/> 의
/// <c>CreateAccountRequest</c>)이 이름·비밀번호만 받는다. 미리보기는 아직 걷기 그림 그대로다 —
/// 머리·색을 입히는 것은 Task C 몫이라 여기서는 자리만 잡는다(plans/character-creation.md).
/// </remarks>
public sealed partial class CreateScreen : Control
{
    private const int TitleFontSize = 22;
    private const int AuxFontSize = 14;
    private const int FormWidth = 300;
    private const int CaptionWidth = 72;
    private const int ValueWidth = 40;
    private const int PreviewSize = 140;

    /// <summary>맨몸·머리 그림이 하나도 없을 때만 쓰는 마지막 대안(있을 리 없음).</summary>
    private const string HeroSheet = "res://assets/actor/hero-walk.png";

    /// <summary>겹쳐 입힐 그림들이 있는 자리 — <c>WorldView.Dress</c> 와 같은 값(부위별로 따로 못 나눈다).</summary>
    private const string PartsFolder = "res://assets/actor/parts/";

    private readonly ConcurrentQueue<string> _reported = new();
    private readonly CancellationTokenSource _closing = new();

    private Task? _attempt;

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

    private Control _preview = null!;
    private Control _stage = null!;

    private Label _hairValue = null!;
    private Control _hairRow = null!;

    private Label _colorValue = null!;
    private Control _colorRow = null!;

    private Control _buttonsRow = null!;

    // 1=남 · 2=여 — 서버가 읽는 값 그대로(ClientFormat04.cs). 머리색은 0~71.
    private byte _gender = 1;
    private int _hairStyle = 1;
    private int _hairColor;

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
        ("성별", _genderRow), ("미리보기", _preview), ("머리", _hairRow), ("색", _colorRow),
        ("단추 줄", _buttonsRow)
    ];

    public override void _Ready()
    {
        _safeArea = Main.SafeAreaContainer();
        AddChild(_safeArea);

        VBoxContainer rows = new();
        rows.AddThemeConstantOverride("separation", Main.Gutter);
        _safeArea.AddChild(rows);

        rows.AddChild(BuildForm());

        ApplyPickedLook();
        RefreshGender();
        RefreshHair();
        RefreshColor();
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
            Text = "캐릭터 만들기",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        title.AddThemeColorOverride("font_color", Greybox.Title);

        _username = Field(secret: false);
        _password = Field(secret: true);
        _confirm = Field(secret: true);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", AuxFontSize);
        _status.AddThemeColorOverride("font_color", Greybox.Muted);

        _create = new Button { Text = "만들기", CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Greybox.Commit(_create);

        _back = new Button { Text = "취소", CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Greybox.Plain(_back);

        HBoxContainer buttons = new();
        buttons.AddThemeConstantOverride("separation", Main.Gutter);
        buttons.AddChild(_back);
        buttons.AddChild(_create);
        _buttonsRow = buttons;

        form.AddChild(title);
        form.AddChild(FieldRow("이름", _username));
        form.AddChild(FieldRow("비밀번호", _password));
        form.AddChild(FieldRow("확인", _confirm));
        form.AddChild(BuildGenderRow());
        form.AddChild(BuildPreview());
        form.AddChild(BuildHairRow());
        form.AddChild(BuildColorRow());
        form.AddChild(_status);
        form.AddChild(buttons);

        padding.AddChild(form);
        panel.AddChild(padding);
        center.AddChild(panel);

        _username.TextChanged += _ => RefreshCreateState();
        _password.TextChanged += _ => RefreshCreateState();
        _confirm.TextChanged += _ => RefreshCreateState();
        _back.Pressed += () => Cancelled?.Invoke();
        _create.Pressed += BeginCreate;

        return center;
    }

    private Control BuildGenderRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        _male = new Button { Text = "남", ToggleMode = true, CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _female = new Button { Text = "여", ToggleMode = true, CustomMinimumSize = new Vector2(0, Main.TouchMinimum), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Greybox.Tab(_male);
        Greybox.Tab(_female);

        _male.Pressed += () => SelectGender(1);
        _female.Pressed += () => SelectGender(2);

        row.AddChild(_male);
        row.AddChild(_female);

        _genderRow = row;
        return row;
    }

    /// <summary>
    /// 가운데 미리보기 자리. 맨몸에 고른 머리·색만 얹는다 — 옷·모자·신발은 없다(사용자, 2026-09-19).
    /// 실제 그림은 <see cref="RefreshPreview"/> 가 채운다.
    /// </summary>
    private Control BuildPreview()
    {
        PanelContainer box = new() { CustomMinimumSize = new Vector2(PreviewSize, PreviewSize) };
        box.AddThemeStyleboxOverride("panel", Greybox.Surface());

        _stage = new Control { ClipContents = true };

        box.AddChild(_stage);
        _preview = box;
        return box;
    }

    /// <summary>
    /// 미리보기를 지금 고른 성별·머리·색으로 다시 그린다. 머리나 색을 넘길 때마다 다시 불린다 — 팔레트
    /// 교체(<see cref="Palettes"/>)는 (그림, 색) 별로 캐시돼 있어 이미 그려 본 조합은 다시 읽지 않는다.
    /// </summary>
    private void RefreshPreview()
    {
        foreach (Node child in _stage.GetChildren())
        {
            child.QueueFree();
        }

        Actor figure = new("미리보기", BareBodySheet())
        {
            Position = new Vector2(PreviewSize / 2f, PreviewSize - Main.Gutter)
        };
        _stage.AddChild(figure);
    }

    /// <summary>
    /// 맨몸 + 고른 머리만 그리는 겹 목록. 실제 게임이 쓰는 <see cref="Wardrobe.Pieces"/> 를 그대로 쓴다 —
    /// 갑옷·무기·신발·방패를 전부 0으로 주면 그 부위들은 스스로 빠진다(<c>Wardrobe.cs</c>). 그림이 없는
    /// 겹은 <c>WorldView.Dress</c> 와 같은 규칙으로 건너뛴다. 머리색은 <see cref="Palettes"/> 가 팔레트
    /// 98번부터 6칸을 갈아 끼워 그린다(plans/character-creation.md).
    /// </summary>
    private Actor.Sheet BareBodySheet()
    {
        Appearance appearance = new(
            Head: _hairStyle, Body: _gender * 16, Armor: 0, Boots: 0, Shield: 0, Weapon: 0,
            HairColor: _hairColor, BootColor: 0, HeadAccessory1: 0, Lantern: 0, HeadAccessory2: 0,
            Resting: 0, OverCoat: 0);

        List<string> paths = [];
        List<int> colours = [];
        List<char> parts = [];

        foreach (Piece piece in Wardrobe.Pieces(appearance))
        {
            string path = $"{PartsFolder}{piece.Name}.png";

            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            paths.Add(path);
            colours.Add(piece.Colour);
            parts.Add(piece.Name[1]);
        }

        return paths.Count > 0 ? Actor.Sheet.Walk(paths, colours, [], parts) : Actor.Sheet.Walk(HeroSheet);
    }

    private Control BuildHairRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label caption = Aux("HAIR");
        caption.CustomMinimumSize = new Vector2(CaptionWidth, 0);
        caption.VerticalAlignment = VerticalAlignment.Center;

        Button prev = ArrowButton("◀");
        Button next = ArrowButton("▶");

        _hairValue = ValueLabel();

        prev.Pressed += () => StepHair(-1);
        next.Pressed += () => StepHair(1);

        row.AddChild(caption);
        row.AddChild(prev);
        row.AddChild(_hairValue);
        row.AddChild(next);

        _hairRow = row;
        return row;
    }

    private Control BuildColorRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label caption = Aux("COLOR");
        caption.CustomMinimumSize = new Vector2(CaptionWidth, 0);
        caption.VerticalAlignment = VerticalAlignment.Center;

        Button prev = ArrowButton("◀");
        Button next = ArrowButton("▶");

        _colorValue = ValueLabel();

        prev.Pressed += () => StepColor(-1);
        next.Pressed += () => StepColor(1);

        row.AddChild(caption);
        row.AddChild(prev);
        row.AddChild(_colorValue);
        row.AddChild(next);

        _colorRow = row;
        return row;
    }

    private static Button ArrowButton(string glyph)
    {
        Button button = new() { Text = glyph, CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        Greybox.Plain(button);

        return button;
    }

    private static Label ValueLabel()
    {
        Label label = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(ValueWidth, 0)
        };
        label.AddThemeColorOverride("font_color", Greybox.Text);

        return label;
    }

    /// <summary>결번을 건너뛰며 넘긴다 — 목록은 지금 고른 성별의 것(data/character-creation/hairstyles.json).</summary>
    private void StepHair(int direction)
    {
        _hairStyle = HairStyles.Step(_hairStyle, _gender, direction);
        RefreshHair();
        RefreshPreview();
    }

    private const int ColorCount = 72;

    private void StepColor(int direction)
    {
        _hairColor = ((_hairColor + direction) % ColorCount + ColorCount) % ColorCount;
        RefreshColor();
        RefreshPreview();
    }

    /// <summary>
    /// 성별을 바꾼다. 지금 고른 머리 번호가 새 성별에 없으면(18·32·33 은 남자 전용) 가장 가까운
    /// 번호로 스스로 옮긴다 — 묻지도, 경고하지도 않는다(2026-09-19 결정).
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
        RefreshHair();
        RefreshPreview();
    }

    private void RefreshGender()
    {
        _male.ButtonPressed = _gender == 1;
        _female.ButtonPressed = _gender == 2;
    }

    private void RefreshHair() => _hairValue.Text = _hairStyle.ToString();

    private void RefreshColor() => _colorValue.Text = _hairColor.ToString();

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

        Task finished = _attempt;
        _attempt = null;

        if (finished.IsCompletedSuccessfully)
        {
            _status.Text = "계정과 캐릭터를 만들었습니다. 취소를 누르면 로그인 화면으로 갑니다.";
            return;
        }

        Exception failure = finished.Exception?.GetBaseException() ?? new IOException("알 수 없는 오류");

        _status.Text = failure.Message;
        _create.Disabled = false;
    }

    private static LineEdit Field(bool secret) => new()
    {
        Secret = secret,
        CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
    };

    private static Control FieldRow(string caption, LineEdit field)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter);

        Label captionLabel = Aux(caption);
        captionLabel.CustomMinimumSize = new Vector2(CaptionWidth, 0);
        captionLabel.VerticalAlignment = VerticalAlignment.Center;

        field.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        row.AddChild(captionLabel);
        row.AddChild(field);

        return row;
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

        bool ready = _username.Text.Length > 0 && _password.Text.Length > 0 && _password.Text == _confirm.Text;

        _create.Disabled = !ready;
        _status.Text = ready ? "만들 준비가 됐습니다." : "이름과 비밀번호(확인 포함)를 입력하세요.";
    }

    /// <summary>로그인 화면과 같은 이유로 같은 방식으로 키보드를 피한다.</summary>
    public override void _Process(double delta)
    {
        int keyboard = DisplayServer.VirtualKeyboardGetHeight();
        Vector2I screen = DisplayServer.ScreenGetSize();

        int lift = keyboard > 0 && screen.Y > 0
            ? Mathf.RoundToInt(keyboard / (float)screen.Y * GetViewportRect().Size.Y)
            : 0;

        _safeArea.AddThemeConstantOverride("margin_bottom", Main.SafeInsets.Bottom + lift);

        DrainCreate();
    }

    public override void _ExitTree()
    {
        _closing.Cancel();
        _closing.Dispose();
    }
}
