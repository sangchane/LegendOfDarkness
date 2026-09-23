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

        AddChild(_invite);
        AddChild(_ask);
        AddChild(_frame);
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
                .Where(member => !string.Equals(member.Name, self, StringComparison.OrdinalIgnoreCase))
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
