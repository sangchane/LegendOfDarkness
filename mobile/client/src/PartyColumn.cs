using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// Everything about the group (원작의 그룹, 화면에서는 파티) in one small column under the top row: the 파티 초대 button
/// while a person is picked out, the question when somebody asks us, and who is in the group with how hurt they are.
/// </summary>
/// <remarks>
/// <para>
/// One place, one step each (사용자: 낮은 깊이). Asking is a tap on a person and a tap on the button; answering is
/// 수락 or 거절 right where the question shows; leaving is one button beside the list. Nothing here opens a window.
/// </para>
/// <para>
/// <b>Who is in the group</b> is a grid of small tiles at the left edge (2026-09-27, 사용자: WoW 애드온 "그리드" 처럼) —
/// the bot's first, then each member's. A tile is its health: the fill is the tile's ground, the name (아이디, the bot's
/// "봇") sits on it, mana is one thin line under it. Numbers are not written (the tooltip has them).
/// </para>
/// <para>
/// Theme (docs/original-ui-451.md): the stone frame and a flat dark inside, light letters on dark, the one committing
/// button (수락) lit, every button 48 tall.
/// </para>
/// </remarks>
public sealed partial class PartyColumn : VBoxContainer
{
    private const int FontSize = 13;

    /// <summary>How long a question waits for an answer. Not answering is the original's "no" — there is no packet for it.</summary>
    private const double AskWaits = 30;

    /// <summary>타일 하나 — 폭, 체력 칸 높이, 그 아래 마력 줄 높이, 타일 사이 틈.</summary>
    public const int TileWide = 68;
    public const int TileTall = 24;
    public const int ManaTall = 3;
    public const int TileGap = 3;

    /// <summary>타일 한 줄의 높이(체력 칸 + 마력 줄).</summary>
    public const int TileRow = TileTall + ManaTall;

    private readonly Button _invite = new() { Text = "파티 초대", CustomMinimumSize = new Vector2(96, Main.TouchMinimum) };
    private readonly Label _question = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Control _ask;

    // 타일 격자 — 봇 타일이 맨 앞, 그다음 파티원. 이 기둥 밖(화면 왼쪽 가장자리)에 게임 화면이 세우고 폭(열 수)을 정한다(Members).
    private readonly Container _frame = new HFlowContainer();

    private string? _asker;
    private double _askedFor;
    private string _shown = string.Empty;
    private int _memberCount;

    public PartyColumn()
    {
        Name = "Party";
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", Main.Gutter);

        Greybox.Plain(_invite);
        _invite.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _invite.Visible = false;
        _invite.Pressed += () => Invited?.Invoke();

        _question.AddThemeFontSizeOverride("font_size", FontSize);
        _question.AddThemeColorOverride("font_color", Greybox.Text);
        _question.CustomMinimumSize = new Vector2(Wide - 24, 0);

        Button decline = new() { Text = "거절", CustomMinimumSize = new Vector2(64, Main.TouchMinimum) };
        Greybox.Plain(decline);
        decline.Pressed += () => Answer(false);
        Decline = decline;

        Button accept = new() { Text = "수락", CustomMinimumSize = new Vector2(64, Main.TouchMinimum) };
        Greybox.Commit(accept);
        accept.Pressed += () => Answer(true);
        AcceptButton = accept;

        HBoxContainer answers = new() { Alignment = AlignmentMode.End };
        answers.AddThemeConstantOverride("separation", Main.Gutter);
        answers.AddChild(decline);
        answers.AddChild(accept);

        VBoxContainer asking = new();
        asking.AddThemeConstantOverride("separation", Main.Gutter / 2);
        asking.AddChild(_question);
        asking.AddChild(answers);
        _ask = Plated(asking);
        _ask.Visible = false;

        // [나가기] — 격자 오른쪽 위 모서리에 붙는 작은 그림 단추(문 밖으로 나가는 화살표, 2026-09-27). 누르는 곳은 32 — 포션 칸과
        // 같은 크기, 급하게 누를 일이 없다. 누르면 전처럼 곧바로 나간다(묻지 않는다).
        Leave = new Button { TooltipText = "파티 나가기", CustomMinimumSize = new Vector2(LeaveSide, LeaveSide), Visible = false, FocusMode = FocusModeEnum.None };
        Greybox.Plain(Leave);
        Glyph door = new(GlyphKind.Leave, 16) { Paint = Greybox.Text };
        Leave.AddChild(door);
        WindowFrame.Centre(door, 16);
        Leave.Pressed += () => Left?.Invoke();

        _frame.AddThemeConstantOverride("h_separation", TileGap);
        _frame.AddThemeConstantOverride("v_separation", TileGap);
        _frame.MouseFilter = MouseFilterEnum.Ignore;
        _frame.Visible = false;

        _botFrame = BuildBotFrame();
        _botFrame.Visible = false;
        _frame.AddChild(_botFrame);

        AddChild(_invite);
        AddChild(_ask);
    }

    // ── 봇 타일 ───────────────────────────────────────────────────────────
    // 파티원과 같은 타일, 이름 자리에 "봇"(봇 이름은 뺀다 — 2026-09-26 "이름까지 띄울 필요 없다"). 격자 맨 앞. 누르면 봇
    // 장비창(BotGearPanel). 체력·마력 %는 서버가 1초마다 보낸다(0x5E 종류 4), 상태는 종류 3.

    private readonly Control _botFrame;
    private readonly GridTile _botTile = new();

    /// <summary>봇 칸을 눌렀다 — 봇 장비창을 연다.</summary>
    public event Action? BotOpened;

    /// <summary>봇 칸 자체(손 없이 확인할 때 누르려고).</summary>
    public Button BotButton { get; private set; } = null!;

    /// <summary>봇 타일 — 격자 맨 앞에 선다. 봇이 있을 때만 보인다.</summary>
    public Control BotSlot => _botFrame;

    /// <summary>봇 이름을 — 봇 칸이 따로 있으니 파티 목록에서는 뺀다.</summary>
    private string? _botShown;

    /// <summary>격자에 선 타일 수(봇 포함) — 게임 화면이 열 수를 정하려고.</summary>
    public int TileCount => (_botShown is null ? 0 : 1) + _memberCount;

    /// <summary>[나가기] 그림 단추의 한 변.</summary>
    public const int LeaveSide = 32;

    /// <summary>
    /// 봇 타일을 그린다. 이름이 없으면 숨긴다. 숫자(<paramref name="numbers" />, 0x5E 종류 4 꼬리)는 타일에 적지 않고 누르기 전
    /// 안내(툴팁)에만 둔다.
    /// </summary>
    public void ShowBot(string? name, int? health, int? mana, IReadOnlyList<StatusBadge>? statuses = null, VitalNumbers? numbers = null)
    {
        _botShown = name;
        _botFrame.Visible = name is not null;
        Refresh();

        if (name is null)
        {
            return;
        }

        BotButton.TooltipText = $"봇 · {name} {PartyNumbers.Text(numbers?.Health, numbers?.MaximumHealth, health)} — 누르면 봇 장비";
        _botTile.Show("봇", false, health, mana, statuses ?? []);
    }

    private Control BuildBotFrame()
    {
        _botTile.SetAnchorsPreset(LayoutPreset.FullRect);

        // 타일 전체가 단추다 — 평평한 틀 없이 타일이 곧 모양(누를 때만 테두리 빛).
        BotButton = new Button { CustomMinimumSize = new Vector2(TileWide, TileRow), FocusMode = FocusModeEnum.None };

        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            StyleBoxFlat plate = new() { BgColor = Colors.Transparent };

            if (state == "pressed")
            {
                plate.BorderColor = Greybox.Title;
                plate.SetBorderWidthAll(1);
            }

            BotButton.AddThemeStyleboxOverride(state, plate);
        }

        BotButton.AddChild(_botTile);
        BotButton.Pressed += () => BotOpened?.Invoke();

        return BotButton;
    }

    /// <summary>How wide the column may get — a name, a short bar and a percentage, and the button beside them.</summary>
    public const int Wide = 196;

    public event Action? Invited;

    /// <summary>Somebody's ask was answered: their name, and whether it was a yes.</summary>
    public event Action<string, bool>? Answered;

    public event Action? Left;

    public Button Invite => _invite;
    public Button AcceptButton { get; }
    public Button Decline { get; }
    public Button Leave { get; }

    /// <summary>Whether a question is on the screen now.</summary>
    public bool Asking => _ask.Visible;

    /// <summary>Whether the group list is showing.</summary>
    public bool Grouped => _memberCount > 0;

    /// <summary>타일 격자(봇 + 파티원) — 게임 화면이 왼쪽 가장자리에 세운다.</summary>
    public Control Members => _frame;

    /// <summary>The 파티 초대 button, for whoever is picked out — only a person, and only one who could join.</summary>
    public void CanInvite(bool can) => _invite.Visible = can && !_ask.Visible;

    /// <summary>Somebody asks us. A newer ask replaces an older one — the server listens only to the last (GroupAskedBy).</summary>
    public void Ask(string name)
    {
        _asker = name;
        _askedFor = 0;
        _question.Text = $"{name}님이 파티에 초대합니다";
        _ask.Visible = true;
        _invite.Visible = false;
    }

    private void Answer(bool yes)
    {
        _ask.Visible = false;

        if (_asker is { } name)
        {
            _asker = null;
            Answered?.Invoke(name, yes);
        }
    }

    /// <summary>
    /// Everyone in the group but ourselves (and the bot, which has its own tile), each as a tile. Rebuilt only when what
    /// it would show changes.
    /// </summary>
    public void Show(PartyRoster roster, string self, Func<string, MemberLook> look)
    {
        List<(string Name, bool Leader, MemberLook Look)> others = roster.Grouped
            ? [.. roster.Members
                .Where(member => !string.Equals(member.Name, self, StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(member.Name, _botShown, StringComparison.OrdinalIgnoreCase))
                .Select(member => (member.Name, member.Leader, look(member.Name)))]
            : [];

        _memberCount = others.Count;
        Refresh();

        string shown = string.Join("|", others.Select(one =>
            $"{one.Name}{one.Leader}{one.Look.Health}/{one.Look.Mana}/{one.Look.Numbers}/{string.Join(",", one.Look.Statuses.Select(b => b.Icon))}"));

        if (shown == _shown)
        {
            return;
        }

        _shown = shown;

        foreach (Node tile in _frame.GetChildren())
        {
            if (tile != _botFrame)
            {
                _frame.RemoveChild(tile);
                tile.QueueFree();
            }
        }

        foreach ((string name, bool leader, MemberLook memberLook) in others)
        {
            GridTile tile = new()
            {
                CustomMinimumSize = new Vector2(TileWide, TileRow),
                TooltipText = $"{name} {PartyNumbers.Text(memberLook.Numbers?.Health, memberLook.Numbers?.MaximumHealth, memberLook.Health)}"
            };
            tile.Show(name, leader, memberLook.Health, memberLook.Mana, memberLook.Statuses);
            _frame.AddChild(tile);
        }
    }

    /// <summary>격자는 봇이나 파티원이 있으면, [나가기] 는 사람 파티원이 있을 때만(봇만 있는 그룹은 봇 장비창·설정에서 나간다).</summary>
    private void Refresh()
    {
        _frame.Visible = TileCount > 0;
        Leave.Visible = _memberCount > 0;
    }

    public override void _Process(double delta)
    {
        if (_ask.Visible && (_askedFor += delta) >= AskWaits)
        {
            Answer(false);
        }
    }

    /// <summary>The stone frame round a flat, nearly opaque inside — the same plate as the top row's.</summary>
    private static Control Plated(Control inside)
    {
        PanelContainer inner = new();
        inner.AddThemeStyleboxOverride("panel", Greybox.Plate());
        inner.AddChild(inside);

        PanelContainer plate = new() { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        plate.AddThemeStyleboxOverride("panel", Greybox.Stone());
        plate.AddChild(inner);

        return plate;
    }
}

/// <summary>
/// 파티원·봇 타일 하나(WoW "그리드" 식, 2026-09-27): 체력이 곧 타일 바탕(채워진 만큼 체력 색, 15% 이하 빨강, 쓰러지면 회색),
/// 그 위 가운데 이름, 그룹장이면 왼쪽 위에 작은 별, 오른쪽 위 구석에 상태 아이콘 셋까지(아주 작게, 해로운 것은 빨간 테두리), 타일 아래 마력 한 줄.
/// 체력을 모르면(아직 알림 전) 바탕은 비고 이름만 — 쓰러진 것(회색으로 가득)과 구별된다.
/// </summary>
public sealed partial class GridTile : VBoxContainer
{
    private static readonly Color Dead = new("#5c5c58");

    private readonly ProgressBar _health = new() { MaxValue = 100, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
    private readonly ProgressBar _mana = new() { MaxValue = 100, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _name = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        ClipText = true,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        MouseFilter = MouseFilterEnum.Ignore
    };
    private readonly Label _star = new() { Text = "★", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
    private readonly StatusStrip _status = new(side: 6, most: 3, timed: false);

    public GridTile()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 0);

        _health.CustomMinimumSize = new Vector2(PartyColumn.TileWide, PartyColumn.TileTall);
        StyleBoxFlat ground = Greybox.Plate();
        ground.SetContentMarginAll(0);
        _health.AddThemeStyleboxOverride("background", ground);

        _mana.CustomMinimumSize = new Vector2(PartyColumn.TileWide, PartyColumn.ManaTall);
        _mana.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("#0f0f0f") });
        _mana.AddThemeStyleboxOverride("fill", Greybox.Fill(Greybox.Mana));

        _name.AddThemeFontSizeOverride("font_size", 10);
        _name.AddThemeColorOverride("font_outline_color", new Color("#030303"));
        _name.AddThemeConstantOverride("outline_size", 2);
        _name.SetAnchorsPreset(LayoutPreset.FullRect);
        _name.OffsetLeft = 3;
        _name.OffsetRight = -3;
        // 위 구석(별·상태 아이콘)을 비켜 조금 아래로.
        _name.OffsetTop = 5;
        _health.AddChild(_name);

        _star.AddThemeFontSizeOverride("font_size", 8);
        _star.AddThemeColorOverride("font_color", Greybox.Title);
        _star.AddThemeColorOverride("font_outline_color", new Color("#030303"));
        _star.AddThemeConstantOverride("outline_size", 2);
        _star.Position = new Vector2(2, -2);
        _health.AddChild(_star);

        _status.MouseFilter = MouseFilterEnum.Ignore;
        _status.SetAnchorsPreset(LayoutPreset.TopRight);
        _status.GrowHorizontal = GrowDirection.Begin;
        _health.AddChild(_status);

        AddChild(_health);
        AddChild(_mana);
    }

    public void Show(string name, bool leader, int? health, int? mana, IReadOnlyList<StatusBadge> statuses)
    {
        _name.Text = name;
        _star.Visible = leader;

        bool dead = health is 0;
        _health.Value = dead ? 100 : health ?? 0;
        _health.AddThemeStyleboxOverride("fill", Greybox.Fill(dead ? Dead : health <= 15 ? Greybox.Gone : Greybox.Health));
        _name.AddThemeColorOverride("font_color", leader ? Greybox.Title : Greybox.Text);

        _mana.Value = mana ?? 0;
        _mana.Modulate = mana is null ? Colors.Transparent : Colors.White;
        _status.Show(statuses);
    }
}

/// <summary>
/// What a party member's frame shows — health and mana %, when known, what is on them (icons only), and the exact numbers
/// when the server sends them (0x5E 종류 6 꼬리, 2026-09-27 — 타일에는 적지 않고 안내에만).
/// </summary>
public sealed record MemberLook(int? Health, int? Mana, IReadOnlyList<StatusBadge> Statuses, VitalNumbers? Numbers = null);
