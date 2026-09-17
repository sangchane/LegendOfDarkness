using Godot;

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
        CustomMinimumSize = new Vector2(0, Main.TouchMinimum)
    };

    private readonly Button _all = Tab("전체");
    private readonly Button _speech = Tab("대화");
    private readonly Button _system = Tab("시스템");

    // 어느 탭이 눌렸나, 그리고 그 탭으로 몇 줄을 적어 두었나. 늘어난 만큼만 덧붙인다.
    private bool _onlySpeech;
    private bool _onlySystem;
    private int _written;
    private int _read;

    public ChatPanel()
    {
        Name = "Chat";
        Visible = false;
        AddThemeStyleboxOverride("panel", Greybox.Sheet());

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", Main.Gutter);

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter);
        _all.Pressed += () => Choose(speech: false, system: false);
        _speech.Pressed += () => Choose(speech: true, system: false);
        _system.Pressed += () => Choose(speech: false, system: true);

        head.AddChild(_all);
        head.AddChild(_speech);
        head.AddChild(_system);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        Close = new Button { Text = "닫기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        head.AddChild(Close);

        _lines.AddThemeConstantOverride("separation", 2);
        _scroll.CustomMinimumSize = new Vector2(0, Tall);
        _scroll.AddChild(_lines);

        Button send = new() { Text = "보내기", CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum) };
        send.Pressed += Say;
        _typed.TextSubmitted += _ => Say();

        HBoxContainer typing = new();
        typing.AddThemeConstantOverride("separation", Main.Gutter);
        typing.AddChild(_typed);
        typing.AddChild(send);

        body.AddChild(head);
        body.AddChild(_scroll);
        body.AddChild(typing);
        AddChild(body);

        Choose(speech: false, system: false);
    }

    /// <summary>The button that shuts the panel, so whoever opened it decides what that means.</summary>
    public Button Close { get; }

    /// <summary>Somebody wrote a line and asked to say it. What comes of it is the server's to say.</summary>
    public event Action<string>? Sent;

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

    private static Button Tab(string name) => new()
    {
        Text = name,
        ToggleMode = true,
        CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
    };

    /// <summary>Shows one kind of line, or all of them. The list is written again, because a different lot belongs in it.</summary>
    private void Choose(bool speech, bool system)
    {
        _onlySpeech = speech;
        _onlySystem = system;
        _all.ButtonPressed = !speech && !system;
        _speech.ButtonPressed = speech;
        _system.ButtonPressed = system;
        _written = -1;
    }

    /// <summary>
    /// Shows every line said so far, oldest at the top. Only the new ones are added — rebuilding the lot every frame
    /// would throw away where the reader had scrolled to.
    /// </summary>
    public void Show(IReadOnlyList<(bool Speech, string Text)> said)
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
            if ((_onlySpeech && !said[index].Speech) || (_onlySystem && said[index].Speech))
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
}
