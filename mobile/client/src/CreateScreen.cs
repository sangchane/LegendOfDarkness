using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 캐릭터 만들기 화면, 원작 <c>dlgcre00.png</c> 의 배치를 세로 한 줄로 편다 — 이름·비밀번호·확인,
/// 남/여, HAIR·COLOR 를 눈으로 보고 고르는 격자, 가운데 미리보기, 만들기/취소.
/// </summary>
/// <remarks>
/// 원작의 E-MAIL 칸과 PHRASE MACRO 표는 뺐다 — 서버 프로토콜(<see cref="Hades718LoginProtocol"/> 의
/// <c>CreateAccountRequest</c>)이 이름·비밀번호만 받는다. 원작은 HAIR·COLOR 를 ◀ ▶ 로 숫자를 하나씩
/// 넘기게 했지만, 숫자만 보고는 무슨 모양·무슨 색인지 알 수 없다(사용자, 2026-09-19) — 그래서 여기서는
/// 격자를 깔아 눈으로 보고 누르게 바꿨다. 화살표는 없앴다: 격자 하나로 고르고 확인까지 되므로 화살표가
/// 하는 일이 남지 않고, 좁은 화면에서 화살표 두 줄(96px)을 없애야 격자가 앉을 자리가 난다.
/// </remarks>
public sealed partial class CreateScreen : Control
{
    private const int TitleFontSize = 22;
    private const int AuxFontSize = 14;
    private const int FormWidth = 300;

    // 미리보기 칸 — 이 화면의 주인공(사용자, 2026-09-19: "몸이 가운데·크게"). 세로가 가로보다 큰
    // 것은 사람이 가로보다 세로로 긴 그림이기 때문이다. 이름·비밀번호·확인 세 칸을 한 줄로 모으고
    // (아래 AuthRow) 옆 캡션을 힌트 글자로 바꿔 세로 96px 을 돌려받아, 그 값으로 배율을 3배까지
    // 올렸다(예전엔 격자 둘 자리가 안 나 2배에 머물렀다).
    private const int PreviewWidth = 168;
    private const int PreviewHeight = 228;

    // 정수 배율로 키운다 — 3배씩이면 원작 그림 한 칸(1px)이 화면에서도 칼같이 3px 로 남는다(흐려지지
    // 않음). 고도 프로젝트 설정(project.godot: default_texture_filter=0=Nearest)이 이미 전역으로
    // 이렇게 그리고 있어 Actor.cs·WorldView.cs 를 보니 텍스처마다 따로 필터를 거는 코드가 없었다 — 여기도
    // 새로 걸지 않고 그 설정에 얹힌다.
    private const int PreviewScale = 3;

    // 원작 그림칸(120x96)의 발 기준점(FeetX=31.5, FeetY=83, Actor.cs)은 실제 그려진 그림의 한가운데가
    // 아니다 — 옆으로는 무기를 휘두를 자리를, 위로는 머리 위 여백을 남겨 두기 때문이다. 몸(mb001·wb001)과
    // 머리 그림 전부(서기 프레임: 뒷모습 0번 · 앞모습 5번, WalkMotion.Stand)를 실측한 테두리 한가운데는
    // (약 29.25, 41) 이었다 — 기준점과의 차이만큼 미리보기를 옮겨 기준점이 아니라 실제 그림이 칸
    // 한가운데 오게 한다. 뒷모습·앞모습이 이 차이가 서로 거의 같아(값이 다르지 않음) 방향이 바뀌어도
    // 이 보정은 그대로 쓴다 — 그래서 돌아도 들썩이지 않는다.
    private const float BodyCentreOffsetX = 2.25f;
    private const float BodyCentreOffsetY = 42f;

    /// <summary>사람이 한 바퀴 도는 데 걸리는, 한 방향을 보여 주는 시간.</summary>
    private const float FacingSeconds = 1.2f;

    /// <summary>맨몸·머리 그림이 하나도 없을 때만 쓰는 마지막 대안(있을 리 없음).</summary>
    private const string HeroSheet = "res://assets/actor/hero-walk.png";

    /// <summary>겹쳐 입힐 그림들이 있는 자리 — <c>WorldView.Dress</c> 와 같은 값(부위별로 따로 못 나눈다).</summary>
    private const string PartsFolder = "res://assets/actor/parts/";

    // 머리 그림 한 칸의 크기와, 미리 서 있는 자세(앞모습)가 있는 칸 — WalkMotion.Stand(Side.Front) 그대로.
    private const int CellWidth = 120;
    private const int CellHeight = 96;

    // COLOR 조각 하나 — 원작 소지품 칸(PackPanel.cs)과 같은 최소 터치 크기를 그대로 쓴다. 새 치수를
    // 만들지 않는다. 색은 판판한 사각형이라 이 크기로도 잘 보인다.
    private static readonly Vector2 ColorTileSize = new(Main.TouchMinimum, Main.TouchMinimum);
    private const int ColorGridColumns = 4;

    // HAIR 조각은 44x56 으로는 부족했다 — 찍어 보니 작은 보라색 얼룩일 뿐 모양이 안 보였다(원인:
    // 자른 그림(36x60)이 44x56 에 맞추려 오히려 0.93배로 더 줄어들었다 — 칸의 가로세로 비가 자른
    // 그림과 달라, 짧은 쪽(세로)이 기준이 돼 버렸다). 자른 그림과 같은 비(34:58)로 54x92 를 줘
    // 약 1.59배로 키운다(몸 미리보기를 키우고 두 줄을 다 보이게 하는 것과 세로를 나눠 가지느라
    // 63x108·1.86배보다는 한 단 낮췄다 — 찍어서 제목이 안 잘리는 걸 확인하며 정함). 한 줄에 4개
    // 대신 3개만 두는 것도 여기서 나온 자리다(칸이 커진 만큼).
    private static readonly Vector2 HairTileSize = new(54, 92);
    private const int HairGridColumns = 3;

    private const int ColorCount = 72;

    // 머리 그림칸(120x96) 안에서 머리·얼굴이 있는 자리만 잘라 쓴다 — 전신을 다 보여 주면 칸 안에서
    // 아주 작아져 모양을 알아볼 수 없다. 남 59·여 56 가지 전부(앞모습, 프레임 5)를 하나하나 실측하니
    // (스크립트로 낱개 테두리를 다 짐) 대부분(특히 남자)은 x 18~41·y 18~42 안에 들지만, 여자 긴 머리
    // 여럿(wh025·wh031·wh036 …)은 y 60 까지 내려온다 — 거기서 잘리면 그 머리가 "긴 머리"인 것 자체를
    // 못 알아본다. 그래서 세로는 줄이지 않고 x 14~48(34폭)·y 0~58(58높이)을 쓴다.
    private static readonly Rect2 HairThumbRegion =
        new(WalkMotion.Stand(Lod.Mobile.Core.Art.Side.Front) * CellWidth + 14, 0, 34, 58);

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

    private Control _preview = null!;
    private Control _stage = null!;
    private Actor? _previewActor = null!;
    private int _previewHeight = PreviewHeight;

    private Control _hairRow = null!;
    private GridContainer _hairGrid = null!;
    private readonly Dictionary<int, Button> _hairTiles = new();

    private Control _colorRow = null!;
    private readonly Dictionary<int, Button> _colorTiles = new();

    private Control _buttonsRow = null!;

    // 1=남 · 2=여 — 서버가 읽는 값 그대로(ClientFormat04.cs). 머리색은 0~71.
    private byte _gender = 1;
    private int _hairStyle = 1;
    private int _hairColor;
    // 1=전사 · 2=도적 · 3=마법사 · 4=성직자 · 5=무도가. A new character must deliberately pick one;
    // Peasant (0) is never silently persisted by this screen.
    private byte? _path;

    /// <summary>Called on the main thread when creation has already completed the normal login into the world.</summary>
    public Action<WorldSession>? Entered { get; set; }

    // 미리보기가 스스로 도는 방향과, 지금 방향을 얼마나 오래 보여 줬나. 머리·색·성별을 바꿔도 이 둘은
    // 그대로 둔다 — 돌던 것이 끊기지 않게(사용자, 2026-09-19).
    private Direction _facing = Direction.South;
    private double _facingElapsed;

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
        panel.AddThemeStyleboxOverride("panel", Greybox.Surface());

        MarginContainer padding = new();
        padding.AddThemeConstantOverride("margin_left", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_top", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_right", Main.Gutter * 2);
        padding.AddThemeConstantOverride("margin_bottom", Main.Gutter * 2);

        Label title = new()
        {
            Text = "캐릭터 만들기",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        title.AddThemeColorOverride("font_color", Greybox.Title);

        _username = Field(secret: false, placeholder: "이름");
        _password = Field(secret: true, placeholder: "비밀번호");
        _confirm = Field(secret: true, placeholder: "확인");

        HBoxContainer authRow = new();
        authRow.AddThemeConstantOverride("separation", Main.Gutter);
        authRow.AddChild(_username);
        authRow.AddChild(_password);
        authRow.AddChild(_confirm);

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

        Control preview = BuildPreview();
        Control gender = BuildGenderRow();
        Control path = BuildPathRow();
        Control hair = BuildHairGrid();
        Control color = BuildColorGrid();

        Control form = Main.Portrait
            ? BuildPortraitForm(title, authRow, preview, gender, path, hair, color, buttons)
            : BuildLandscapeForm(title, authRow, preview, gender, path, hair, color, buttons);

        padding.AddChild(form);
        panel.AddChild(padding);

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

    /// <summary>
    /// 짧은 가로 화면은 세로 여백 대신 폭을 쓴다. 입력·미리보기·꾸미기를 서로 다른 열에 놓아 모든
    /// 조작이 360px 높이 안에 남고, HAIR/COLOR는 기존의 스크롤 격자와 선택 동작을 그대로 쓴다.
    /// </summary>
    private Control BuildLandscapeForm(
        Control title, Control auth, Control preview, Control gender, Control path, Control hair, Control color, Control buttons)
    {
        HBoxContainer columns = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", Main.Gutter);

        VBoxContainer input = Column();
        input.CustomMinimumSize = new Vector2(200, 0);
        input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        input.AddChild(title);
        input.AddChild(auth);
        input.AddChild(gender);
        input.AddChild(path);
        input.AddChild(_status);
        input.AddChild(buttons);

        preview.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        VBoxContainer appearance = Column();
        appearance.CustomMinimumSize = new Vector2(ColorGridColumns * Main.TouchMinimum + (ColorGridColumns - 1) * Main.Gutter, 0);
        appearance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        appearance.AddChild(hair);
        appearance.AddChild(color);

        columns.AddChild(input);
        columns.AddChild(preview);
        columns.AddChild(appearance);
        return columns;
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
    /// The primary class is selected at creation, not deferred to the NPC-only legacy chooser. The row is
    /// horizontally scrollable on a short landscape phone so every 48px touch target remains reachable.
    /// </summary>
    private Control BuildPathRow()
    {
        HBoxContainer choices = new();
        choices.AddThemeConstantOverride("separation", Main.Gutter / 2);

        foreach ((byte path, string label) in new[]
        {
            ((byte)1, "전사"), ((byte)2, "도적"), ((byte)3, "법사"), ((byte)4, "사제"), ((byte)5, "무도")
        })
        {
            Button tile = new()
            {
                Text = label,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
            };
            Greybox.Tab(tile);
            tile.Pressed += () => SelectPath(path);
            _pathTiles[path] = tile;
            choices.AddChild(tile);
        }

        ScrollContainer scroll = new()
        {
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        scroll.AddChild(choices);
        _pathRow = scroll;
        return scroll;
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
    /// 가운데 미리보기 자리. 고른 직업이 있으면 실제 5.99 레벨 1 직업 갑옷을 입히고, 아직 안 골랐을
    /// 때만 맨몸이다. 실제 그림은 <see cref="RefreshPreview"/> 가 채운다.
    /// </summary>
    private Control BuildPreview()
    {
        // On the compact 360x640 portrait drawable the decorative preview yields height, never input
        // fields or the two final actions.
        // The five job choices add one 48px thumb row.  At 360x640 reserve that room from the
        // decorative preview, rather than letting the first fields and the final actions fall offscreen.
        int height = Main.Portrait && GetViewportRect().Size.Y <= 680 ? 110 : PreviewHeight;
        _previewHeight = height;
        PanelContainer box = new() { CustomMinimumSize = new Vector2(PreviewWidth, height) };
        box.AddThemeStyleboxOverride("panel", Greybox.Surface());

        _stage = new Control { ClipContents = true };

        box.AddChild(_stage);
        _preview = box;
        return box;
    }

    /// <summary>
    /// 미리보기를 지금 고른 성별·머리·색으로, 지금 돌고 있는 방향으로 다시 그린다. 머리나 색을 넘길
    /// 때마다 다시 불린다 — 팔레트 교체(<see cref="Palettes"/>)는 (그림, 색) 별로 캐시돼 있어 이미
    /// 그려 본 조합은 다시 읽지 않는다.
    /// </summary>
    private void RefreshPreview()
    {
        foreach (Node child in _stage.GetChildren())
        {
            child.QueueFree();
        }

        // 키우는 것과 가운데에 놓는 것은 Actor 가 아니라 이 겉 노드가 한다 — Actor.Face() 가 자기
        // Scale.X 를 좌우 뒤집기(거울)에 쓰고 있어(Actor.cs), 거기 손대지 않고 배율을 얹으려면 한 칸
        // 밖에서 씌워야 한다.
        Node2D wrapper = new()
        {
            Position = new Vector2(
                PreviewWidth / 2f + BodyCentreOffsetX * PreviewScale,
                _previewHeight / 2f + BodyCentreOffsetY * PreviewScale),
            Scale = new Vector2(PreviewScale, PreviewScale)
        };

        Actor figure = new("미리보기", BareBodySheet());
        wrapper.AddChild(figure);
        _stage.AddChild(wrapper);

        figure.Face(_facing);
        _previewActor = figure;
    }

    /// <summary>
    /// 고른 머리와 실제 레벨 1 직업 갑옷을 그리는 겹 목록. 실제 게임이 쓰는 <see cref="Wardrobe.Pieces"/> 를 그대로 쓴다 —
    /// 갑옷·무기·신발·방패를 전부 0으로 주면 그 부위들은 스스로 빠진다(<c>Wardrobe.cs</c>). 다만 바지
    /// (part 'n')는 여기서 따로 뺀다: 몸 그림(mb001.png) 을 실제로 뽑아 보니 이미 흰/회색 팬티 차림의
    /// 맨몸이었다 — 바지는 그 위에 게임 화면(WorldView)이 항상 덧입히는 것이라 미리보기에서는 필요
    /// 없다(Wardrobe.cs 자체는 게임 화면도 같이 쓰므로 고치지 않는다). 그림이 없는 겹은
    /// <c>WorldView.Dress</c> 와 같은 규칙으로 건너뛴다. 머리색은 <see cref="Palettes"/> 가 팔레트
    /// 98번부터 6칸을 갈아 끼워 그린다(plans/character-creation.md).
    /// </summary>
    private Actor.Sheet BareBodySheet()
    {
        Appearance appearance = new(
            Head: _hairStyle, Body: _gender * 16, Armor: StarterArmor(), Boots: 0, Shield: 0, Weapon: 0,
            HairColor: _hairColor, BootColor: 0, HeadAccessory1: 0, Lantern: 0, HeadAccessory2: 0,
            Resting: 0, OverCoat: 0);

        List<string> paths = [];
        List<int> colours = [];
        List<char> parts = [];

        foreach (Piece piece in Wardrobe.Pieces(appearance))
        {
            if (piece.Name[1] == 'n')
            {
                continue;
            }

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

    /// <summary>5.99 level-one class armour image: warrior 2, rogue 4, wizard 6, priest 5, monk 3.</summary>
    private int StarterArmor() => _path switch
    {
        1 => 2,
        2 => 4,
        3 => 6,
        4 => 5,
        5 => 3,
        _ => 0
    };

    /// <summary>HAIR 격자의 틀 — 자리는 <see cref="PopulateHairGrid"/> 가 채운다(성별이 바뀔 때 다시).</summary>
    private Control BuildHairGrid()
    {
        VBoxContainer section = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        section.AddThemeConstantOverride("separation", Main.Gutter / 2);
        section.AddChild(Aux("HAIR"));

        _hairGrid = new GridContainer { Columns = HairGridColumns };
        _hairGrid.AddThemeConstantOverride("h_separation", Main.Gutter);
        _hairGrid.AddThemeConstantOverride("v_separation", Main.Gutter);

        // 최소 두 줄은 보이게 한다(사용자, 2026-09-19) — 한 줄만 보이면 옆에 뭐가 더 있는지 스크롤바
        // 손잡이로만 짐작해야 해서 고르기 나쁘다. (임시: 한 줄로 자리를 먼저 잡는다 — 다음 편집에서
        // 실제 예산을 재고 두 줄 높이로 올린다.)
        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, HairTileSize.Y * 2 + Main.Gutter),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        scroll.AddChild(_hairGrid);
        section.AddChild(scroll);

        _hairRow = section;
        return section;
    }

    /// <summary>
    /// HAIR 격자를 지금 성별의 머리 번호로, 지금 고른 COLOR 로 다시 채운다. 그림 조각 하나가 곧
    /// 단추다 — 원작 소지품 칸(PackPanel.cs)과 같은 방식으로, 고른 것만 <c>Flat=false</c> 를 둬
    /// 테두리가 남게 한다. COLOR 를 바꿀 때는 이 함수를 통째로 다시 부르지 않고
    /// <see cref="RefreshHairIcons"/> 만 불러 그림만 새로 물들인다(단추를 다시 짓지 않음).
    /// </summary>
    private void PopulateHairGrid()
    {
        foreach (Node child in _hairGrid.GetChildren())
        {
            child.QueueFree();
        }

        _hairTiles.Clear();

        foreach (int number in HairStyles.For(_gender))
        {
            Button tile = new()
            {
                CustomMinimumSize = HairTileSize,
                Icon = HairIcon(number),
                ExpandIcon = true,
                Flat = number != _hairStyle
            };

            int picked = number;
            tile.Pressed += () => SelectHair(picked);

            _hairTiles[number] = tile;
            _hairGrid.AddChild(tile);
        }
    }

    /// <summary>
    /// 이 머리 번호를, 지금 성별·지금 고른 COLOR 로 물들여 자른 그림. 그림이 없으면(있을 리 없음) null.
    /// </summary>
    private AtlasTexture? HairIcon(int number)
    {
        char genderLetter = _gender == 2 ? 'w' : 'm';
        string path = $"{PartsFolder}{genderLetter}h{number:000}.png";

        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        Texture2D dyed = Palettes.Load(path, _hairColor);
        return new AtlasTexture { Atlas = dyed, Region = HairThumbRegion };
    }

    /// <summary>
    /// COLOR 를 바꿨을 때 HAIR 격자의 그림만 새로 물들인다 — 단추를 다시 짓지 않아 고른 표시
    /// (Flat)가 흔들리지 않는다. <see cref="Palettes.Load"/> 가 (그림, 색) 별로 캐시하므로 같은
    /// 색을 다시 고르면 다시 물들이지 않는다.
    /// </summary>
    private void RefreshHairIcons()
    {
        foreach ((int number, Button tile) in _hairTiles)
        {
            tile.Icon = HairIcon(number);
        }
    }

    /// <summary>COLOR 격자 — 72가지 색 조각. 한 번만 짓는다(성별과 무관).</summary>
    private Control BuildColorGrid()
    {
        VBoxContainer section = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        section.AddThemeConstantOverride("separation", Main.Gutter / 2);
        section.AddChild(Aux("COLOR"));

        GridContainer grid = new() { Columns = ColorGridColumns };
        grid.AddThemeConstantOverride("h_separation", Main.Gutter);
        grid.AddThemeConstantOverride("v_separation", Main.Gutter);

        IReadOnlyDictionary<int, IReadOnlyList<Colour>> table = Palettes.AllColours();

        for (int number = 0; number < ColorCount; number++)
        {
            // 색 하나는 여섯 톤을 가진다(밝은 것부터 어두운 것까지, data/character-creation/hair-colours.json).
            // 조각 하나로는 하나만 보여 줄 수 있으니 가운데 톤(6개 중 3번째, 0-기준 인덱스 2)을 쓴다 — 가장
            // 밝은 톤은 바래 보이고 가장 어두운 톤은 칙칙해, 그 사이가 "이 색"이라고 봤을 때 가장 무난했다.
            Colour shade = table.TryGetValue(number, out IReadOnlyList<Colour>? shades) && shades.Count > 0
                ? shades[Mathf.Min(2, shades.Count - 1)]
                : new Colour(120, 120, 120);

            Button tile = new()
            {
                CustomMinimumSize = ColorTileSize,
                Icon = SwatchIcon(shade),
                ExpandIcon = true,
                Flat = number != _hairColor
            };

            int picked = number;
            tile.Pressed += () => SelectColor(picked);

            _colorTiles[number] = tile;
            grid.AddChild(tile);
        }

        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        scroll.AddChild(grid);
        section.AddChild(scroll);

        _colorRow = section;
        return section;
    }

    /// <summary>한 색으로 칠한 1x1 그림 — 단추 안을 그 색으로 채우는 가장 짧은 길(ExpandIcon 이 늘려 준다).</summary>
    private static ImageTexture SwatchIcon(Colour colour)
    {
        Image pixel = Image.CreateEmpty(1, 1, false, Image.Format.Rgb8);
        pixel.SetPixel(0, 0, Color.Color8(colour.R, colour.G, colour.B));

        return ImageTexture.CreateFromImage(pixel);
    }

    private void SelectHair(int number)
    {
        _hairStyle = number;
        RefreshHairSelection();
        RefreshPreview();
    }

    private void SelectColor(int number)
    {
        _hairColor = number;
        RefreshColorSelection();
        RefreshHairIcons();
        RefreshPreview();
    }

    private void RefreshHairSelection()
    {
        foreach ((int number, Button tile) in _hairTiles)
        {
            tile.Flat = number != _hairStyle;
        }
    }

    private void RefreshColorSelection()
    {
        foreach ((int number, Button tile) in _colorTiles)
        {
            tile.Flat = number != _hairColor;
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
    private static LineEdit Field(bool secret, string placeholder) => new()
    {
        Secret = secret,
        PlaceholderText = placeholder,
        CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
        SizeFlagsHorizontal = SizeFlags.ExpandFill
    };

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

    /// <summary>한 바퀴 도는 차례 — 북·동·남·서(시계 방향). 두 벌만 있는 그림을 돌아가며 거울에 비추므로
    /// (Facing.Of, Wardrobe.Rank 와 같은 원작 규칙) 넷 다 자연스럽게 이어진다.</summary>
    private static Direction NextFacing(Direction facing) => facing switch
    {
        Direction.North => Direction.East,
        Direction.East => Direction.South,
        Direction.South => Direction.West,
        _ => Direction.North
    };

    /// <summary>로그인 화면과 같은 이유로 같은 방식으로 키보드를 피한다. 미리보기가 스스로 도는 것도
    /// 여기서 잰다 — Actor 를 다시 짓지 않고 <see cref="Actor.Face"/> 만 불러 가볍다.</summary>
    public override void _Process(double delta)
    {
        int keyboard = DisplayServer.VirtualKeyboardGetHeight();
        Vector2I screen = DisplayServer.ScreenGetSize();

        int lift = keyboard > 0 && screen.Y > 0
            ? Mathf.RoundToInt(keyboard / (float)screen.Y * GetViewportRect().Size.Y)
            : 0;

        _safeArea.AddThemeConstantOverride("margin_bottom", Main.SafeInsets.Bottom + lift);

        _facingElapsed += delta;

        if (_facingElapsed >= FacingSeconds)
        {
            _facingElapsed -= FacingSeconds;
            _facing = NextFacing(_facing);
            _previewActor?.Face(_facing);
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
