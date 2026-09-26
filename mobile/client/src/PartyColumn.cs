using Godot;
using Lod.Mobile.Core.World;

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
/// <b>Health.</b> The server never sends a group member's numbers — only the same out-of-a-hundred health bar it sends
/// to anyone who can see them being hurt (0x13). So a member standing near shows that, and one who is not in sight shows
/// a dash rather than a number made up here.
/// </para>
/// <para>
/// Theme (docs/original-ui-451.md): the stone frame and a flat dark inside, light letters on dark, the one committing
/// button (수락) lit, every button 48 tall. The percentage sits right-aligned in a box of its own width so it does not
/// jitter as it changes.
/// </para>
/// </remarks>
public sealed partial class PartyColumn : VBoxContainer
{
    private const int FontSize = 13;

    /// <summary>How long a question waits for an answer. Not answering is the original's "no" — there is no packet for it.</summary>
    private const double AskWaits = 30;

    private readonly Button _invite = new() { Text = "파티 초대", CustomMinimumSize = new Vector2(96, Main.TouchMinimum) };
    private readonly Label _question = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Control _ask;
    private readonly Control _frame;
    private readonly VBoxContainer _rows = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };

    private string? _asker;
    private double _askedFor;
    private string _shown = string.Empty;

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

        Leave = new Button { Text = "나가기", CustomMinimumSize = new Vector2(64, Main.TouchMinimum) };
        Greybox.Plain(Leave);
        Leave.Pressed += () => Left?.Invoke();

        _rows.AddThemeConstantOverride("separation", 2);

        HBoxContainer list = new();
        list.AddThemeConstantOverride("separation", Main.Gutter);
        list.AddChild(_rows);
        list.AddChild(Leave);
        _frame = Plated(list);
        _frame.Visible = false;

        _botFrame = BuildBotFrame();
        _botFrame.Visible = false;

        AddChild(_botFrame);
        AddChild(_invite);
        AddChild(_ask);
        AddChild(_frame);
    }

    // ── 봇 칸 ─────────────────────────────────────────────────────────────
    // 디아블로의 동료 칸처럼 이름 한 줄, 그 아래 와우 파티원 칸처럼 체력 막대(굵게)와 마력 막대(얇게). 봇이 있을 때만 선다.
    // 누르면 봇 장비창(BotGearPanel). 체력·마력 %는 서버가 1초마다 보낸다(0x5E 종류 4).

    private readonly Control _botFrame;
    private readonly Label _botName = new() { ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
    private readonly Label _botNumber = new() { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(38, 0) };
    private readonly ProgressBar _botHealth = Bar(Greybox.Health, 10);
    private readonly ProgressBar _botMana = Bar(Greybox.Mana, 4);

    /// <summary>봇 칸을 눌렀다 — 봇 장비창을 연다.</summary>
    public event Action? BotOpened;

    /// <summary>봇 칸 자체(손 없이 확인할 때 누르려고).</summary>
    public Button BotButton { get; private set; } = null!;

    /// <summary>봇 이름을 — 봇 칸이 따로 있으니 파티 목록에서는 뺀다.</summary>
    private string? _botShown;

    /// <summary>봇 칸을 그린다. 이름이 없으면 숨긴다. 막대 값을 모르면 막대를 숨긴다(빈 막대는 "쓰러졌다" 로 읽힌다).</summary>
    public void ShowBot(string? name, int? health, int? mana)
    {
        _botShown = name;
        _botFrame.Visible = name is not null;

        if (name is null)
        {
            return;
        }

        _botName.Text = $"봇 · {name}";
        _botHealth.Value = health ?? 0;
        _botMana.Value = mana ?? 0;
        _botHealth.Modulate = health is null ? Colors.Transparent : Colors.White;
        _botMana.Modulate = mana is null ? Colors.Transparent : Colors.White;
        _botNumber.Text = health is { } percent ? $"{percent,3}%" : "  —";
    }

    private Control BuildBotFrame()
    {
        _botName.AddThemeFontSizeOverride("font_size", FontSize);
        _botName.AddThemeColorOverride("font_color", Greybox.Title);
        _botName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _botNumber.AddThemeFontSizeOverride("font_size", FontSize);
        _botNumber.AddThemeColorOverride("font_color", Greybox.Text);

        HBoxContainer top = new() { MouseFilter = MouseFilterEnum.Ignore };
        top.AddChild(_botName);
        top.AddChild(_botNumber);

        VBoxContainer inside = new() { MouseFilter = MouseFilterEnum.Ignore };
        inside.AddThemeConstantOverride("separation", 3);
        inside.AddChild(top);
        inside.AddChild(_botHealth);
        inside.AddChild(_botMana);

        foreach (Control part in new Control[] { _botName, _botNumber, _botHealth, _botMana })
        {
            part.MouseFilter = MouseFilterEnum.Ignore;
        }

        MarginContainer pad = new() { MouseFilter = MouseFilterEnum.Ignore };
        pad.AddThemeConstantOverride("margin_left", Main.Gutter);
        pad.AddThemeConstantOverride("margin_right", Main.Gutter);
        pad.AddThemeConstantOverride("margin_top", 6);
        pad.AddThemeConstantOverride("margin_bottom", 6);
        pad.SetAnchorsPreset(LayoutPreset.FullRect);
        pad.AddChild(inside);

        // 칸 전체가 단추다 — 손가락 최소치(48) 높이, 평평한 어둠(원작 4.51: 돌은 틀에만).
        BotButton = new Button { CustomMinimumSize = new Vector2(Wide - 20, Main.TouchMinimum), ClipContents = true };

        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            BotButton.AddThemeStyleboxOverride(state, Greybox.Plate());
        }

        BotButton.AddChild(pad);
        BotButton.Pressed += () => BotOpened?.Invoke();

        PanelContainer plate = new() { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        plate.AddThemeStyleboxOverride("panel", Greybox.Stone());
        plate.AddChild(BotButton);

        return plate;
    }

    private static ProgressBar Bar(Color paint, int height)
    {
        ProgressBar bar = new()
        {
            CustomMinimumSize = new Vector2(0, height),
            MaxValue = 100,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        bar.AddThemeStyleboxOverride("background", Greybox.Surface());
        bar.AddThemeStyleboxOverride("fill", Greybox.Fill(paint));

        return bar;
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
    public bool Grouped => _frame.Visible;

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
    /// Everyone in the group but ourselves, each with the health the server last showed us, or a dash when it has not.
    /// Rebuilt only when what it would show changes.
    /// </summary>
    public void Show(PartyRoster roster, string self, Func<string, int?> health)
    {
        List<(string Name, bool Leader, int? Health)> others = roster.Grouped
            ? [.. roster.Members
                .Where(member => !string.Equals(member.Name, self, StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(member.Name, _botShown, StringComparison.OrdinalIgnoreCase))
                .Select(member => (member.Name, member.Leader, health(member.Name)))]
            : [];

        string shown = string.Join("|", others.Select(one => $"{one.Name}{one.Leader}{one.Health}"));

        _frame.Visible = others.Count > 0;

        if (shown == _shown)
        {
            return;
        }

        _shown = shown;

        foreach (Node row in _rows.GetChildren())
        {
            row.QueueFree();
        }

        foreach ((string name, bool leader, int? left) in others)
        {
            _rows.AddChild(Row(name, leader, left));
        }
    }

    public override void _Process(double delta)
    {
        if (_ask.Visible && (_askedFor += delta) >= AskWaits)
        {
            Answer(false);
        }
    }

    /// <summary>One member: the name (the leader marked 장), a short bar and the number beside it.</summary>
    private static Control Row(string name, bool leader, int? left)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);

        Label named = new()
        {
            Text = leader ? $"{name} ·장" : name,
            CustomMinimumSize = new Vector2(72, 0),
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        named.AddThemeFontSizeOverride("font_size", FontSize);
        named.AddThemeColorOverride("font_color", leader ? Greybox.Title : Greybox.Muted);

        ProgressBar bar = new()
        {
            CustomMinimumSize = new Vector2(40, 8),
            MaxValue = 100,
            Value = left ?? 0,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        bar.AddThemeStyleboxOverride("background", Greybox.Surface());
        bar.AddThemeStyleboxOverride("fill", Greybox.Fill());

        // 모르는 체력을 빈 막대로 그리면 "쓰러졌다" 로 읽힌다 — 자리만 지키고 안 보인다.
        bar.Modulate = left is null ? Colors.Transparent : Colors.White;

        // 숫자가 읽기다 — 막대는 거든다. 모르면 지어내지 않고 줄표.
        Label number = new()
        {
            Text = left is { } percent ? $"{percent,3}%" : "  —",
            CustomMinimumSize = new Vector2(34, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        number.AddThemeFontSizeOverride("font_size", FontSize);
        number.AddThemeColorOverride("font_color", left is null ? Greybox.Muted : Greybox.Text);

        row.AddChild(named);
        row.AddChild(bar);
        row.AddChild(number);

        return row;
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
