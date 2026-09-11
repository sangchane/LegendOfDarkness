using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What the character has on and what they are carrying, as the original showed it: a grid of pictures
/// with no names in it. One thing is picked out at a time, and its name and what can be done with it are
/// written underneath — a line of text per item eats a phone screen, and names here run past thirty letters.
/// </summary>
/// <remarks>
/// The lists are rebuilt only when what they would show changes, because they are asked every frame and a
/// panel that throws its children away sixty times a second cannot be pressed.
/// </remarks>
public sealed partial class PackPanel : PanelContainer
{
    // 원작은 33x36 칸이었다. 손가락은 그보다 커서 시안의 최소 터치 크기를 쓴다.
    private static readonly Vector2 Cell = new(Main.TouchMinimum, Main.TouchMinimum);

    private readonly GridContainer _gear = new() { Name = "Worn" };
    private readonly GridContainer _rows = new() { Name = "Items" };
    private readonly Label _chosenName = new();
    private readonly Button _use = new() { Text = "착용" };
    private readonly Button _drop = new() { Text = "버리기" };

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
        head.AddChild(new Label { Text = "인벤토리", SizeFlagsVertical = SizeFlags.ShrinkCenter });
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        Tidy = new Button { Text = "정렬", CustomMinimumSize = Cell };
        head.AddChild(Tidy);

        Close = new Button { Text = "닫기", CustomMinimumSize = Cell };
        head.AddChild(Close);

        // 세로는 패널이 전폭이라 한 줄에 여섯, 가로는 오른쪽 3분의 1 남짓이라 넷이 들어간다.
        _gear.Columns = Main.Portrait ? 6 : 4;
        _rows.Columns = _gear.Columns;

        VBoxContainer inside = new();
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        inside.AddChild(Heading("장비"));
        inside.AddChild(_gear);
        inside.AddChild(Heading("소지품"));
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

        // 걸친 것은 음수로 두므로, 양수일 때만 쓸 것이 골라져 있다는 뜻이다.
        _use.Pressed += () =>
        {
            if (_chosen > 0)
            {
                Used?.Invoke(_chosen);
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
    }

    /// <summary>The button that shuts the panel, so whoever opened it can decide what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody asked to use a carried thing. The slot is what the server wants.</summary>
    public event System.Action<int>? Used;

    /// <summary>Somebody asked to throw a carried thing away. The server decides whether it may be.</summary>
    public event System.Action<int>? Dropped;

    /// <summary>The button that pulls everything to the front of the pack.</summary>
    public Button Tidy { get; }

    /// <summary>Shows what is worn and what is carried, and says plainly when there is nothing.</summary>
    public void Show(IReadOnlyList<InventoryItem> carried, IReadOnlyList<WornItem> worn)
    {
        string wanted = Describe(carried, worn);

        if (wanted == _showing)
        {
            return;
        }

        _showing = wanted;

        Fill(_gear, worn.Select(gear => (Key: -gear.Slot, gear.Icon)));
        Fill(_rows, carried.Select(item => (Key: item.Slot, item.Icon)));

        ShowChosen(carried, worn);
    }

    /// <summary>
    /// Picks the first carried thing and asks to use it, as a hand would. Only for a run with no hand on
    /// it — it goes through the same event the button raises, so the wiring is checked, not bypassed.
    /// </summary>
    public bool PressFirst()
    {
        foreach (Node cell in _rows.GetChildren())
        {
            if (cell is Button button)
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);
                _use.EmitSignal(BaseButton.SignalName.Pressed);

                return true;
            }
        }

        return false;
    }

    private static Label Heading(string text)
    {
        Label heading = new() { Text = text };
        heading.AddThemeColorOverride("font_color", Greybox.Muted);

        return heading;
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
            _use.Visible = true;
            _drop.Visible = true;

            return;
        }

        WornItem? gear = worn.FirstOrDefault(one => -one.Slot == _chosen);

        if (gear is not null)
        {
            _chosenName.Text = $"{WornPlace.Of(gear.Slot)} · {gear.Called}";
            _use.Visible = false;
            _drop.Visible = false;

            return;
        }

        _chosenName.Text = carried.Count == 0 && worn.Count == 0
            ? "가진 것이 없습니다."
            : $"{carried.Count}가지 · 걸친 것 {worn.Count}";

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
