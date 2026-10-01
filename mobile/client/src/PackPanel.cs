using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What the character is carrying, as the original showed it: pictures with no names in them. Laid out the way phone
/// RPGs lay an inventory out (사용자, 2026-09-26): a title and an X in the top-right corner (<see cref="WindowFrame" />),
/// the grid across the whole width, a count in a cell's corner, and gold with how full the pack is on one line at the
/// bottom beside the 줍기 and 정렬 icons. Tapping a thing opens a small action row beside it — its name, one line (how
/// many, how worn) and icon buttons (사용/입기 · 버리기). Tapping the same thing twice quickly does the main one at once
/// (<see cref="DoubleTap" />).
/// </summary>
/// <remarks>
/// What is worn is its own window now (<see cref="GearPanel" />, 사용자 2026-10-01) — the original kept the two apart too,
/// and the gear picture did not fit under this window's head. The grid is rebuilt only when what it would show changes,
/// because it is asked every frame and a panel that throws its children away sixty times a second cannot be pressed.
///
/// The pack shows one page at a time and turns left and right — by a swipe across the pictures or by the arrows, since
/// no action may need a swipe alone (wireframes 2.2).
/// </remarks>
public sealed partial class PackPanel : PanelContainer
{
    // 원작은 33x36 칸이었다. 손가락은 그보다 커서 시안의 최소 터치 크기를 쓴다. 칸은 창 폭을 다 쓰도록 옆으로 늘어난다.
    private static readonly Vector2 Cell = new(Main.TouchMinimum, Main.TouchMinimum);

    // 한 장에 6열, 네 줄까지. 창이 받은 높이에 들어가는 만큼만 둔다(FitRows).
    private const int Columns = 6;
    private const int MostRows = 4;
    private int _perPage = Columns * MostRows;

    /// <summary>How far a finger has to travel across the pictures before it counts as turning the page.</summary>
    private const float SwipeDistance = Main.TouchMinimum;

    private readonly GridContainer _rows = new() { Name = "Items" };

    // 아래 한 줄 — 원작처럼 왼쪽에 몇 칸 찼나, 오른쪽 끝에 금화(사용자 2026-10-01).
    private readonly Label _count = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
    private readonly Label _gold = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

    // 종류 탭 — 서버는 무엇인지 말해 주지 않아 닳는 것을 장비로 친다(ItemActions.IsGear). null 이면 전체.
    private bool? _gearOnly;
    private readonly Button[] _kinds = [KindTab("전체"), KindTab("장비"), KindTab("기타")];
    private readonly TextureRect _actionIcon = new()
    {
        CustomMinimumSize = new Vector2(40, 40),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
    };

    // 칸을 누르면 그 옆에 뜨는 작은 동작 줄 — 이름 · 한 줄 설명 · 아이콘 단추.
    private readonly PanelContainer _action = new() { Name = "ItemAction", TopLevel = true, Visible = false, ZIndex = 5 };
    private readonly Label _actionName = new();
    private readonly Label _actionLine = new();
    private readonly Label _actionStats = new();
    private readonly GridContainer _actionTable = new();

    // 묶음을 버릴 때만 뜨는 개수 묻기 — 기본은 전부(사용자 2026-10-01).
    private readonly PanelContainer _ask = new() { Name = "DropCount", Visible = false };
    private readonly Label _askName = new();
    private readonly Button _askDrop = new() { Text = "버리기", CustomMinimumSize = new Vector2(96, Main.TouchMinimum) };
    private readonly Button _askCancel = new() { Text = "취소", CustomMinimumSize = new Vector2(96, Main.TouchMinimum) };
    private readonly Button _use = WindowFrame.IconButton(GlyphKind.Use, "입기", width: 56);
    private readonly Button _drop = WindowFrame.IconButton(GlyphKind.Drop, "버리기", tab: true);
    private readonly DoubleTap _taps = new();
    private readonly SpinBox _dropCount = new() { MinValue = 1, MaxValue = 1, Step = 1, Value = 1, CustomMinimumSize = new Vector2(80, Main.TouchMinimum) };

    // 지금 그려진 소지품 칸 — 동작 줄을 그 칸 옆에 세우려고 칸 번호로 찾는다.
    private readonly Dictionary<int, Button> _cellsBySlot = [];

    // 소지품 한 장과 장 넘김.
    private readonly VBoxContainer _content = new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill
    };

    private readonly HBoxContainer _main = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly HBoxContainer _pager = new() { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _pageNumber = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private HBoxContainer _foot = null!;
    private Control _head = null!;
    private bool _under;
    private Node _tidyHome = null!;
    private int _tidyAt;

    // 보이는 장, 그리고 손가락이 누른 자리. 밀어 넘긴 손은 그림을 고르지 않는다.
    private int _page;
    private float? _swipeFrom;
    private bool _swiped;
    private int _carriedCount;

    // 무엇을 고쳐 그렸는지. 고른 것이 바뀌어도 테두리가 옮겨 가야 하므로 함께 센다.
    private string? _showing;

    // 고른 소지품 칸 번호, 없으면 0.
    private int _chosen;

    public PackPanel()
    {
        Name = "Pack";
        Visible = false;
        // 틀은 원작 돌, 속은 평평한 어둠 — 무늬 위에 작은 글자를 얹으면 먼저 무너진다(data/ui-vault).
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter / 2);

        Tidy = WindowFrame.IconButton(GlyphKind.Sort, "정렬");
        Close = WindowFrame.CloseButton();

        _rows.Columns = Columns;
        _rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _rows.AddThemeConstantOverride("h_separation", 4);
        _rows.AddThemeConstantOverride("v_separation", 4);

        Button back = new() { Text = "◀", CustomMinimumSize = Cell, FocusMode = FocusModeEnum.None };
        Button forward = new() { Text = "▶", CustomMinimumSize = Cell, FocusMode = FocusModeEnum.None };
        Greybox.Plain(back);
        Greybox.Plain(forward);
        back.Pressed += () => Turn(-1);
        forward.Pressed += () => Turn(1);
        _pageNumber.CustomMinimumSize = Cell;
        _pageNumber.AddThemeColorOverride("font_color", Greybox.Muted);
        _pager.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _pager.AddChild(back);
        _pager.AddChild(_pageNumber);
        _pager.AddChild(forward);

        BuildAction();

        _content.AddThemeConstantOverride("separation", Main.Gutter / 2);
        // 칸 뒤 바닥은 원작 아이템 인벤토리 판(panel02)의 어두운 바닥 무늬 — 늘리지 않고 되풀이해 깐다. 칸 상자는 없고 아이템이
        // 바닥에 바로 놓인다(사용자 2026-10-01: 원작 장비창 아래 인벤토리를 모바일에 맞게).
        PanelContainer floor = new() { Name = "Floor" };
        floor.AddThemeStyleboxOverride("panel", Floor());
        floor.AddChild(_rows);
        _content.AddChild(floor);

        // 세로는 장 넘김이 칸 아래에, 가로는 낮아서 아래 한 줄(금화 옆)에 — 그만큼 칸이 한 줄 더 든다.
        if (Main.Portrait)
        {
            _content.AddChild(_pager);
        }

        _main.AddThemeConstantOverride("separation", Main.Gutter);
        _main.AddChild(_content);

        // 아래 한 줄: 금화 · 몇 칸 — 그리고 줍기 · 정렬 아이콘.
        _gold.AddThemeColorOverride("font_color", Greybox.Title);
        _gold.AddThemeFontSizeOverride("font_size", 13);
        _count.AddThemeColorOverride("font_color", Greybox.Muted);
        _count.AddThemeFontSizeOverride("font_size", 13);
        _foot = new HBoxContainer();
        _foot.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _foot.AddChild(_count);

        if (!Main.Portrait)
        {
            _pager.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            _pageNumber.CustomMinimumSize = new Vector2(36, Cell.Y);
            _gold.AddThemeFontSizeOverride("font_size", 11);
            _count.AddThemeFontSizeOverride("font_size", 11);
            _foot.AddChild(_pager);
        }

        _foot.AddChild(_gold);

        ButtonGroup kinds = new();
        bool?[] shows = [null, true, false];

        for (int i = 0; i < _kinds.Length; i++)
        {
            bool? only = shows[i];
            _kinds[i].ButtonGroup = kinds;
            _kinds[i].Pressed += () =>
            {
                _gearOnly = only;
                _page = 0;
                _showing = null;
            };
        }

        _kinds[0].ButtonPressed = true;

        // 정렬은 위 줄 탭 옆에(사용자 2026-10-01). 자동 줍기는 설정 창으로 옮겼다 — 모바일 게임들도 인벤토리에 두지 않는다.
        body.AddChild(_head = WindowFrame.Head(WindowFrame.Title("소지품"), Close, [.. _kinds, Tidy, _drop]));
        _tidyHome = Tidy.GetParent();
        _tidyAt = Tidy.GetIndex();
        body.AddChild(_main);
        body.AddChild(_foot);

        // 속 여백은 좌우 4.
        StyleBoxFlat sheet = Greybox.Sheet();
        sheet.ContentMarginLeft = Main.Gutter / 2;
        sheet.ContentMarginRight = Main.Gutter / 2;

        PanelContainer inside = new();
        inside.AddThemeStyleboxOverride("panel", sheet);
        inside.AddChild(body);

        AddChild(inside);
        Control actionLayer = new() { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(actionLayer);
        actionLayer.AddChild(_action);
        actionLayer.AddChild(_ask);
        _ask.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        _ask.GrowHorizontal = GrowDirection.Both;
        _ask.GrowVertical = GrowDirection.Both;

        // 소지품 한 장은 제 높이만큼만 아래에 붙는다(GameScreen.Cover).
        SizeFlagsVertical = SizeFlags.ShrinkEnd;
    }

    /// <summary>
    /// The row beside a picked thing: its name, a line under it, and the icon buttons that apply. One button does both
    /// carried things — the server's use (0x1C) puts gear on and drinks a potion — so only its word changes.
    /// </summary>
    private void BuildAction()
    {
        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = new Color("#0f0f0f");
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(10);
        plate.SetContentMarginAll(6);
        _action.AddThemeStyleboxOverride("panel", plate);

        _dropCount.GetLineEdit().VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Number;

        _actionName.AddThemeColorOverride("font_color", Greybox.Title);
        _actionName.AddThemeFontSizeOverride("font_size", 15);
        _actionName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _actionName.ClipText = true;
        _actionName.CustomMinimumSize = new Vector2(120, 0);
        _actionLine.AddThemeColorOverride("font_color", Greybox.Muted);
        _actionLine.AddThemeFontSizeOverride("font_size", 12);
        _actionStats.AddThemeColorOverride("font_color", Greybox.Text);
        _actionStats.AddThemeFontSizeOverride("font_size", 12);


        // 주 동작은 강조색 아이콘 — 창마다 확정은 하나(Greybox.Commit 과 같은 뜻).
        if (_use.GetMeta("glyph").As<Glyph>() is { } lit)
        {
            lit.Paint = Greybox.Accent;
        }

        _use.Pressed += () =>
        {
            if (_chosen > 0)
            {
                Used?.Invoke(_chosen);
            }
        };

        // 버리기는 위 줄에 켜고 끄는 단추(사용자 2026-10-01) — 켜 둔 동안 누르는 것마다 버린다. 창을 닫으면 꺼진다.
        // 켜진 버리기는 빨갛게 — 판·테두리·아이콘·글자까지, 글자도 「버리는 중」(사용자 2026-10-01: 잘못 버리지 않게).
        Color danger = new("#e05a5a");
        StyleBoxFlat armed = Greybox.Sheet();
        armed.BgColor = new Color("#3a1414");
        armed.BorderColor = danger;
        armed.SetBorderWidthAll(2);
        armed.SetCornerRadiusAll(8);
        armed.SetContentMarginAll(4);
        _drop.AddThemeStyleboxOverride("pressed", armed);
        _drop.AddThemeStyleboxOverride("hover_pressed", armed);
        _drop.Toggled += on =>
        {
            WindowFrame.Relabel(_drop, on ? "버리는 중" : "버리기");

            if (on)
            {
                _drop.GetMeta("glyph").As<Glyph>().Paint = danger;
                _drop.GetMeta("word").As<Label>().AddThemeColorOverride("font_color", danger);
            }

            _dropping = on;
            _chosen = 0;
            _showing = null;
            _action.Visible = false;
            _ask.Visible = false;
        };
        VisibilityChanged += () =>
        {
            if (!Visible)
            {
                _drop.ButtonPressed = false;
            }
        };

        _askDrop.Pressed += () =>
        {
            if (_held is { } held)
            {
                Dropped?.Invoke(held.Slot, (int)_dropCount.Value);
            }

            _ask.Visible = false;
        };
        _askCancel.Pressed += () => _ask.Visible = false;
        Greybox.Commit(_askDrop);
        Greybox.Plain(_askCancel);

        StyleBoxFlat askPlate = Greybox.Plate();
        askPlate.BgColor = new Color("#0f0f0f");
        askPlate.BorderColor = Greybox.Muted;
        askPlate.SetCornerRadiusAll(10);
        askPlate.SetContentMarginAll(Main.Gutter);
        _ask.AddThemeStyleboxOverride("panel", askPlate);
        _askName.AddThemeColorOverride("font_color", Greybox.Title);
        _askName.HorizontalAlignment = HorizontalAlignment.Center;
        VBoxContainer asking = new();
        asking.AddThemeConstantOverride("separation", Main.Gutter);
        asking.AddChild(_askName);
        // 숫자 칸은 작게 가운데, 숫자도 가운데.
        _dropCount.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _dropCount.CustomMinimumSize = new Vector2(120, 40);
        _dropCount.Alignment = HorizontalAlignment.Center;
        asking.AddChild(_dropCount);
        HBoxContainer answers = new() { Alignment = BoxContainer.AlignmentMode.Center };
        answers.AddThemeConstantOverride("separation", Main.Gutter);
        answers.AddChild(_askDrop);
        answers.AddChild(_askCancel);
        asking.AddChild(answers);
        _ask.AddChild(asking);


        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);
        // 누르면 뜨는 정보 상자 — 그림을 크게, 이름 아래에 내구·개수(사용자 2026-10-01, 다른 게임의 말풍선처럼).
        VBoxContainer words = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddChild(_actionName);
        words.AddChild(_actionLine);
        HBoxContainer top = new();
        top.AddThemeConstantOverride("separation", Main.Gutter);
        top.AddChild(_actionIcon);
        top.AddChild(words);
        // 교체·장착·사용은 오른쪽 위(장비창의 [장착 해제]와 같은 자리) — 아래 단추 줄 몫의 빈 곳이 없어진다(사용자 2026-10-01).
        top.AddChild(_use);
        column.AddChild(top);
        // 서버가 보낸 수치 — 공격력·방어·능력치·요구 레벨·직업·무게(우리 확장 0x0F 꼬리).
        column.AddChild(_actionTable);
        column.AddChild(_actionStats);
        _action.AddChild(column);
    }

    private static double Now() => Time.GetTicksMsec() / 1000.0;

    /// <summary>
    /// Upright under our gear window the pack gets only what the picture leaves, and its title strip and page turner ate
    /// all but one row (사용자 2026-10-01). There the gear window's Close shuts both, so the strip goes, and the turner joins
    /// the gold line as it does on its side, with smaller letters and the gold shortened to make room.
    /// </summary>
    public void UnderGear(bool under)
    {
        if (under == _under)
        {
            return;
        }

        _under = under;
        _head.Visible = !under;

        // 위 줄이 숨으면 정렬·버리기는 아래 줄 금화 앞으로.
        foreach (Button tool in new[] { Tidy, _drop })
        {
            tool.GetParent().RemoveChild(tool);

            if (under)
            {
                _foot.AddChild(tool);
                _foot.MoveChild(tool, _foot.GetChildCount() - 2);
            }
        }

        if (!under)
        {
            _tidyHome.AddChild(Tidy);
            _tidyHome.MoveChild(Tidy, _tidyAt);
            _tidyHome.AddChild(_drop);
            _tidyHome.MoveChild(_drop, _tidyAt + 1);
        }

        if (!Main.Portrait)
        {
            return;
        }

        _pager.GetParent().RemoveChild(_pager);
        _pager.SizeFlagsHorizontal = under ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill;
        _pageNumber.CustomMinimumSize = new Vector2(under ? 36 : Cell.X, Cell.Y);
        _gold.AddThemeFontSizeOverride("font_size", under ? 11 : 13);
        _count.AddThemeFontSizeOverride("font_size", under ? 11 : 13);

        // 금화·칸 수·정렬과 한 줄에 들도록 화살표를 조금 좁힌다(높이는 그대로).
        foreach (Node arrow in _pager.GetChildren())
        {
            if (arrow is Button button)
            {
                button.CustomMinimumSize = new Vector2(under ? 40 : Cell.X, Cell.Y);
            }
        }

        if (under)
        {
            _foot.AddChild(_pager);
            _foot.MoveChild(_pager, 1);
        }
        else
        {
            _content.AddChild(_pager);
        }
    }

    private static Button KindTab(string name)
    {
        Button tab = new() { Text = name, ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(40, Main.TouchMinimum) };
        Greybox.Tab(tab);

        return tab;
    }

    /// <summary>One place in the pack: a faintly sunken square on the floor, so empty places read as room left.</summary>
    private static StyleBoxFlat Place()
    {
        StyleBoxFlat place = new() { BgColor = new Color(0, 0, 0, 0.3f), BorderColor = new Color(1, 1, 1, 0.07f) };
        place.SetBorderWidthAll(1);
        place.SetCornerRadiusAll(3);

        return place;
    }

    private const string FloorArt = "res://assets/ui/pack-floor.png";

    /// <summary>The pack's floor: <c>panel02</c>'s dark recessed stone, tiled at its own size; plain dark when the picture is missing.</summary>
    private static StyleBox Floor()
    {
        if (!ResourceLoader.Exists(FloorArt))
        {
            return Greybox.Surface();
        }

        StyleBoxTexture floor = new()
        {
            Texture = GD.Load<Texture2D>(FloorArt),
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile
        };
        floor.SetContentMarginAll(4);

        return floor;
    }

    /// <summary>The button that shuts the panel, so whoever opened it can decide what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody asked to use a carried thing. The slot is what the server wants.</summary>
    public event System.Action<int>? Used;

    /// <summary>Somebody asked to throw a carried thing away. The server decides whether it may be.</summary>
    public event System.Action<int, int>? Dropped;

    /// <summary>What is worn now — the info box weighs a carried thing against what is on in its place.</summary>
    public IReadOnlyList<WornItem> Worn { get; set; } = [];

    /// <summary>The button that pulls everything to the front of the pack.</summary>
    public Button Tidy { get; }

    /// <summary>Shows what is carried, and says plainly when there is nothing.</summary>
    public void Show(IReadOnlyList<InventoryItem> carried, long gold = 0)
    {
        bool roomy = _pager.GetParent() != _foot;
        _count.Text = $"{carried.Count}/60칸";
        _gold.Text = roomy ? $"금화 {gold:N0}" : $"금화 {GameScreen.GoldText(gold)}";
        IReadOnlyList<InventoryItem> all = carried;
        carried = _gearOnly is { } gearOnly ? [.. carried.Where(item => ItemActions.IsGear(item) == gearOnly)] : carried;

        FitRows();

        string wanted = $"{_gearOnly}|{Describe(carried)}";

        if (wanted == _showing)
        {
            return;
        }

        _showing = wanted;

        _carriedCount = carried.Count;
        _page = Paging.Kept(_page, carried.Count, _perPage);
        _pageNumber.Text = $"{_page + 1}/{Paging.Pages(carried.Count, _perPage)}";
        Fill(_rows, Paging.Page(carried, _page, _perPage));

        _lastCarried = all;
        ShowChosen(all);
    }

    /// <summary>
    /// Picks the first thing in the pack and presses the button beside it — put it on, or throw it down. Only for a run
    /// with no hand on it: it goes through the same events the buttons raise, so the wiring is checked, not bypassed.
    /// </summary>
    public bool PressFirst(bool throwing = false)
    {
        foreach (Node cell in _rows.GetChildren())
        {
            if (cell is Button button)
            {
                // 버리기는 켜 두고 누른다. 입기는 누른 칸이 그려지려면 Show 가 한 번 돌아야 해 그 자리에서 고른 것을 채운다.
                if (throwing)
                {
                    _drop.ButtonPressed = true;
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                }
                else
                {
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    ShowChosen(_lastCarried);
                    _use.EmitSignal(BaseButton.SignalName.Pressed);
                }

                if (_ask.Visible)
                {
                    _askDrop.EmitSignal(BaseButton.SignalName.Pressed);
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rebuilds the page: one pressable picture per thing, the picked one outlined, and empty places where the pack runs
    /// out, so every page is the same height.
    /// </summary>
    private void Fill(GridContainer grid, IReadOnlyList<InventoryItem?> page)
    {
        foreach (Node cell in grid.GetChildren())
        {
            cell.QueueFree();
        }

        _cellsBySlot.Clear();

        foreach (InventoryItem? item in page)
        {
            if (item is null)
            {
                Panel place = new() { CustomMinimumSize = Cell, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
                place.AddThemeStyleboxOverride("panel", Place());
                grid.AddChild(place);
                continue;
            }

            int key = item.Slot;

            Button cell = new()
            {
                CustomMinimumSize = Cell,
                Icon = ItemIcons.For(item.Icon),
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None
            };

            // 칸은 바닥 위 희미한 자리(사용자 2026-10-01: 바닥 + 칸 자리), 고른 칸만 밝은 테두리.
            StyleBoxFlat box = Place();

            if (key == _chosen)
            {
                box.BorderColor = Greybox.Title;
                box.SetBorderWidthAll(2);
            }

            foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
            {
                cell.AddThemeStyleboxOverride(state, box);
            }

            // 개수는 칸 오른쪽 아래 구석에 작게.
            if (item.Stacks > 1)
            {
                Label count = new() { Text = item.Stacks.ToString(), MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right };
                count.AddThemeFontSizeOverride("font_size", 10);
                count.AddThemeColorOverride("font_color", Greybox.Text);
                count.AddThemeColorOverride("font_outline_color", Colors.Black);
                count.AddThemeConstantOverride("outline_size", 3);
                count.AnchorLeft = 0;
                count.AnchorRight = 1;
                count.AnchorTop = 1;
                count.AnchorBottom = 1;
                count.OffsetTop = -14;
                count.OffsetRight = -3;
                count.OffsetBottom = -1;
                cell.AddChild(count);
            }

            _cellsBySlot[key] = cell;

            cell.Pressed += () =>
            {
                if (_swiped)
                {
                    return;
                }

                if (_dropping)
                {
                    Throw(item);
                    return;
                }

                // 빠르게 두 번 = 사용/입기.
                if (_taps.Tap(key, Now()))
                {
                    _chosen = key;
                    _action.Visible = false;
                    Used?.Invoke(key);
                    return;
                }

                _chosen = key;

                // 테두리를 옮기려면 다시 그려야 한다. 다음 프레임의 Show 가 하도록 표시만 지운다.
                _showing = null;
            };

            grid.AddChild(cell);
        }
    }

    private void Turn(int step)
    {
        _page = step > 0 ? Paging.After(_page, _carriedCount, _perPage) : Paging.Before(_page, _carriedCount, _perPage);
        _showing = null;
    }

    /// <summary>
    /// Counts again how many rows a page can hold in the room the panel is given. What is outside the row the page
    /// stands in (the title strip, and upright the tab row and the 입기 row) and what stands under the page in it (the
    /// page turner) are measured apart from the rows, so the count does not feed back on itself.
    /// </summary>
    private void FitRows()
    {
        float outside = GetCombinedMinimumSize().Y - _main.GetCombinedMinimumSize().Y;
        float under = _content.GetCombinedMinimumSize().Y - _rows.GetCombinedMinimumSize().Y;
        int rows = Paging.RowsThatFit(Room() - outside, under, Cell.Y, _rows.GetThemeConstant("v_separation"), MostRows);

        if (Columns * rows != _perPage)
        {
            _perPage = Columns * rows;
            _showing = null;
        }
    }

    /// <summary>
    /// The height the holder's anchors give the panel — not the holder's own size, which grows with whatever it holds
    /// and so would always say there is room.
    /// </summary>
    private float Room() =>
        GetParent() is Control holder
            ? (holder.GetParentAreaSize().Y * (holder.AnchorBottom - holder.AnchorTop)) + holder.OffsetBottom - holder.OffsetTop
            : GetViewportRect().Size.Y;

    /// <summary>
    /// A swipe across the pictures turns the page. Watched before the pictures get the touch, so a finger that lands on
    /// one and slides away turns the page without picking it.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } press)
        {
            return;
        }

        if (press.Pressed)
        {
            _swiped = false;
            _swipeFrom = _rows.GetGlobalRect().HasPoint(press.Position) ? press.Position.X : null;
            return;
        }

        if (_swipeFrom is { } from && Mathf.Abs(press.Position.X - from) >= SwipeDistance)
        {
            _swiped = true;
            Turn(press.Position.X < from ? 1 : -1);
        }

        _swipeFrom = null;
    }

    /// <summary>
    /// Fills the action row for whatever is picked — name, one line, and the buttons that apply — or hides it when
    /// nothing is. Where it stands is worked out every frame (<see cref="PlaceAction" />), once the grid has settled.
    /// </summary>
    private void ShowChosen(IReadOnlyList<InventoryItem> carried)
    {
        InventoryItem? held = carried.FirstOrDefault(item => item.Slot == _chosen);

        if (held is not null)
        {
            _actionName.Text = held.Name;
            _actionIcon.Texture = ItemIcons.For(held.Icon);
            _actionLine.Text = ItemActions.Line(held);
            _actionLine.Visible = _actionLine.Text.Length > 0;
            WornItem? instead = ItemActions.WornInstead(held, Worn);
            WindowFrame.ShowStats(_actionTable, _actionStats, ItemActions.Stats(held, instead?.Stats));

            // 같은 자리에 걸친 것이 있으면 그것과 견준다(▲ 나음 · ▼ 못함).
            if (instead is not null)
            {
                _actionLine.Text = _actionLine.Text.Length > 0 ? $"{_actionLine.Text} · {instead.Called} 착용 중" : $"{instead.Called} 착용 중";
                _actionLine.Visible = true;
            }
            WindowFrame.Relabel(_use, ItemActions.Primary(held, Worn));
            _use.Visible = true;
            _held = held;
            _action.Visible = !_ask.Visible;
            _action.ResetSize();

            return;
        }

        _action.Visible = false;

        if (!_dropping)
        {
            _held = null;
            _ask.Visible = false;
        }
    }

    /// <summary>Throws away what was pressed while 버리기 is on: one at once, a bundle after asking how many (all, to begin with).</summary>
    private void Throw(InventoryItem item)
    {
        _held = item;

        if (item.Stacks <= 1)
        {
            Dropped?.Invoke(item.Slot, 1);
            return;
        }

        _askName.Text = $"{item.Name} — 몇 개 버릴까요?";
        _dropCount.MaxValue = item.Stacks;
        _dropCount.Value = item.Stacks;
        _ask.Visible = true;
    }

    private bool _dropping;

    // 지금 고른 것 — 위 줄 [버리기]가 버린다.
    private InventoryItem? _held;
    private IReadOnlyList<InventoryItem> _lastCarried = [];

    /// <summary>
    /// Stands the action row just above the picked cell — below it when there is no room above inside the window — and
    /// keeps it inside the window left and right.
    /// </summary>
    private void PlaceAction()
    {
        if (!_action.Visible)
        {
            return;
        }

        Control? cell = _cellsBySlot.GetValueOrDefault(_chosen);

        if (cell is null || !cell.IsVisibleInTree())
        {
            _action.Visible = false;
            return;
        }

        Rect2 window = GetGlobalRect();
        Rect2 at = cell.GetGlobalRect();
        Vector2 size = _action.GetCombinedMinimumSize();
        float x = Mathf.Clamp(at.GetCenter().X - (size.X / 2), window.Position.X + 4, Mathf.Max(window.Position.X + 4, window.End.X - size.X - 4));
        float above = at.Position.Y - size.Y - 4;
        float y = above >= window.Position.Y + 4 ? above : at.End.Y + 4;

        _action.Size = size;
        float bottom = Mathf.Min(window.End.Y, GetViewportRect().Size.Y - TouchInput.Covered);
        _action.GlobalPosition = new Vector2(x, Mathf.Clamp(y, window.Position.Y + 4, Mathf.Max(window.Position.Y + 4, bottom - size.Y - 4)));
    }

    public override void _Process(double delta)
    {
        PlaceAction();
    }

    /// <summary>
    /// Taps the <paramref name="nth" /> picture on the pack page (1-based) the way a finger does — only for a run with no
    /// hand on it (<c>--pack-pick</c>), to photograph the action row it opens.
    /// </summary>
    public bool PickNth(int nth)
    {
        Button? cell = _rows.GetChildren().OfType<Button>().Skip(nth - 1).FirstOrDefault();

        if (cell is null)
        {
            return false;
        }

        cell.EmitSignal(BaseButton.SignalName.Pressed);

        return true;
    }

    private string Describe(IReadOnlyList<InventoryItem> carried) =>
        $"{_chosen}|{_page}|" + string.Join(";", carried.Select(item => $"{item.Slot}:{item.Icon}:{item.Stacks}"));
}
