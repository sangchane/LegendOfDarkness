using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What the character has on and what they are carrying, as the original showed it: pictures with no names
/// in them. One thing is picked out at a time, and its name and what can be done with it are written
/// underneath — a line of text per item eats a phone screen, and names here run past thirty letters.
/// </summary>
/// <remarks>
/// The two are separate tabs, not one list above another, because the original kept them in separate
/// windows and stacking them pushed the pack off the screen as the worn places filled up. The gear tab is
/// the original's own ring of places (<see cref="GearGrid" />); the pack tab is a plain grid of pictures.
/// Both are rebuilt only when what they would show changes, because they are asked every frame and a panel
/// that throws its children away sixty times a second cannot be pressed.
///
/// The pack shows one page at a time and turns left and right — by a swipe across the pictures or by the arrows, since
/// no action may need a swipe alone (wireframes 2.2). Sixty pictures in a scrolling list covered nearly the whole
/// portrait screen (사용자, 2026-09-18); a page sits at the bottom and leaves the map above it.
/// </remarks>
public sealed partial class PackPanel : PanelContainer
{
    // 원작은 33x36 칸이었다. 손가락은 그보다 커서 시안의 최소 터치 크기를 쓴다.
    private static readonly Vector2 Cell = new(Main.TouchMinimum, Main.TouchMinimum);

    // 한 장에 6열, 네 줄까지. 창이 받은 높이에 들어가는 만큼만 둔다(FitRows) — 돌 제목줄이 붙은 뒤로 가로 360 에서
    // 두 줄이면 입기 줄이 화면 밑으로 빠졌다. 가로 창이 화면 높이를 다 쓰게 된 뒤로는 가로에도 네 줄이 든다.
    private const int Columns = 6;
    private const int MostRows = 4;
    private int _perPage = Columns * MostRows;

    // 가로 장비 고리의 칸. 44 를 먼저 노리고, 안 되면 40, 36 까지 — 그 아래는 손가락이 못 누른다(사용자·조정자, 2026-09-23).
    private static readonly int[] PressableCells = [44, 40, 36];

    /// <summary>How far a finger has to travel across the pictures before it counts as turning the page.</summary>
    private const float SwipeDistance = Main.TouchMinimum;

    private readonly GearGrid _gear = new();
    private readonly GridContainer _rows = new() { Name = "Items" };
    private readonly Button _gearTab = new() { Text = "장비", ToggleMode = true };
    private readonly Button _packTab = new() { Text = "소지품", ToggleMode = true };
    private readonly Label _chosenName = new();

    /// <summary>The name on the stone strip — which of the two tabs is open.</summary>
    private Label _title = null!;

    /// <summary>밟은 것을 알아서 줍는지 켜고 끄는 단추.</summary>
    private Button _loot = null!;

    private void ShowLoot() =>
        _loot.Text = Main.AutoLoot ? "줍기 켬" : "줍기 끔";
    private readonly Label _gold = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button _use = new() { Text = "입기" };
    private readonly Button _drop = new() { Text = "버리기" };

    // 탭의 내용(장비 고리 또는 소지품 한 장과 장 넘김), 그리고 그것이 선 줄. 가로에서는 그 줄에 탭·입기 기둥이 옆에 선다.
    private readonly VBoxContainer _content = new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill
    };

    private readonly HBoxContainer _main = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly HBoxContainer _pager = new();
    private readonly Label _pageNumber = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

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
        body.AddThemeConstantOverride("separation", Main.Gutter);

        // 세로는 탭·줍기·정렬·닫기가 한 줄로 창 위에, 가로는 두 칸씩 고리 옆 기둥에 선다.
        Container head = Main.Portrait ? new HBoxContainer() : new GridContainer { Columns = 2 };
        head.AddThemeConstantOverride("separation", Main.Gutter);
        head.AddThemeConstantOverride("h_separation", Main.Gutter);
        head.AddThemeConstantOverride("v_separation", Main.Gutter / 2);

        _gearTab.CustomMinimumSize = Cell;
        _packTab.CustomMinimumSize = Cell;
        Greybox.Tab(_gearTab);
        Greybox.Tab(_packTab);

        // 창마다 확정 단추는 하나뿐이다 — 전부 돌로 하면 아무것도 돋보이지 않는다.
        Greybox.Commit(_use);
        Greybox.Plain(_drop);
        _gearTab.Pressed += () => ShowTab(gear: true);
        _packTab.Pressed += () => ShowTab(gear: false);

        head.AddChild(_gearTab);
        head.AddChild(_packTab);

        if (Main.Portrait)
        {
            head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        }

        // 밟은 것을 알아서 주울지. 원작에는 없던 것이라 끌 수 있어야 한다(사용자, 2026-09-19).
        _loot = new Button { CustomMinimumSize = Cell, ToggleMode = true, ButtonPressed = Main.AutoLoot };
        Greybox.Tab(_loot);
        ShowLoot();

        _loot.Pressed += () =>
        {
            Main.SetAutoLoot(_loot.ButtonPressed);
            ShowLoot();
        };

        head.AddChild(_loot);

        Tidy = new Button { Text = "정렬", CustomMinimumSize = Cell };
        Greybox.Plain(Tidy);
        head.AddChild(Tidy);

        Close = new Button { Text = "닫기", CustomMinimumSize = Cell };
        Greybox.Plain(Close);
        head.AddChild(Close);

        _rows.Columns = Columns;

        // 걸친 것을 고르는 것은 소지품과 같은 한 자리를 쓴다. 음수로 두어 칸 번호와 구별한다.
        _gear.Chosen += slot =>
        {
            _chosen = -slot;
            _showing = null;
        };

        // 장비 고리는 장으로 나눌 수도, 굴릴 수도 없다 — 가로에서 굴려 내리게 했더니 불편해서 못 쓴다고 했다(사용자,
        // 2026-09-23). 가로는 칸을 줄여(FitRing) 여섯 줄을 한 화면에 세운다.
        _gear.SizeFlagsVertical = SizeFlags.ExpandFill;

        Button back = new() { Text = "◀", CustomMinimumSize = Cell };
        Button forward = new() { Text = "▶", CustomMinimumSize = Cell };
        back.Pressed += () => Turn(-1);
        forward.Pressed += () => Turn(1);
        _pageNumber.CustomMinimumSize = Cell;
        _pager.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _pager.AddChild(back);
        _pager.AddChild(_pageNumber);
        _pager.AddChild(forward);

        _chosenName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _chosenName.MaxLinesVisible = 2;
        _chosenName.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _use.CustomMinimumSize = Cell;
        _use.Visible = false;

        // 한 버튼이 둘을 한다. 고른 것이 소지품이면 입고, 걸친 것이면 벗는다 — 둘이 동시에 골라지는
        // 일이 없으므로 버튼을 둘 둘 이유가 없다. 걸친 것은 음수로 두어 어느 쪽인지 가린다.
        _use.Pressed += () =>
        {
            if (_chosen > 0)
            {
                Used?.Invoke(_chosen);
            }
            else if (_chosen < 0)
            {
                TakenOff?.Invoke(-_chosen);
            }
        };

        _drop.CustomMinimumSize = Cell;
        _drop.Visible = false;

        _drop.Pressed += () =>
        {
            if (_chosen > 0)
            {
                Dropped?.Invoke(_chosen);
            }
        };

        // 입기 단추가 뜨기 전에도 그 높이를 잡아 둔다. 안 그러면 고르는 순간 창이 28 자라 화면 밑으로 빠진다.
        // 세로는 이름 옆에 단추, 가로는 좁은 기둥이라 이름 아래에 단추.
        HBoxContainer buttons = new() { CustomMinimumSize = new Vector2(0, Cell.Y) };
        buttons.AddThemeConstantOverride("separation", Main.Gutter);
        BoxContainer foot = Main.Portrait ? buttons : new VBoxContainer();
        foot.AddThemeConstantOverride("separation", Main.Gutter);
        foot.AddChild(_chosenName);

        if (!Main.Portrait)
        {
            foot.AddChild(buttons);
        }

        buttons.AddChild(_use);
        buttons.AddChild(_drop);

        // 돌 제목줄 — 어느 창인지와 지금 가진 금화를 늘 같은 자리에서 본다. 어두운 돌 위라 글자는 밝은 쪽이다.
        HBoxContainer naming = new();
        naming.AddThemeConstantOverride("separation", Main.Gutter);

        _title = new Label { Text = "소지품" };
        _title.AddThemeColorOverride("font_color", Greybox.Title);
        naming.AddChild(_title);
        naming.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _gold.AddThemeColorOverride("font_color", Greybox.Title);
        naming.AddChild(_gold);

        // 원작도 금화를 소지품 창에 적었다. 상점에서 사기 전에 볼 곳이 여기다. 머리 줄에 두면 세로 360 에서
        // 탭·정렬·닫기와 함께 넘친다(한 번 그렇게 됐다) — 장 넘김과 한 줄.
        HBoxContainer turning = new();
        turning.AddChild(_pager);

        _content.AddThemeConstantOverride("separation", Main.Gutter);
        _content.AddChild(_gear);
        _content.AddChild(_rows);
        _content.AddChild(turning);

        _main.AddThemeConstantOverride("separation", Main.Gutter);
        _main.AddChild(_content);

        body.AddChild(Greybox.Header(naming));

        if (Main.Portrait)
        {
            body.AddChild(head);
            body.AddChild(_main);
            body.AddChild(foot);
        }
        else
        {
            // 가로 기둥: 위에 탭, 아래에 고른 것과 입기 — 고리가 창 높이를 다 쓰도록 머리 줄과 꼬리 줄을 옆으로 뺐다.
            VBoxContainer side = new();
            side.AddThemeConstantOverride("separation", Main.Gutter);
            side.AddChild(head);
            side.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
            side.AddChild(foot);

            foreach (Control button in new Control[] { _gearTab, _packTab, _loot, Tidy, Close, _use, _drop })
            {
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            }

            _main.AddChild(side);
            body.AddChild(_main);

            // 두 탭이 같은 폭을 쓰게 — 소지품 한 장(308)과 줄인 고리가 다르면 탭을 바꿀 때마다 창이 옆으로 움직인다.
            // 온 크기 고리(312)를 잡아 두면 둘 다 들어간다.
            _content.CustomMinimumSize = new Vector2(_gear.CustomMinimumSize.X, 0);
        }

        PanelContainer inside = new();
        inside.AddThemeStyleboxOverride("panel", Greybox.Sheet());
        inside.AddChild(body);

        AddChild(inside);

        ShowTab(Main.OnGear);
    }

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
        _gearTab.ButtonPressed = gear;
        _packTab.ButtonPressed = !gear;
        Tidy.Visible = !gear;
        _title.Text = gear ? "장비" : "소지품";

        // 탭을 옮기면 고른 것이 다른 탭에 있을 수 있다. 놓고 다시 고르게 한다.
        _chosen = 0;
        _showing = null;
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
        _gold.Text = $"금화 {gold:N0}";

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
                (throwing ? _drop : _use).EmitSignal(BaseButton.SignalName.Pressed);

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
                Flat = key != _chosen
            };

            cell.Pressed += () =>
            {
                if (_swiped)
                {
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

    /// <summary>Writes out whatever is picked, and offers to put it on when it is not on already.</summary>
    private void ShowChosen(IReadOnlyList<InventoryItem> carried, IReadOnlyList<WornItem> worn)
    {
        InventoryItem? held = carried.FirstOrDefault(item => item.Slot == _chosen);

        if (held is not null)
        {
            _chosenName.Text = held.Stacks > 1 ? $"{held.Name} ×{held.Stacks}" : held.Name;
            _use.Text = "입기";
            _use.Visible = true;
            _drop.Visible = true;

            return;
        }

        WornItem? gear = worn.FirstOrDefault(one => -one.Slot == _chosen);

        if (gear is not null)
        {
            _chosenName.Text = $"{WornPlace.Of(gear.Slot)} · {gear.Called}";
            _use.Text = "벗기";
            _use.Visible = true;

            // 걸친 것은 바로 버릴 수 없다. 벗어서 소지품에 든 다음에야 버릴 것이 생긴다.
            _drop.Visible = false;

            return;
        }

        _chosenName.Text = _onGear
            ? worn.Count == 0 ? "걸친 것이 없습니다." : $"걸친 것 {worn.Count}가지"
            : carried.Count == 0 ? "가진 것이 없습니다." : $"{carried.Count}가지";

        _chosenName.AddThemeColorOverride("font_color", Greybox.Muted);
        _use.Visible = false;
        _drop.Visible = false;
    }

    private string Describe(IReadOnlyList<InventoryItem> carried, IReadOnlyList<WornItem> worn) =>
        $"{_chosen}|{_page}|"
        + string.Join(";", carried.Select(item => $"{item.Slot}:{item.Icon}:{item.Stacks}"))
        + "|"
        + string.Join(";", worn.Select(gear => $"{gear.Slot}:{gear.Icon}"));
}
