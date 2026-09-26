using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 월드맵 창: 갈 수 있는 곳마다 카드 한 장 — 곳 이름을 크게, 그 아래 마을/사냥터 · 도착하는 맵 · 입장 레벨(1보다 클 때만).
/// 서버는 이름과 맵 번호만 주고, 나머지는 <c>guide.txt</c> 의 <c>area</c> 줄에서 온다(<see cref="WorldMapCards" />). 모르는 곳은
/// 이름만 적는다 — 지어내지 않는다.
/// </summary>
/// <remarks>
/// 닫을 수 있다(<see cref="Close"/>, 오른쪽 위 X). 이 창이 열려 있는 동안 서버는 고르기 말고 이 접속의 패킷을 모두
/// 버리므로(`NetworkServer.cs:141`), 화면만 숨기면 손이 묶인 채다 — 닫을 때 서버에 취소(맵 번호 0)를 보내야 조작이
/// 돌아온다. 주고받는 것(0xF0 · FieldChoice)은 줄 목록이던 때와 같다.
/// </remarks>
public sealed partial class FieldPanel : PanelContainer
{
    private readonly MapGuide _guide;
    private readonly GridContainer _places = new();
    private readonly GridContainer _fields = new();
    private readonly Button _townTab = WindowFrame.IconButton(GlyphKind.Town, "마을", tab: true, width: 56);
    private readonly Button _fieldTab = WindowFrame.IconButton(GlyphKind.Field, "사냥터", tab: true, width: 56);
    private readonly Dictionary<Button, string> _names = [];

    public FieldPanel(MapGuide guide)
    {
        _guide = guide;
        Name = "Field";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);

        // 세로는 두 장씩, 가로는 세 장씩 — 가로 화면은 낮고 넓다.
        _places.Columns = Main.Portrait ? 2 : 3;
        _places.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _places.AddThemeConstantOverride("h_separation", Main.Gutter);
        _places.AddThemeConstantOverride("v_separation", Main.Gutter);

        // 사냥터 탭의 카드 칸 — 마을 칸과 같은 모양(2026-09-26: 사냥터랑 마을 구분 탭).
        _fields.Columns = _places.Columns;
        _fields.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _fields.AddThemeConstantOverride("h_separation", Main.Gutter);
        _fields.AddThemeConstantOverride("v_separation", Main.Gutter);
        _townTab.Pressed += () => ShowTab(towns: true);
        _fieldTab.Pressed += () => ShowTab(towns: false);

        Close = WindowFrame.CloseButton();

        // 세로 창은 제 높이만큼만 선다 — 굴림 칸은 속을 제 크기로 올려 보내지 않으니 세 줄(여섯 곳)이 드는 높이를 준다.
        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, Main.Portrait ? (72 * 3) + (Main.Gutter * 2) : 0)
        };
        VBoxContainer both = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        both.AddChild(_places);
        both.AddChild(_fields);
        scroll.AddChild(both);

        // 제목 자리에 두 탭 — [마을] · [사냥터](공통 창 틀의 아이콘 탭). 제목 글자는 탭 옆에 작게.
        HBoxContainer left = new();
        left.AddThemeConstantOverride("separation", Main.Gutter);
        left.AddChild(WindowFrame.Tabs(_townTab, _fieldTab));
        Label title = WindowFrame.Title("월드맵");
        title.AddThemeColorOverride("font_color", Greybox.Muted);
        left.AddChild(title);
        inside.AddChild(WindowFrame.Head(left, Close));
        inside.AddChild(scroll);

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", Main.Gutter);
        margin.AddThemeConstantOverride("margin_right", Main.Gutter);
        margin.AddThemeConstantOverride("margin_top", Main.Gutter / 2);
        margin.AddThemeConstantOverride("margin_bottom", Main.Gutter);
        margin.AddChild(inside);

        PanelContainer sheet = new();
        sheet.AddThemeStyleboxOverride("panel", Greybox.Sheet());
        sheet.AddChild(margin);
        AddChild(sheet);
    }

    /// <summary>창을 그냥 닫는다. 서버에 "취소"를 보내야 조작이 돌아온다 — 화면만 숨기면 손이 묶인 채다.</summary>
    public Button Close { get; }

    /// <summary>고른 곳의 맵 번호.</summary>
    public event Action<int>? Chosen;

    /// <summary>창을 채우고 보인다. 창은 늘 통째로 다시 짓는다 — 월드맵은 한 번에 하나뿐이다.</summary>
    /// <remarks>
    /// 카드를 [마을] · [사냥터] 두 칸에 나눠 담는다(<see cref="WorldMapCards.Towns" /> · <see cref="WorldMapCards.Fields" />). 처음 보이는
    /// 탭은 지금 선 곳의 종류다 — 마을에 서 있으면 [마을], 사냥터면 [사냥터](그 탭이 비었으면 다른 쪽).
    /// </remarks>
    public void Show(WorldMapInfo field, string standingIn = "")
    {
        foreach (GridContainer grid in new[] { _places, _fields })
        {
            foreach (Node old in grid.GetChildren())
            {
                grid.RemoveChild(old);
                old.QueueFree();
            }
        }

        _names.Clear();
        IReadOnlyList<WorldMapCard> cards = WorldMapCards.From(field, _guide);

        foreach ((GridContainer grid, IReadOnlyList<WorldMapCard> list) in new[] { (_places, WorldMapCards.Towns(cards)), (_fields, WorldMapCards.Fields(cards)) })
        {
            foreach (WorldMapCard card in list)
            {
                Button face = Card(card);
                int area = card.AreaId;
                face.Pressed += () => Chosen?.Invoke(area);
                _names[face] = card.Name;
                grid.AddChild(face);
            }
        }

        ShowTab(WorldMapCards.OpensOnTowns(standingIn, cards));
        Visible = true;
    }

    /// <summary>한 탭만 보인다 — 마을이면 true.</summary>
    public void ShowTab(bool towns)
    {
        _places.Visible = towns;
        _fields.Visible = !towns;
        _townTab.SetPressedNoSignal(towns);
        _fieldTab.SetPressedNoSignal(!towns);
        _townTab.EmitSignal(BaseButton.SignalName.Toggled, towns);
        _fieldTab.EmitSignal(BaseButton.SignalName.Toggled, !towns);
    }

    /// <summary>그 이름의 카드 — 손 없이 확인할 때(<c>--map-go</c>) 누른다.</summary>
    public Button? RowNamed(string name) => _names.FirstOrDefault(pair => pair.Value == name).Key;

    /// <summary>
    /// 한 장: 어두운 칸(돌 아님 — 목록은 돌을 쓰지 않는다) 위에 이름, 그 아래 작은 글씨로 종류 · 도착 · 레벨. 마을은
    /// 강조색 띠, 사냥터는 빨강 띠 — 고르기 전에 한눈에 갈린다.
    /// </summary>
    private static Button Card(WorldMapCard card)
    {
        Button face = new()
        {
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            ClipContents = true
        };

        Color stripe = card.Kind switch { "마을" => Greybox.Accent, "사냥터" => Greybox.Gone, _ => Greybox.Muted };

        foreach ((string state, Color back) in new[] { ("normal", new Color("#1f1f24")), ("hover", new Color("#1f1f24")), ("focus", new Color("#1f1f24")), ("pressed", new Color("#2a2a30")) })
        {
            StyleBoxFlat box = new() { BgColor = back, BorderColor = new Color("#303036") };
            box.SetBorderWidthAll(1);
            box.BorderWidthLeft = 4;
            box.BorderColor = new Color("#303036");
            box.SetCornerRadiusAll(8);
            face.AddThemeStyleboxOverride(state, box);
        }

        ColorRect band = new() { Color = stripe, MouseFilter = MouseFilterEnum.Ignore };
        band.AnchorTop = 0;
        band.AnchorBottom = 1;
        band.OffsetLeft = 0;
        band.OffsetRight = 4;
        face.AddChild(band);

        VBoxContainer words = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        words.SetAnchorsPreset(LayoutPreset.FullRect);
        words.OffsetLeft = 14;
        words.OffsetRight = -8;
        words.AddThemeConstantOverride("separation", 2);

        Label name = new() { Text = card.Name, MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
        name.AddThemeFontSizeOverride("font_size", 16);
        name.AddThemeColorOverride("font_color", Greybox.Text);
        words.AddChild(name);

        List<string> about = [];

        if (card.Kind.Length > 0)
        {
            about.Add(card.Kind);
        }

        if (card.Level > 0)
        {
            about.Add($"Lv {card.Level}+");
        }

        if (about.Count > 0)
        {
            Label kind = new() { Text = string.Join(" · ", about), MouseFilter = MouseFilterEnum.Ignore };
            kind.AddThemeFontSizeOverride("font_size", 12);
            kind.AddThemeColorOverride("font_color", stripe);
            words.AddChild(kind);
        }

        if (card.Arrival.Length > 0 && card.Arrival != card.Name)
        {
            Label arrival = new() { Text = $"도착 {card.Arrival}", MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
            arrival.AddThemeFontSizeOverride("font_size", 11);
            arrival.AddThemeColorOverride("font_color", Greybox.Muted);
            words.AddChild(arrival);
        }

        face.AddChild(words);

        return face;
    }
}
