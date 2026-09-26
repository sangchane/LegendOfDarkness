using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 봇 장비창 — 봇 칸(<see cref="PartyColumn" />)을 누르면 열린다. 장비 고리는 내 장비창과 같은 부품(<see cref="GearGrid" />),
/// 그 아래(가로는 옆)에 봇 가방의 포션 개수와, 고른 자리에 할 수 있는 일: 입은 것이면 [벗기기], 아니면 내 가방에서 입혀 볼
/// 장비 목록, 그리고 봇에게 넘길 내 포션(한 번에 5개 · 전부).
/// </summary>
/// <remarks>
/// 봇이 입을 수 있는지(레벨·직업·성별)는 서버만 안다 — 여기선 내구가 있는 것을 다 보이고, 안 맞으면 서버 알림으로 까닭을 듣는다
/// (<see cref="BotKit" />). 원작 4.51 규칙(docs/original-ui-451.md): 돌 틀·평평한 어두운 속, 확정 단추 하나만 밝은 돌.
/// </remarks>
public sealed partial class BotGearPanel : PanelContainer
{
    private readonly GearGrid _gear = new();
    private readonly Label _title = new() { Text = "봇 장비", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _potions = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _hint = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _choices = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    private int _chosen;
    private string _drawn = string.Empty;

    public BotGearPanel()
    {
        Name = "BotGear";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        Greybox.Plain(Close);

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter);
        head.AddChild(_title);
        head.AddChild(Close);

        _potions.AddThemeColorOverride("font_color", Greybox.Text);
        _potions.AddThemeFontSizeOverride("font_size", 13);
        _hint.AddThemeColorOverride("font_color", Greybox.Muted);
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _choices.AddThemeConstantOverride("separation", Main.Gutter / 2);

        _gear.Chosen += slot =>
        {
            _chosen = slot;
            _drawn = string.Empty;
        };

        VBoxContainer side = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        side.AddThemeConstantOverride("separation", Main.Gutter / 2);
        side.AddChild(_potions);
        side.AddChild(_hint);

        ScrollContainer scroll = new()
        {
            CustomMinimumSize = Main.Portrait ? new Vector2(0, 150) : new Vector2(200, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        scroll.AddChild(_choices);
        side.AddChild(scroll);

        // 세로는 고리 아래에 목록, 가로는 고리 옆에 — 가로 화면은 낮아 아래로는 안 들어간다.
        BoxContainer body = Main.Portrait ? new VBoxContainer() : new HBoxContainer();
        body.AddThemeConstantOverride("separation", Main.Gutter);
        body.AddChild(_gear);
        body.AddChild(side);

        if (!Main.Portrait)
        {
            _gear.Lay(40);
        }

        VBoxContainer inside = new();
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        inside.AddChild(head);
        inside.AddChild(body);

        MarginContainer margin = new();

        foreach (string edge in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(edge, Main.Gutter);
        }

        margin.AddChild(inside);
        AddChild(margin);
    }

    public Button Close { get; }

    /// <summary>내 가방 한 칸을 봇에게 — 칸 번호와 개수(0 은 다).</summary>
    public event System.Action<int, int>? Given;

    /// <summary>봇의 장비 한 자리를 내 가방으로.</summary>
    public event System.Action<int>? TakenOff;

    /// <summary>손 없이 확인할 때 한 자리를 고른다.</summary>
    public void Choose(int slot)
    {
        _chosen = slot;
        _drawn = string.Empty;
    }

    /// <summary>
    /// 봇이 입은 것·봇 가방 포션·내 가방으로 창을 다시 그린다. 같은 것이면 목록은 그대로 둔다(누르는 중인 단추를 지우지 않게).
    /// </summary>
    public void Show(string name, CompanionKit? kit, IReadOnlyList<InventoryItem> pack, Character? doll)
    {
        _title.Text = $"봇 장비 · {name}";
        _gear.ShowDoll(doll);
        _gear.Show(kit?.Worn ?? [], -_chosen);
        _potions.Text = $"봇 가방: {BotKit.Summary(kit)}";

        WornItem? worn = BotKit.At(kit, _chosen);
        string drawn = $"{_chosen}|{worn?.Name}|{string.Join(",", pack.Select(one => $"{one.Slot}{one.Name}{one.Stacks}"))}";

        if (drawn == _drawn)
        {
            return;
        }

        _drawn = drawn;

        foreach (Node old in _choices.GetChildren())
        {
            old.QueueFree();
        }

        if (worn is not null)
        {
            _hint.Text = $"{worn.Called} — 벗기면 내 가방으로 (기본 장비는 못 벗긴다)";
            Button off = new() { Text = "벗기기", CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
            Greybox.Commit(off);
            int place = _chosen;
            off.Pressed += () => TakenOff?.Invoke(place);
            _choices.AddChild(off);
        }
        else
        {
            _hint.Text = _chosen == 0 ? "자리를 누르면 입힐 수 있는 것" : $"{WornPlace.Of(_chosen)} — 내 가방에서 입힐 것";
        }

        foreach (InventoryItem item in BotKit.Wearables(pack))
        {
            _choices.AddChild(ItemRow(item, "입히기", 0));
        }

        foreach (InventoryItem item in BotKit.Potions(pack))
        {
            _choices.AddChild(ItemRow(item, $"{BotKit.PotionHandful}개", BotKit.PotionHandful, all: true));
        }

        if (_choices.GetChildCount() == 0)
        {
            _choices.AddChild(new Label { Text = "내 가방에 줄 것이 없습니다", Modulate = Greybox.Muted });
        }
    }

    /// <summary>한 줄: 그림 · 이름(개수) · 단추 하나(포션은 둘 — 5개 · 다).</summary>
    private Control ItemRow(InventoryItem item, string action, int count, bool all = false)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);

        TextureRect icon = new()
        {
            Texture = ItemIcons.For(item.Icon),
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };

        Label named = new()
        {
            Text = item.Stacks > 1 ? $"{item.Name} {item.Stacks}" : item.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        named.AddThemeFontSizeOverride("font_size", 13);

        row.AddChild(icon);
        row.AddChild(named);
        row.AddChild(Press(action, () => Given?.Invoke(item.Slot, count)));

        if (all)
        {
            row.AddChild(Press("다", () => Given?.Invoke(item.Slot, 0)));
        }

        return row;
    }

    private static Button Press(string text, System.Action pressed)
    {
        Button button = new() { Text = text, CustomMinimumSize = new Vector2(56, Main.TouchMinimum) };
        Greybox.Plain(button);
        button.Pressed += pressed;
        return button;
    }
}
