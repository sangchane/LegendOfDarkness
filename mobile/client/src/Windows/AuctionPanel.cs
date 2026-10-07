using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 경매장 창(2026-10-07, 설계 <c>autopilot/loot-auction/03-prd.md</c> 화면 스케치) — 위에 탭 넷: 찾기 · 올리기 · 내 경매 · 받을 것(개수).
/// 찾기·내 경매·받을 것은 보일 때 제 보기를 서버에 묻고(0xF4 → 0x5E 8) 줄을 늘어놓는다. 올리기는 가방을 보인다.
/// 판은 설정 창과 같은 어두운 돌판 — 회색 채움 단추 없이.
/// </summary>
/// <remarks>
/// 서버는 요청마다 답을 하나(0x5E 8 보기 · 9 결과)만 보내고, 한 세션의 요청을 0.3초에 하나만 받는다. 그래서 요청은 늘 하나씩 보내고
/// 답이 올 때까지 동작 단추를 잠근다. 답이 안 오면 <see cref="Patience" /> 뒤 풀어 준다. 보기를 다시 묻는 것은 간격을 지켜 <see cref="_Process" /> 가 보낸다.
/// 파일: <c>AuctionPanel.cs</c> 틀·답 · <c>.Rows.cs</c> 줄 · <c>.Forms.cs</c> 입찰 줄·올리기 양식.
/// </remarks>
public sealed partial class AuctionPanel : PanelContainer
{
    private static readonly string[] Tabs = ["찾기", "올리기", "내 경매", "받을 것"];
    private static readonly string[] Kinds = ["전체", "무기", "방어구", "장신구", "기타"];
    private static readonly string[] Sorts = ["남은 시간", "현재가", "즉시 구매가"];
    private static readonly string[] Bands = ["짧게", "보통", "길게", "아주 길게"];
    private static readonly string[] Reasons = ["낙찰품", "판매 대금", "유찰", "밀린 입찰금", "취소", "나눔 넘침"];
    private static readonly float[] TabWidths = [44, 50, 54, 74];
    private static readonly string[] Empties = ["찾는 물건이 없습니다", "올릴 수 있는 물건이 없습니다", "올린 물건이 없습니다", "받을 것이 없습니다"];

    /// <summary>서버의 요청 간격(0.3초)보다 조금 길게 — 이 시간 안에는 다음 요청을 안 보낸다(밀리초).</summary>
    private const ulong Gap = 350;

    /// <summary>답이 이만큼(초) 안 오면 기다리기를 그만둔다.</summary>
    private const double Patience = 5;

    private readonly Button[] _tabButtons = new Button[Tabs.Length];

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
    private readonly Button _takeAll = new() { Text = "모두 받기", CustomMinimumSize = new Vector2(96, Main.TouchMinimum), FocusMode = FocusModeEnum.None, Visible = false };
    private readonly HBoxContainer _pager;
    private readonly Control _search;

    // 줄마다 달린 [취소]·[받기] — 답을 기다리는 동안 잠그려고 모아 둔다.
    private readonly List<Button> _acts = [];
    private ButtonGroup _group = new() { AllowUnpress = true };

    private int _tab;
    private byte _kind;
    private byte _sort;
    private ushort _page;
    private ushort _pages = 1;
    private ushort _want;
    private bool _asking;
    private bool _busy;
    private bool _viewing;
    private double _waited;
    private ulong _readyAt;
    private float _slide;

    public AuctionPanel()
    {
        Name = "Auction";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Stone());
        Close = WindowFrame.CloseButton();
        _tab = Math.Max(0, Array.FindIndex(Tabs, tab => tab.Replace(" ", string.Empty) == Main.AuctionTab.Replace(" ", string.Empty)));

        // 탭 — 설정 창 탭처럼 어두운 판에 밝은 글씨, 고른 탭은 밑줄이 밝다. 폭은 글자 수에 비례(받을 것은 개수가 붙는다) —
        // 글자가 길어도 창을 넓히지는 않게 자른다.
        for (int at = 0; at < Tabs.Length; at++)
        {
            int tab = at;
            Button button = new()
            {
                Text = Tabs[at],
                ToggleMode = true,
                ClipText = true,
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(44, Main.TouchMinimum),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = TabWidths[at]
            };
            button.AddThemeFontSizeOverride("font_size", 14);
            Greybox.Tab(button);
            button.Pressed += () => Select(tab);
            _tabButtons[at] = button;
        }

        HBoxContainer tabs = WindowFrame.Tabs(_tabButtons);

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

        Greybox.Commit(_takeAll);
        _takeAll.Pressed += () => Act(server => server.AuctionTakeAsync(0, CancellationToken.None));

        // 가로(360 높이)는 검색·종류·정렬을 한 줄에. 세로는 두 줄.
        if (Main.Portrait)
        {
            VBoxContainer two = new();
            two.AddThemeConstantOverride("separation", Main.Gutter);
            two.AddChild(Line(_query, find));
            two.AddChild(Line(kind, sort));
            _search = two;
        }
        else
        {
            _search = Line(_query, find, kind, sort);
        }

        // 목록은 창이 허락하는 만큼 늘어난다(창이 화면 아래까지 선다 — 세로 5줄 · 가로 3줄쯤). 안전 구역으로 줄어도 두 줄은 남는다.
        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 2 * (Main.TouchMinimum + 4)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_rows);

        _state.ClipText = true;
        _state.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _state.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _state.AddThemeFontSizeOverride("font_size", 14);

        // 가로는 높이가 빠듯해 안내 줄이 따로 설 자리가 없다 — 쪽 줄 오른쪽에 둔다.
        _pager = Main.Portrait ? Line(_previous, _pageLabel, _next, _takeAll) : Line(_previous, _pageLabel, _next, _state, _takeAll);
        _pager.Alignment = BoxContainer.AlignmentMode.Center;

        VBoxContainer list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", Main.Gutter / 2);
        list.AddChild(scroll);
        list.AddChild(_pager);

        // 입찰 줄·올리기 양식은 세로는 목록 아래, 가로는 목록 오른쪽 옆(높이가 모자라서).
        BoxContainer body = new() { Vertical = Main.Portrait, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", Main.Gutter);
        body.AddChild(list);
        body.AddChild(BuildSide());

        VBoxContainer inside = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inside.AddThemeConstantOverride("separation", Main.Portrait ? Main.Gutter : Main.Gutter / 2);

        // 제목 줄 — 용 문양을 빼고(탭 넷이 폭을 다 쓴다) 탭과 X 만.
        tabs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HBoxContainer head = Line(tabs, Close);
        inside.AddChild(Greybox.Header(head, emblem: false));
        inside.AddChild(_search);
        if (Main.Portrait)
        {
            inside.AddChild(_state);
        }

        inside.AddChild(body);

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
        BuildConfirm();

        Frame();
        Side();
    }

    public Button Close { get; }

    /// <summary>서버에 보낼 요청 — 화면이 연결(<see cref="WorldClient" />)에 대 준다. 연결이 없으면 아무 일도 안 한다.</summary>
    public event Action<Func<WorldClient, Task>>? Send;

    /// <summary>가방(올리기 탭이 보인다). 화면이 연결의 가방을 대 준다.</summary>
    public Func<IReadOnlyList<InventoryItem>>? Bag { get; set; }

    /// <summary>지금 늘어선 줄의 이름 — 손 없이 확인할 때.</summary>
    public List<string> Names { get; } = [];

    /// <summary>지금 탭이 늘어놓은 줄 수. 답이 오기 전에는 null.</summary>
    public int? RowCount { get; private set; }

    public string TabName => Tabs[_tab];

    /// <summary>창을 열 때 — 보던 탭을 다시 묻는다.</summary>
    public void Open()
    {
        _confirm.Visible = false;
        Enter();
    }

    /// <summary>서버가 보낸 보기(0x5E 8) — 지금 탭의 것이면 줄을 늘어놓는다. 받을 것 개수는 어느 보기든 갱신한다.</summary>
    public void ShowPage(AuctionPage page)
    {
        Claims(page.ClaimCount);
        Release();

        // 다른 탭으로 옮기기 전에 보낸 것의 늦은 답이거나, 새로 물을 것이 기다리는 중이면 버린다.
        int view = _tab switch { 0 => 0, 2 => 1, 3 => 2, _ => -1 };
        if (_asking || page.View != view)
        {
            return;
        }

        _page = page.Page;
        _pages = Math.Max((ushort)1, page.Pages);
        _pageLabel.Text = $"{_page + 1} / {_pages}";
        _previous.Disabled = _page == 0;
        _next.Disabled = _page + 1 >= _pages;
        Clear();

        foreach (AuctionRow row in page.Rows)
        {
            Names.Add(row.Name);
            _rows.AddChild(_tab == 0 ? Found(row) : Mine(row));
        }

        foreach (AuctionClaim claim in page.Claims)
        {
            Names.Add(claim.Kind == 1 ? $"금화 {claim.Gold}" : claim.Name);
            _rows.AddChild(ClaimRow(claim));
        }

        Filled();
    }

    /// <summary>내 요청의 결과(0x5E 9). 거절은 금색 한 줄, 성공은 그 문구를 보이고 바뀐 것을 다시 보인다.</summary>
    public void ShowDone(AuctionDone done)
    {
        Claims(done.ClaimCount);
        bool answer = _busy && !_viewing;
        bool view = _busy && _viewing;
        Release();
        Say(done.Message, done.Ok ? Greybox.Text : Greybox.Accent);

        // 보기를 물었는데 거절이 왔다 — 쪽이 안 오니 "…" 에서 풀어 준다.
        if (view && !done.Ok)
        {
            Fill(string.Empty);
        }

        if (!done.Ok)
        {
            return;
        }

        // 올리기는 가방이 바뀌었으니 가방을, 나머지는 보던 쪽을 다시 묻는다.
        if (answer && _tab == 1)
        {
            ShowBag();
        }
        else if (answer)
        {
            Ask(_page, keepSay: true);
        }
    }

    /// <summary>남의 조작이 알린 것(내 물건이 팔림·유찰, 입찰에서 밀림, 낙찰) — 안내 줄에 보이고 받을 것 개수를 갱신한다. 내 경매·받을 것 탭이 보고 있으면 새로 고친다.</summary>
    public void ShowNotice(AuctionDone notice)
    {
        Claims(notice.ClaimCount);
        Say(notice.Message, Greybox.Text);
        if (Visible && _tab >= 2)
        {
            Ask(_page, keepSay: true);
        }
    }

    /// <summary>탭을 누를 때 — 첫 쪽부터.</summary>
    private void Select(int tab)
    {
        _tab = tab;
        _page = 0;
        Enter();
    }

    /// <summary>탭을 바꾸거나 창을 열 때 — 그 탭의 줄만 보이게 하고 보여 줄 것을 새로 묻는다.</summary>
    private void Enter()
    {
        Frame();
        _item = null;
        if (_tab == 1)
        {
            ShowBag();
        }
        else
        {
            Ask(_page);
        }
    }

    /// <summary>지금 탭에 맞게 탭 단추·검색 줄·쪽 줄을 맞춘다. 묻지는 않는다(창이 닫혀 있을 때 서버에 말을 걸지 않게).</summary>
    private void Frame()
    {
        _tabButtons[_tab].SetPressedNoSignal(true);
        _search.Visible = _tab == 0;
        _takeAll.Visible = _tab == 3;
        _previous.Visible = _pageLabel.Visible = _next.Visible = _tab != 1;
        _pager.Visible = _tab != 1 || !Main.Portrait;
        Say(string.Empty, Greybox.Muted);
    }

    /// <summary>보기를 새로 묻는다 — 줄 자리에는 "…". 실제 요청은 앞 요청이 끝나고 간격이 지난 뒤 <see cref="_Process" /> 가 보낸다.</summary>
    private void Ask(ushort page, bool keepSay = false)
    {
        _want = page;
        _asking = true;
        _pick = null;
        Side();
        Fill("…");
        if (!keepSay)
        {
            Say(string.Empty, Greybox.Muted);
        }

        Lock();
    }

    private void SendView()
    {
        _asking = false;
        _viewing = true;
        ushort page = _want;
        byte kind = _kind;
        byte sort = _sort;
        string query = _query.Text.Trim();

        Start(_tab switch
        {
            0 => server => server.AuctionBrowseAsync(kind, sort, page, query, CancellationToken.None),
            2 => server => server.AuctionMineAsync(page, CancellationToken.None),
            _ => server => server.AuctionClaimsAsync(page, CancellationToken.None)
        });
    }

    /// <summary>동작(올리기·입찰·구매·취소·받기)을 보낸다. 앞 요청의 답이 오기 전이면 보내지 않는다.</summary>
    private void Act(Func<WorldClient, Task> call)
    {
        if (_busy || _asking)
        {
            return;
        }

        _viewing = false;
        Say("…", Greybox.Muted);
        Start(call);
    }

    private void Start(Func<WorldClient, Task> call)
    {
        _busy = true;
        _waited = 0;
        _readyAt = Time.GetTicksMsec() + Gap;
        Lock();
        Send?.Invoke(call);
    }

    private void Release()
    {
        _busy = false;
        _viewing = false;
        Lock();
    }

    /// <summary>답을 기다리는 동안은 동작 단추를 잠근다. 줄이 없으면 [모두 받기] 도 잠근다.</summary>
    private void Lock()
    {
        bool wait = _busy || _asking;
        _bidButton.Disabled = _buyoutButton.Disabled = _postButton.Disabled = wait;
        _takeAll.Disabled = wait || (RowCount ?? 0) == 0;

        foreach (Button act in _acts)
        {
            act.Disabled = wait;
        }
    }

    private void Claims(ushort count) => _tabButtons[3].Text = count > 0 ? $"받을 것 {count}" : Tabs[3];

    /// <summary>안내 한 줄 — 거절·결과·알림. 빈 문구는 줄만 남기고 지운다.</summary>
    private void Say(string text, Color colour)
    {
        _state.Text = text;
        _state.AddThemeColorOverride("font_color", colour);
    }

    public override void _Process(double delta)
    {
        if (_busy && (_waited += delta) > Patience)
        {
            bool viewing = _viewing;
            Release();
            Say("응답이 없습니다", Greybox.Accent);
            if (viewing)
            {
                Fill(string.Empty);
            }
        }

        if (_asking && Visible && !_busy && Time.GetTicksMsec() >= _readyAt)
        {
            SendView();
        }

        PollBag(delta);
        FollowKeyboard();
    }

    /// <summary>
    /// 글자를 치는 동안 칸과 그 칸의 단추가 화면 키보드 위에 오도록 창을 통째로 올린다(로그인 화면과 같은 규칙, <see cref="KeyboardFit" />).
    /// 가로는 칸이 위쪽에 서서 거의 안 움직인다.
    /// </summary>
    private void FollowKeyboard()
    {
        if (GetParent() is not Control holder)
        {
            return;
        }

        float slide = 0;

        if (Visible && TouchInput.Covered > 0 && TouchInput.Editing(GetViewport()) is { } field && IsAncestorOf(field))
        {
            Rect2 typed = field.GetGlobalRect();
            Control last = field == _query ? field : _tab == 1 ? _form : _strip;
            Rect2 finish = last.GetGlobalRect();

            // 지금 자리는 이미 올린 만큼 올라가 있다 — 올리기 전 자리로 되돌려 잰다.
            slide = KeyboardFit.Slide(
                typed.Position.Y + _slide,
                typed.End.Y + _slide,
                finish.End.Y + _slide,
                GetViewportRect().Size.Y - TouchInput.Covered,
                Main.SafeInsets.Top,
                Main.Gutter);
        }

        if (!Mathf.IsEqualApprox(slide, _slide))
        {
            holder.OffsetTop -= slide - _slide;
            holder.OffsetBottom -= slide - _slide;
            _slide = slide;
        }
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
