using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What the character has on and what they are carrying, as the original showed it: pictures with no names in them.
/// Laid out the way phone RPGs lay an inventory out (사용자, 2026-09-26): small icon tabs at the top — 소지품 · 장비 — and
/// an X in the top-right corner (<see cref="WindowFrame" />), the grid across the whole width, a count in a cell's corner,
/// and gold with how full the pack is on one line at the bottom beside the 줍기 and 정렬 icons. There are no long buttons
/// standing under the grid any more: tapping a thing opens a small action row beside it — its name, one line (how many,
/// how worn) and icon buttons (사용/입기 · 버리기, or 벗기 for something worn). Tapping the same thing twice quickly does
/// the main one at once (<see cref="DoubleTap" />).
/// </summary>
/// <remarks>
/// The two are separate tabs, not one list above another, because the original kept them in separate windows. The gear
/// tab is the original's own ring of places (<see cref="GearGrid" />); the pack tab is a plain grid of pictures. Both are
/// rebuilt only when what they would show changes, because they are asked every frame and a panel that throws its
/// children away sixty times a second cannot be pressed.
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

    // 가로 장비 고리의 칸. 44 를 먼저 노리고, 안 되면 40, 36 까지 — 그 아래는 손가락이 못 누른다(사용자·조정자, 2026-09-23).
    private static readonly int[] PressableCells = [44, 40, 36];

    /// <summary>How far a finger has to travel across the pictures before it counts as turning the page.</summary>
    private const float SwipeDistance = Main.TouchMinimum;

    private readonly GearGrid _gear = new();
    private readonly GridContainer _rows = new() { Name = "Items" };
    private readonly Button _packTab = WindowFrame.IconButton(GlyphKind.Bag, "소지품", tab: true, width: 56);
    private readonly Button _gearTab = WindowFrame.IconButton(GlyphKind.Armor, "장비", tab: true, width: 56);

    /// <summary>밟은 것을 알아서 줍는지 켜고 끄는 아이콘.</summary>
    private readonly Button _loot = WindowFrame.IconButton(GlyphKind.Loot, "줍기", tab: true);

    private void ShowLoot() => WindowFrame.Relabel(_loot, Main.AutoLoot ? "줍기 켬" : "줍기 끔");

    // 아래 한 줄 — 금화와 몇 칸 찼나.
    private readonly Label _gold = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };

    // 칸을 누르면 그 옆에 뜨는 작은 동작 줄 — 이름 · 한 줄 설명 · 아이콘 단추.
    private readonly PanelContainer _action = new() { Name = "ItemAction", TopLevel = true, Visible = false, ZIndex = 5 };
    private readonly Label _actionName = new();
    private readonly Label _actionLine = new();
    private readonly Button _use = WindowFrame.IconButton(GlyphKind.Use, "입기", width: 56);
    private readonly Button _drop = WindowFrame.IconButton(GlyphKind.Drop, "버리기", width: 56);
    private readonly Button _off = WindowFrame.IconButton(GlyphKind.TakeOff, "벗기", width: 56);
    private readonly DoubleTap _taps = new();

    // 지금 그려진 소지품 칸 — 동작 줄을 그 칸 옆에 세우려고 칸 번호로 찾는다.
    private readonly Dictionary<int, Button> _cellsBySlot = [];

    // 탭의 내용(장비 고리 또는 소지품 한 장과 장 넘김).
    private readonly VBoxContainer _content = new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill
    };

    private readonly HBoxContainer _main = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly HBoxContainer _pager = new() { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _pageNumber = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private HBoxContainer _foot = null!;

    // 보이는 장, 그리고 손가락이 누른 자리. 밀어 넘긴 손은 그림을 고르지 않는다.
    private int _page;
    private float? _swipeFrom;
    private bool _swiped;
    private int _carriedCount;

    // 어느 탭이 보이나. 장비면 true.
    private bool _onGear;

    // 무엇을 고쳐 그렸는지. 고른 것이 바뀌어도 테두리가 옮겨 가야 하므로 함께 센다.
    private string? _showing;

    // 고른 것: 소지품이면 칸 번호, 걸친 것이면 자리 번호에 음수를 붙여 구별한다.
    private int _chosen;

    public PackPanel()
    {
        Name = "Pack";
        Visible = false;
        // 틀은 원작 돌, 속은 평평한 어둠 — 무늬 위에 작은 글자를 얹으면 먼저 무너진다(data/ui-vault).
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter / 2);

        _gearTab.Pressed += () => ShowTab(gear: true);
        _packTab.Pressed += () => ShowTab(gear: false);

        // 밟은 것을 알아서 주울지. 원작에는 없던 것이라 끌 수 있어야 한다(사용자, 2026-09-19).
        _loot.ButtonPressed = Main.AutoLoot;
        ShowLoot();
        _loot.Pressed += () =>
        {
            Main.SetAutoLoot(_loot.ButtonPressed);
            ShowLoot();
        };

        Tidy = WindowFrame.IconButton(GlyphKind.Sort, "정렬");
        Close = WindowFrame.CloseButton();

        _rows.Columns = Columns;
        _rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _rows.AddThemeConstantOverride("h_separation", 4);
        _rows.AddThemeConstantOverride("v_separation", 4);

        // 걸친 것을 고르는 것은 소지품과 같은 한 자리를 쓴다. 음수로 두어 칸 번호와 구별한다. 빠르게 두 번 누르면 벗는다.
        _gear.Chosen += slot =>
        {
            if (_taps.Tap(-slot, Now()))
            {
                _action.Visible = false;
                TakenOff?.Invoke(slot);
                return;
            }

            _chosen = -slot;
            _showing = null;
        };

        // 장비 고리는 장으로 나눌 수도, 굴릴 수도 없다 — 가로에서 굴려 내리게 했더니 불편해서 못 쓴다고 했다(사용자,
        // 2026-09-23). 가로는 칸을 줄여(FitRing) 여섯 줄을 한 화면에 세운다.
        _gear.SizeFlagsVertical = SizeFlags.ExpandFill;
        _gear.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

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
        _content.AddChild(_gear);
        _content.AddChild(_rows);

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
        _foot = new HBoxContainer();
        _foot.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _foot.AddChild(_gold);

        if (!Main.Portrait)
        {
            _pager.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            _pageNumber.CustomMinimumSize = new Vector2(36, Cell.Y);
            _gold.AddThemeFontSizeOverride("font_size", 11);
            _foot.AddChild(_pager);
        }

        _foot.AddChild(_loot);
        _foot.AddChild(Tidy);

        body.AddChild(WindowFrame.Head(WindowFrame.Tabs(_packTab, _gearTab), Close));
        body.AddChild(_main);
        body.AddChild(_foot);

        if (!Main.Portrait)
        {
            // 두 탭이 같은 폭을 쓰게 — 소지품 한 장과 줄인 고리가 다르면 탭을 바꿀 때마다 창이 옆으로 움직인다.
            _content.CustomMinimumSize = new Vector2(_gear.CustomMinimumSize.X, 0);
        }

        // 속 여백은 좌우 4 — 세로 장비 고리(328)가 360 화면의 안전 폭(344) 안에 들어야 한다.
        StyleBoxFlat sheet = Greybox.Sheet();
        sheet.ContentMarginLeft = Main.Gutter / 2;
        sheet.ContentMarginRight = Main.Gutter / 2;

        PanelContainer inside = new();
        inside.AddThemeStyleboxOverride("panel", sheet);
        inside.AddChild(body);

        AddChild(inside);
        AddChild(_action);

        ShowTab(Main.OnGear);
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

        _actionName.AddThemeColorOverride("font_color", Greybox.Text);
        _actionName.AddThemeFontSizeOverride("font_size", 13);
        _actionName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _actionName.ClipText = true;
        _actionName.CustomMinimumSize = new Vector2(120, 0);
        _actionLine.AddThemeColorOverride("font_color", Greybox.Muted);
        _actionLine.AddThemeFontSizeOverride("font_size", 11);

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

        _drop.Pressed += () =>
        {
            if (_chosen > 0)
            {
                Dropped?.Invoke(_chosen);
                _action.Visible = false;
            }
        };

        _off.Pressed += () =>
        {
            if (_chosen < 0)
            {
                TakenOff?.Invoke(-_chosen);
                _action.Visible = false;
            }
        };

        HBoxContainer buttons = new();
        buttons.AddThemeConstantOverride("separation", 4);
        buttons.AddChild(_use);
        buttons.AddChild(_drop);
        buttons.AddChild(_off);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(_actionName);
        column.AddChild(_actionLine);
        column.AddChild(buttons);
        _action.AddChild(column);
    }

    private static double Now() => Time.GetTicksMsec() / 1000.0;

    /// <summary>
    /// Shows one tab and hides the other. Nothing is asked of the server — both were already sent, so this
    /// is only which of them is on screen. Tidying is a pack thing, so its button goes with the pack.
    /// </summary>
    internal void ShowTab(bool gear)
    {
        _onGear = gear;

        _gear.Visible = gear;
        _rows.Visible = !gear;
        _pager.Visible = !gear;

        // 소지품 한 장은 제 높이만큼만 아래에 붙고, 장비 고리는 남는 높이를 다 쓴다(GameScreen.Cover).
        SizeFlagsVertical = gear ? SizeFlags.ExpandFill : SizeFlags.ShrinkEnd;
        _gearTab.SetPressedNoSignal(gear);
        _packTab.SetPressedNoSignal(!gear);
        _gearTab.EmitSignal(BaseButton.SignalName.Toggled, gear);
        _packTab.EmitSignal(BaseButton.SignalName.Toggled, !gear);
        Tidy.Visible = !gear;

        // 탭을 옮기면 고른 것이 다른 탭에 있을 수 있다. 놓고 다시 고르게 한다.
        _chosen = 0;
        _showing = null;
        _action.Visible = false;
    }

    /// <summary>The button that shuts the panel, so whoever opened it can decide what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody asked to use a carried thing. The slot is what the server wants.</summary>
    public event System.Action<int>? Used;

    /// <summary>Somebody asked to throw a carried thing away. The server decides whether it may be.</summary>
    public event System.Action<int>? Dropped;

    /// <summary>Somebody asked to take off what is in one worn place. The number is the server's own.</summary>
    public event System.Action<int>? TakenOff;

    /// <summary>The button that pulls everything to the front of the pack.</summary>
    public Button Tidy { get; }

    /// <summary>Shows what is worn and what is carried, and says plainly when there is nothing.</summary>
    public void Show(IReadOnlyList<InventoryItem> carried, IReadOnlyList<WornItem> worn, Character? self = null, long gold = 0)
    {
        _gold.Text = Main.Portrait ? $"금화 {gold:N0} · {carried.Count}/60칸" : $"금화 {gold:N0}\n{carried.Count}/60칸";

        // 종이인형은 목록과 따로 갱신한다 — 차림이 바뀌는 것과 소지품이 바뀌는 것은 같은 일이 아니다.
        _gear.ShowDoll(self);
        FitRows();
        FitRing();

        string wanted = Describe(carried, worn);

        if (wanted == _showing)
        {
            return;
        }

        _showing = wanted;

        _gear.Show(worn, _chosen);
        _carriedCount = carried.Count;
        _page = Paging.Kept(_page, carried.Count, _perPage);
        _pageNumber.Text = $"{_page + 1}/{Paging.Pages(carried.Count, _perPage)}";
        Fill(_rows, Paging.Page(carried, _page, _perPage));

        ShowChosen(carried, worn);
    }

    /// <summary>
    /// Picks the first thing on whichever tab is showing and presses the button beside it — put it on from
    /// the pack, take it off from the gear ring. Only for a run with no hand on it: it goes through the
    /// same events the buttons raise, so the wiring is checked, not bypassed.
    /// </summary>
    public bool PressFirst(bool throwing = false)
    {
        foreach (Node cell in (_onGear ? _gear.Cells : _rows.GetChildren()))
        {
            if (cell is Button button)
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);
                (_onGear ? _off : throwing ? _drop : _use).EmitSignal(BaseButton.SignalName.Pressed);

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
                grid.AddChild(new Control { CustomMinimumSize = Cell, MouseFilter = MouseFilterEnum.Ignore });
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

            // 칸은 평평한 어둠, 고른 칸은 밝은 테두리 — 돌은 칸에 쓰지 않는다(규칙표).
            StyleBoxFlat box = Greybox.Surface();
            box.SetCornerRadiusAll(6);

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
    /// page turner) are measured apart from the rows, so the count does not feed back on itself. Only the pack tab has rows.
    /// </summary>
    private void FitRows()
    {
        if (_onGear)
        {
            return;
        }

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
    /// On its side, sizes the ring's cells so all six rows stand in the window at once (GearLayout.CellThatFits). Upright
    /// the ring keeps its full-size cells — it fits there already.
    /// </summary>
    private void FitRing()
    {
        if (Main.Portrait)
        {
            return;
        }

        float outside = GetCombinedMinimumSize().Y - _main.GetCombinedMinimumSize().Y;
        _gear.Lay(GearLayout.CellThatFits(Room(), outside, GearGrid.Gap, PressableCells));
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
        if (!IsVisibleInTree() || _onGear || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } press)
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
    private void ShowChosen(IReadOnlyList<InventoryItem> carried, IReadOnlyList<WornItem> worn)
    {
        InventoryItem? held = carried.FirstOrDefault(item => item.Slot == _chosen);

        if (held is not null)
        {
            _actionName.Text = held.Name;
            _actionLine.Text = ItemActions.Line(held);
            _actionLine.Visible = _actionLine.Text.Length > 0;
            WindowFrame.Relabel(_use, ItemActions.Primary(held));
            _use.Visible = true;
            _drop.Visible = true;
            _off.Visible = false;
            _action.Visible = true;
            _action.ResetSize();

            return;
        }

        WornItem? gear = worn.FirstOrDefault(one => -one.Slot == _chosen);

        if (gear is not null)
        {
            // 걸친 것은 바로 버릴 수 없다. 벗어서 소지품에 든 다음에야 버릴 것이 생긴다.
            _actionName.Text = gear.Called;
            _actionLine.Text = ItemActions.Line(gear);
            _actionLine.Visible = true;
            _use.Visible = false;
            _drop.Visible = false;
            _off.Visible = true;
            _action.Visible = true;
            _action.ResetSize();

            return;
        }

        _action.Visible = false;
    }

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

        Control? cell = _chosen > 0
            ? _cellsBySlot.GetValueOrDefault(_chosen)
            : _gear.FindChild($"Slot{-_chosen}", true, false) as Control;

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
        _action.GlobalPosition = new Vector2(x, Mathf.Min(y, window.End.Y - size.Y - 4));
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

        if (_onGear || cell is null)
        {
            return false;
        }

        cell.EmitSignal(BaseButton.SignalName.Pressed);

        return true;
    }

    private string Describe(IReadOnlyList<InventoryItem> carried, IReadOnlyList<WornItem> worn) =>
        $"{_chosen}|{_page}|"
        + string.Join(";", carried.Select(item => $"{item.Slot}:{item.Icon}:{item.Stacks}"))
        + "|"
        + string.Join(";", worn.Select(gear => $"{gear.Slot}:{gear.Icon}"));
}
