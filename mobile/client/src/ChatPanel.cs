using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// What has been said, to read back through: people's own words and the server's own, in the one window, with tabs to
/// show one kind at a time. The lines over the movement pad fade after a few seconds so they do not sit on the map;
/// this is where they can be found again (사용자, 2026-09-18).
/// </summary>
/// <remarks>
/// Tabs across the top of the one window is what mobile games do — one list, a tab per channel, and only that channel's
/// lines while it is chosen (docs/mobile-client.md 대화).
/// </remarks>
public sealed partial class ChatPanel : PanelContainer
{
    private const int FontSize = 14;

    /// <summary>How tall the lines are, before scrolling. The window is not the map's to fill (사용자, 2026-09-18).</summary>
    private static readonly int Tall = Main.Portrait ? 220 : 150;

    // 굴림 칸은 안엣것을 그 자신의 최소 폭으로 재므로, 폭을 다 쓰라고 일러 주지 않으면 글자가 한 자씩 끊겨 내려간다.
    private readonly VBoxContainer _lines = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly ScrollContainer _scroll = new()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
    };

    /// <summary>How long a line may be. The original counts bytes, and Korean takes two of them each.</summary>
    private const int Longest = 60;

    private readonly LineEdit _typed = new()
    {
        PlaceholderText = "할 말",
        MaxLength = Longest,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(0, Main.TouchMinimum),
        // 엔터로 보낸 뒤에도 키보드를 내리지 않는다 — 다음 줄을 바로 친다. 기본값은 보낼 때마다 키보드를 내렸다.
        KeepEditingOnTextSubmit = true
    };

    // 키보드가 올라와 자리가 모자라면 접는 탭 줄, 그리고 창을 들어 올린 만큼.
    private readonly HBoxContainer _head = new();
    private float _lift;

    // 전체 · 일반(사람의 말 — 근처 말·외침·귓속말·길드) · 파티 · 시스템. 귓속말은 따로 두지 않는다: 서버가 귓속말을
    // 보내는 곳이 두 군데뿐이고(GameServerHandlers.cs:865-866) 줄 앞에 이름이 붙어 섞여도 구별된다.
    private readonly (Button Tab, MessageChannel? Channel)[] _tabs =
    [
        (Tab("전체"), null),
        (Tab("일반"), MessageChannel.General),
        (Tab("파티"), MessageChannel.Party),
        (Tab("시스템"), MessageChannel.System)
    ];

    // 어느 탭이 눌렸나(null 은 전체), 그리고 그 탭으로 몇 줄을 적어 두었나. 늘어난 만큼만 덧붙인다.
    private MessageChannel? _only;
    private int _written;
    private int _read;

    public ChatPanel()
    {
        Name = "Chat";
        Visible = false;
        // 틀은 원작 돌, 속은 평평한 어둠 — 무늬 위에 작은 글자를 얹으면 먼저 무너진다(data/ui-vault).
        AddThemeStyleboxOverride("panel", Greybox.Stone());

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter);

        HBoxContainer head = _head;
        head.AddThemeConstantOverride("separation", Main.Gutter);
        foreach ((Button tab, MessageChannel? channel) in _tabs)
        {
            tab.Pressed += () => Choose(channel);
            head.AddChild(tab);
        }

        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        head.AddChild(Close);

        _lines.AddThemeConstantOverride("separation", 2);
        _scroll.CustomMinimumSize = new Vector2(0, Tall);
        _scroll.AddChild(_lines);

        // 초점을 받지 않는 단추 — 누를 때 글자 칸이 초점을 잃지 않아 키보드가 내려갔다 다시 오르지 않는다.
        Button send = new() { Text = "보내기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum), FocusMode = FocusModeEnum.None };

        Greybox.Commit(send);
        send.Pressed += Say;
        _typed.TextSubmitted += _ => Say();

        HBoxContainer typing = new();
        typing.AddThemeConstantOverride("separation", Main.Gutter);
        typing.AddChild(_typed);
        typing.AddChild(send);

        // 입력 줄 안(보내기 포함)을 누르면 키보드를 그대로 둔다. 밖을 누르면 내린다(TouchInput).
        TouchInput.Zone(_typed, typing);

        body.AddChild(head);
        body.AddChild(_scroll);
        body.AddChild(typing);
        PanelContainer inside = new();
        inside.AddThemeStyleboxOverride("panel", Greybox.Sheet());
        inside.AddChild(body);

        AddChild(inside);

        Choose(null);
    }

    /// <summary>The button that shuts the panel, so whoever opened it decides what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody wrote a line and asked to say it. What comes of it is the server's to say.</summary>
    public event Action<string>? Sent;

    /// <summary>
    /// Whether what is typed goes to the group rather than out loud: it does while the 파티 tab is open, the way mobile
    /// games send to the channel being read.
    /// </summary>
    public bool ToParty => _only == MessageChannel.Party;

    /// <summary>Only when checking without a hand (<c>--party-say</c>): opens the 파티 tab and sends a line from the box.</summary>
    public void Rehearse(string line)
    {
        Choose(MessageChannel.Party);
        _typed.Text = line;
        Say();
    }

    /// <summary>
    /// Says what was written and empties the box, leaving the keyboard up so another line can follow. What was said comes
    /// back from the server like anybody else's words — nothing is written here as if it had been.
    /// </summary>
    private void Say()
    {
        string line = _typed.Text.Trim();

        if (line.Length == 0)
        {
            return;
        }

        Sent?.Invoke(line);
        _typed.Clear();
        _typed.GrabFocus();
    }

    private static Button Tab(string name)
    {
        Button tab = new()
        {
            Text = name,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
        };

        // 고른 탭만 밝은 돌에 음각 — 글자를 읽지 않고도 어느 쪽이 열려 있는지 보인다.
        Greybox.Tab(tab);

        return tab;
    }

    /// <summary>Shows one kind of line, or all of them (null). The list is written again, because a different lot belongs in it.</summary>
    public void Choose(MessageChannel? channel)
    {
        _only = channel;
        _typed.PlaceholderText = channel == MessageChannel.Party ? "파티에게 할 말" : "할 말";

        foreach ((Button tab, MessageChannel? mine) in _tabs)
        {
            tab.ButtonPressed = mine == channel;
        }

        _written = -1;
    }

    /// <summary>
    /// Shows every line said so far, oldest at the top. Only the new ones are added — rebuilding the lot every frame
    /// would throw away where the reader had scrolled to.
    /// </summary>
    public void Show(IReadOnlyList<(MessageChannel Channel, string Text)> said)
    {
        if (said.Count == _read && _written >= 0)
        {
            return;
        }

        // 지난 줄이 잘려 나갔거나(오래된 것은 버린다) 탭이 바뀌었으면 처음부터 다시 적는다.
        if (said.Count < _read || _written < 0)
        {
            foreach (Node line in _lines.GetChildren())
            {
                line.QueueFree();
            }

            _written = 0;
        }

        for (int index = _written; index < said.Count; index++)
        {
            if (_only is { } only && said[index].Channel != only)
            {
                continue;
            }

            Label line = new()
            {
                Text = said[index].Text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            line.AddThemeFontSizeOverride("font_size", FontSize);
            line.AddThemeColorOverride("font_color", Greybox.Muted);

            _lines.AddChild(line);
        }

        _written = said.Count;
        _read = said.Count;

        // 마지막 줄이 보이게 맨 아래로. 자리가 잡힌 다음이라야 얼마나 내려야 하는지 알 수 있다.
        CallDeferred(MethodName.ScrollToEnd);
    }

    private void ScrollToEnd() => _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;

    /// <summary>
    /// While words are being typed, the input bar rides just above the keyboard and the world stays as it is: the window
    /// is lifted by what the keyboard covers below it, and the lines give up height so the whole window fits between the
    /// top row and the keyboard. When even two lines would not fit (a landscape phone leaves about 110), the tabs fold
    /// away too, leaving the last lines and the bar. Closing the keyboard puts everything back.
    /// </summary>
    /// <remarks>
    /// The holder this window stands in belongs to GameScreen, which lifts it by the whole keyboard height each frame
    /// before this runs; this overrides that with the lift that leaves no gap (the keyboard also covers the home-bar
    /// margin) and with the pretend keyboard of <c>--keyboard</c>. Without the list giving way, the window was taller
    /// than the room left and its holder grew downwards, putting the input under the keyboard.
    /// </remarks>
    public override void _Process(double delta)
    {
        if (!Visible || GetParent() is not Control holder)
        {
            return;
        }

        float covered = TouchInput.Covered;
        bool typing = covered > 0 && TouchInput.Editing(GetViewport()) == _typed;

        // 들어 올리기 전의 창 바닥. 위에서 누가 들어 올렸든 OffsetBottom 만큼 되돌려 잰다.
        Rect2 held = holder.GetGlobalRect();
        float bottom = held.End.Y - holder.OffsetBottom;
        float keyboardTop = GetViewportRect().Size.Y - covered;
        float lift = Mathf.Max(0, bottom - keyboardTop);

        holder.OffsetBottom = -lift;

        float list = Tall;
        bool head = true;

        if (typing)
        {
            // 창에서 목록을 뺀 높이(탭 줄은 있는 것으로). 줄 사이 간격(Gutter)도 보이는 것만 센다.
            float room = bottom - lift - held.Position.Y;
            float heads = _head.GetCombinedMinimumSize().Y + Main.Gutter;
            float chrome = GetCombinedMinimumSize().Y
                           - (_scroll.Visible ? _scroll.CustomMinimumSize.Y + Main.Gutter : 0)
                           + (_head.Visible ? 0 : heads);

            list = KeyboardFit.ListRoom(Tall, 0, room, chrome + Main.Gutter);

            // 두 줄도 안 들어가면 탭 줄을 접는다.
            if (list < FontSize * 3)
            {
                head = false;
                list = KeyboardFit.ListRoom(Tall, 0, room, chrome + Main.Gutter - heads);
            }
        }

        _head.Visible = head;
        _scroll.Visible = list > 0;

        if (!Mathf.IsEqualApprox(list, _scroll.CustomMinimumSize.Y) || !Mathf.IsEqualApprox(lift, _lift))
        {
            _scroll.CustomMinimumSize = new Vector2(0, list);
            _lift = lift;
            CallDeferred(MethodName.ScrollToEnd);
        }
    }
}
