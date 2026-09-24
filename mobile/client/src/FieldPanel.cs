using System;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 월드맵 창: 갈 수 있는 곳의 이름을 줄로 세운다. 그림 위에 점을 찍는 원작 모습은 아직이고,
/// 지금은 고를 수만 있으면 된다.
/// </summary>
/// <remarks>
/// 닫을 수 있다(<see cref="Close"/>). 이 창이 열려 있는 동안 서버는 고르기 말고 이 접속의 패킷을 모두
/// 버리므로(`NetworkServer.cs:141`), 화면만 숨기면 손이 묶인 채다 — 닫을 때 서버에 취소(0x3F, 맵 번호 0)를
/// 보내야 조작이 돌아온다.
/// </remarks>
public sealed partial class FieldPanel : PanelContainer
{
    private readonly Label _title = new() { Text = "어디로 갈까" };
    private readonly VBoxContainer _places = new();

    public FieldPanel()
    {
        Name = "Field";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        _places.AddThemeConstantOverride("separation", Main.Gutter / 2);

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(_title);

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        head.AddChild(Close);

        ScrollContainer scroll = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(_places);

        inside.AddChild(head);
        inside.AddChild(scroll);

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", Main.Gutter);
        margin.AddThemeConstantOverride("margin_right", Main.Gutter);
        margin.AddThemeConstantOverride("margin_top", Main.Gutter);
        margin.AddThemeConstantOverride("margin_bottom", Main.Gutter);
        margin.AddChild(inside);

        AddChild(margin);
    }

    /// <summary>창을 그냥 닫는다. 서버에 "취소"를 보내야 조작이 돌아온다 — 화면만 숨기면 손이 묶인 채다.</summary>
    public Button Close { get; }

    /// <summary>고른 곳의 맵 번호.</summary>
    public event Action<int>? Chosen;

    /// <summary>창을 채우고 보인다. 창은 늘 통째로 다시 짓는다 — 월드맵은 한 번에 하나뿐이다.</summary>
    public void Show(WorldMapInfo field)
    {
        foreach (Node old in _places.GetChildren())
        {
            _places.RemoveChild(old);
            old.QueueFree();
        }

        foreach (WorldMapNode place in field.Nodes)
        {
            Button row = new()
            {
                Text = place.Name,
                CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
            };

            int area = place.AreaId;
            row.Pressed += () => Chosen?.Invoke(area);

            _places.AddChild(row);
        }

        Visible = true;
    }

    /// <summary>그 이름의 줄 — 손 없이 확인할 때(<c>--map-go</c>) 누른다.</summary>
    public Button? RowNamed(string name) => _places.GetChildren().OfType<Button>().FirstOrDefault(row => row.Text == name);
}
