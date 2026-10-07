using System;
using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Protocol.World;

namespace LodClient;

/// <summary>
/// 경매장 창(2026-10-07, 설계 <c>autopilot/loot-auction/03-prd.md</c> 화면 스케치) — 찾기: 검색어·종류·정렬로 서버에 묻고(0xF4 0),
/// 온 쪽(0x5E 8)을 줄마다 그림 · 이름 · 남은 시간 · 현재가 · 즉시 구매가로 늘어놓는다. 쪽은 ◀ ▶. 파는 이 이름은 오지 않는다(와우).
/// 판은 설정 창과 같은 어두운 돌판 — 회색 채움 단추 없이.
/// </summary>
public sealed partial class AuctionPanel : PanelContainer
{
    private static readonly string[] Kinds = ["전체", "무기", "방어구", "장신구", "기타"];
    private static readonly string[] Sorts = ["남은 시간", "현재가", "즉시 구매가"];
    private static readonly string[] Bands = ["짧게", "보통", "길게", "아주 길게"];

    private readonly LineEdit _query = new()
    {
        PlaceholderText = "물건 이름",
        MaxLength = 20,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
    };

    private readonly VBoxContainer _rows = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _state = Words(string.Empty, Greybox.Muted);
    private readonly Label _pageLabel = Words("1 / 1", Greybox.Text);
    private readonly Button _previous = Small("◀");
    private readonly Button _next = Small("▶");
    private byte _kind;
    private byte _sort;
    private ushort _page;
    private ushort _pages = 1;

    public AuctionPanel()
    {
        Name = "Auction";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());
        Close = WindowFrame.CloseButton();

        PercentSelect kind = PercentSelect.Of(Kinds, 0, this, 88, Main.TouchMinimum);
        kind.Changed += index => { _kind = (byte)index; Ask(0); };
        PercentSelect sort = PercentSelect.Of(Sorts, 0, this, 112, Main.TouchMinimum);
        sort.Changed += index => { _sort = (byte)index; Ask(0); };
        Button find = Small("찾기");
        find.CustomMinimumSize = new Vector2(64, Main.TouchMinimum);
        find.Pressed += () => Ask(0);
        _query.TextSubmitted += _ => Ask(0);
        _previous.Pressed += () => Ask((ushort)Math.Max(0, _page - 1));
        _next.Pressed += () => Ask((ushort)Math.Min(_pages - 1, _page + 1));

        // 가로(360 높이)는 검색·종류·정렬을 한 줄에, 목록 세 줄. 세로는 두 줄에 목록 다섯 줄.
        HBoxContainer search = Main.Portrait ? Line(_query, find) : Line(_query, find, kind, sort);

        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, (Main.Portrait ? 5 : 3) * (Main.TouchMinimum + 4)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_rows);

        HBoxContainer pages = Line(_previous, _pageLabel, _next);
        pages.Alignment = BoxContainer.AlignmentMode.Center;

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Gutter);
        inside.AddChild(WindowFrame.Head(WindowFrame.Title("경매장"), Close));
        inside.AddChild(search);
        if (Main.Portrait)
        {
            inside.AddChild(Line(kind, sort));
        }

        _state.Visible = false;
        inside.AddChild(_state);
        inside.AddChild(scroll);
        inside.AddChild(pages);

        MarginContainer margin = new();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, Main.Gutter);
        }

        margin.AddChild(inside);

        // 돌 테두리(Stone)는 속을 안 그린다 — 설정 창처럼 불투명 판을 한 겹(지도·조작 단추가 비치지 않게).
        PanelContainer within = new();
        within.AddThemeStyleboxOverride("panel", Greybox.Sheet());
        within.AddChild(margin);
        AddChild(within);
    }

    public Button Close { get; }

    /// <summary>찾기 요청 — 종류 · 정렬 · 쪽 · 검색어. 화면이 서버에 보낸다(0xF4 0).</summary>
    public event Action<byte, byte, ushort, string>? Search;

    /// <summary>지금 늘어선 줄의 이름 — 손 없이 확인할 때.</summary>
    public List<string> Names { get; } = [];

    /// <summary>창을 열 때 — 보던 쪽을 다시 묻는다.</summary>
    public void Open() => Ask(_page);

    /// <summary>서버가 보낸 찾기 쪽(보기 0)을 늘어놓는다.</summary>
    public void ShowPage(AuctionPage page)
    {
        foreach (Node old in _rows.GetChildren())
        {
            _rows.RemoveChild(old);
            old.QueueFree();
        }

        Names.Clear();
        _page = page.Page;
        _pages = Math.Max((ushort)1, page.Pages);
        _pageLabel.Text = $"{_page + 1} / {_pages}";
        _previous.Disabled = _page == 0;
        _next.Disabled = _page + 1 >= _pages;
        _state.Text = page.Rows.Count == 0 ? "찾는 물건이 없습니다" : string.Empty;
        _state.Visible = page.Rows.Count == 0;
        _state.AddThemeColorOverride("font_color", Greybox.Muted);

        foreach (AuctionRow row in page.Rows)
        {
            Names.Add(row.Name);
            _rows.AddChild(Row(row));
        }
    }

    /// <summary>경매 결과(0x5E 9) — 거절 문구는 금색 한 줄로.</summary>
    public void ShowDone(AuctionDone done)
    {
        if (done.Ok)
        {
            return;
        }

        _state.Text = done.Message;
        _state.Visible = true;
        _state.AddThemeColorOverride("font_color", Greybox.Accent);
    }

    private void Ask(ushort page)
    {
        _state.Text = "…";
        _state.Visible = true;
        _state.AddThemeColorOverride("font_color", Greybox.Muted);
        Search?.Invoke(_kind, _sort, page, _query.Text.Trim());
    }

    private static Control Row(AuctionRow row)
    {
        PanelContainer plate = new() { CustomMinimumSize = new Vector2(0, Main.TouchMinimum) };
        plate.AddThemeStyleboxOverride("panel", Greybox.Surface());

        HBoxContainer content = new() { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", Main.Gutter);
        content.AddChild(new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = ItemIcons.For(row.Image),
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TextureFilter = TextureFilterEnum.Nearest
        });

        VBoxContainer words = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        string name = row.Stacks > 1 ? $"{row.Name} ×{row.Stacks}" : row.Name;
        Label title = Words(name, Greybox.Text);
        title.ClipText = true;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        words.AddChild(title);

        string mark = (row.Flags & 1) != 0 ? " · 내 물건" : (row.Flags & 2) != 0 ? " · 최고 입찰" : string.Empty;
        string buyout = row.Buyout > 0 ? $"{row.Buyout:N0}" : "—";
        Label detail = Words($"{Bands[Math.Min(row.Band, (byte)3)]} · 현 {row.Price:N0} · 즉 {buyout}{mark}", Greybox.Muted);
        detail.AddThemeFontSizeOverride("font_size", 13);
        words.AddChild(detail);
        content.AddChild(words);
        plate.AddChild(content);

        return plate;
    }

    private static HBoxContainer Line(params Control[] parts)
    {
        HBoxContainer line = new();
        line.AddThemeConstantOverride("separation", Main.Gutter / 2);
        foreach (Control part in parts)
        {
            line.AddChild(part);
        }

        return line;
    }

    private static Button Small(string text)
    {
        Button button = new() { Text = text, CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum), FocusMode = FocusModeEnum.None };
        Greybox.Plain(button);
        return button;
    }

    private static Label Words(string text, Color colour)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", colour);
        return label;
    }
}
