using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What the character is carrying. A reading panel — nothing here equips, uses or throws anything away;
/// it exists so that picking something up can be seen to have worked.
/// </summary>
/// <remarks>
/// The list is rebuilt only when what it would say changes, because it is asked every frame and a panel
/// that throws its children away sixty times a second cannot be scrolled.
/// </remarks>
public sealed partial class PackPanel : PanelContainer
{
    private readonly VBoxContainer _rows = new() { Name = "Items" };
    private readonly Label _count = new();

    // 빈 문자열로 두면 "가진 것이 없다"는 첫 상태가 "바뀐 것 없음"과 구별되지 않아 안내가 안 나온다.
    private string? _showing;

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

        Close = new Button
        {
            Text = "닫기",
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        head.AddChild(Close);

        // 시안대로 목록이 패널을 넘칠 때만 스크롤을 붙인다. 지금은 넣을 것이 몇 개뿐이라, 스크롤
        // 상자가 남는 높이를 다 먹고 줄이 한 줄도 안 보이는 편이 더 나쁘다.
        _rows.SizeFlagsVertical = SizeFlags.ExpandFill;
        _rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        body.AddChild(head);
        body.AddChild(_rows);
        body.AddChild(_count);

        AddChild(body);
    }

    /// <summary>The button that shuts the panel, so whoever opened it can decide what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody pressed a carried thing. The slot is what the server wants; the rest is its business.</summary>
    public event System.Action<int>? Used;

    /// <summary>Shows what is being carried, or says plainly that nothing is.</summary>
    public void Show(IReadOnlyList<InventoryItem> carried)
    {
        string wanted = Describe(carried);

        if (wanted == _showing)
        {
            return;
        }

        _showing = wanted;

        foreach (Node row in _rows.GetChildren())
        {
            row.QueueFree();
        }

        if (carried.Count == 0)
        {
            _rows.AddChild(new Label
            {
                Text = "가진 것이 없습니다.",
                CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        foreach (InventoryItem item in carried)
        {
            _rows.AddChild(Row(item, () => Used?.Invoke(item.Slot)));
        }

        _count.Text = $"{carried.Count}가지";
        _count.AddThemeColorOverride("font_color", Greybox.Muted);
    }

    /// <summary>
    /// Presses the first carried thing, as a hand would. Only for a run with no hand on it — it goes
    /// through the same button so the wiring is checked, not bypassed.
    /// </summary>
    public bool PressFirst()
    {
        foreach (Node row in _rows.GetChildren())
        {
            if (row is Button button)
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);

                return true;
            }
        }

        return false;
    }

    /// <summary>One carried thing: its picture where there is one, and always its name. Pressing it uses it.</summary>
    private static Control Row(InventoryItem item, System.Action pressed)
    {
        // A button rather than a label: the whole row is the target, which is what a thumb expects.
        Button row = new()
        {
            CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
            Flat = true
        };

        row.Pressed += pressed;

        HBoxContainer line = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.SetAnchorsPreset(LayoutPreset.FullRect);
        line.AddThemeConstantOverride("separation", Main.Gutter);

        // A row with no picture still has to line its name up with the rows that do.
        line.AddChild(new TextureRect
        {
            Texture = ItemIcons.For(item.Icon),
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered
        });

        line.AddChild(new Label
        {
            Text = item.Stacks > 1 ? $"{item.Name} ×{item.Stacks}" : item.Name,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        });

        row.AddChild(line);

        return row;
    }

    private static string Describe(IReadOnlyList<InventoryItem> carried)
    {
        string said = string.Empty;

        foreach (InventoryItem item in carried)
        {
            said += $"{item.Slot}:{item.Name}:{item.Stacks};";
        }

        return said;
    }
}
