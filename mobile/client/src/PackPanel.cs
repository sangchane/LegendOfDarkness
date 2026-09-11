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
/// </remarks>
public sealed partial class PackPanel : PanelContainer
{
    // 원작은 33x36 칸이었다. 손가락은 그보다 커서 시안의 최소 터치 크기를 쓴다.
    private static readonly Vector2 Cell = new(Main.TouchMinimum, Main.TouchMinimum);

    private readonly GearGrid _gear = new();
    private readonly GridContainer _rows = new() { Name = "Items" };
    private readonly Button _gearTab = new() { Text = "장비", ToggleMode = true };
    private readonly Button _packTab = new() { Text = "소지품", ToggleMode = true };
    private readonly Label _chosenName = new();
    private readonly Button _use = new() { Text = "입기" };
    private readonly Button _drop = new() { Text = "버리기" };

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
        AddThemeStyleboxOverride("panel", Greybox.Plate());

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter);

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter);

        _gearTab.CustomMinimumSize = Cell;
        _packTab.CustomMinimumSize = Cell;
        _gearTab.Pressed += () => ShowTab(gear: true);
        _packTab.Pressed += () => ShowTab(gear: false);

        head.AddChild(_gearTab);
        head.AddChild(_packTab);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        Tidy = new Button { Text = "정렬", CustomMinimumSize = Cell };
        head.AddChild(Tidy);

        Close = new Button { Text = "닫기", CustomMinimumSize = Cell };
        head.AddChild(Close);

        // 세로는 패널이 전폭이라 한 줄에 여섯, 가로는 오른쪽 3분의 1 남짓이라 넷이 들어간다.
        _rows.Columns = Main.Portrait ? 6 : 4;

        // 걸친 것을 고르는 것은 소지품과 같은 한 자리를 쓴다. 음수로 두어 칸 번호와 구별한다.
        _gear.Chosen += slot =>
        {
            _chosen = -slot;
            _showing = null;
        };

        VBoxContainer inside = new();
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        inside.AddChild(_gear);
        inside.AddChild(_rows);

        // 칸이 늘어 패널이 화면을 넘으면 제목과 닫기 버튼이 밀려난다(한 번 그렇게 됐다).
        // 넘치는 것은 스크롤로 두고, 머리와 꼬리는 언제나 남긴다.
        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };

        scroll.AddChild(inside);

        _chosenName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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

        HBoxContainer foot = new();
        foot.AddThemeConstantOverride("separation", Main.Gutter);
        foot.AddChild(_chosenName);
        foot.AddChild(_use);
        foot.AddChild(_drop);

        body.AddChild(head);
        body.AddChild(scroll);
        body.AddChild(foot);

        AddChild(body);

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
        _gearTab.ButtonPressed = gear;
        _packTab.ButtonPressed = !gear;
        Tidy.Visible = !gear;

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
    public void Show(IReadOnlyList<InventoryItem> carried, IReadOnlyList<WornItem> worn, Character? self = null)
    {
        // 종이인형은 목록과 따로 갱신한다 — 차림이 바뀌는 것과 소지품이 바뀌는 것은 같은 일이 아니다.
        _gear.ShowDoll(self);

        string wanted = Describe(carried, worn);

        if (wanted == _showing)
        {
            return;
        }

        _showing = wanted;

        _gear.Show(worn, _chosen);
        Fill(_rows, carried.Select(item => (Key: item.Slot, item.Icon)));

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

    /// <summary>Rebuilds one grid: one pressable picture per thing, and the picked one outlined.</summary>
    private void Fill(GridContainer grid, IEnumerable<(int Key, int Icon)> things)
    {
        foreach (Node cell in grid.GetChildren())
        {
            cell.QueueFree();
        }

        bool any = false;

        foreach ((int key, int icon) in things)
        {
            any = true;

            Button cell = new()
            {
                CustomMinimumSize = Cell,
                Icon = ItemIcons.For(icon),
                ExpandIcon = true,
                Flat = key != _chosen
            };

            cell.Pressed += () =>
            {
                _chosen = key;

                // 테두리를 옮기려면 다시 그려야 한다. 다음 프레임의 Show 가 하도록 표시만 지운다.
                _showing = null;
            };

            grid.AddChild(cell);
        }

        if (!any)
        {
            grid.AddChild(new Label { Text = "없음", CustomMinimumSize = Cell });
        }
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
        $"{_chosen}|"
        + string.Join(";", carried.Select(item => $"{item.Slot}:{item.Icon}:{item.Stacks}"))
        + "|"
        + string.Join(";", worn.Select(gear => $"{gear.Slot}:{gear.Icon}"));
}
