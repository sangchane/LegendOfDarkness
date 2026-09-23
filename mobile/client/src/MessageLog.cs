using Godot;

namespace LodClient;

/// <summary>
/// The ticker: the last two things worth seeing, as outlined words straight on the floor — no box. Each line stays a
/// moment and then fades, so once nothing new has been said the floor is all there is (사용자, 2026-09-23: 검은 기록판이
/// 화면을 가려 몰입이 깨진다). Tapping it opens the full log; everything the ticker leaves out is there.
/// </summary>
/// <remarks>
/// Only what <see cref="Lod.Mobile.Core.World.MessageSort" /> sends to the ticker lands here — what was gained goes to the
/// toasts, a level or a death to the banner, speech over the speaker's head, and the noise to the log alone.
/// </remarks>
public sealed partial class MessageLog : VBoxContainer
{
    private const int FontSize = 14;

    /// <summary>How long a line stays fully readable, and how long it then takes to fade.</summary>
    private const double StaysFor = 4;
    private const double FadesFor = 1;

    private readonly int _most;
    private readonly bool _wraps;
    private readonly List<(Label Line, double Age)> _shown = [];

    /// <param name="most">How many lines at once.</param>
    /// <param name="wraps">
    /// Whether a long line may take a second row. Portrait keeps one row each: the ticker's height decides where the
    /// character stands (GameScreen.FocusY), and a ticker that grew would move the character with every line.
    /// </param>
    public MessageLog(int most, bool wraps)
    {
        _most = most;
        _wraps = wraps;
        Alignment = AlignmentMode.End;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 0);
    }

    /// <summary>Somebody tapped the lines — the screen opens the full log.</summary>
    public event Action? Tapped;

    public void Add(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        Label line = new()
        {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = _wraps ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            ClipText = !_wraps,
            MaxLinesVisible = _wraps ? 2 : 1
        };
        Outlined(line, FontSize, Greybox.Text);

        AddChild(line);
        _shown.Add((line, 0));

        while (_shown.Count > _most)
        {
            _shown[0].Line.QueueFree();
            _shown.RemoveAt(0);
        }
    }

    /// <summary>
    /// Words that stay readable over any floor without a plate behind them: a dark outline and a soft shadow, the way
    /// mobile games write over the world.
    /// </summary>
    public static void Outlined(Label label, int size, Color colour)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } or InputEventScreenTouch { Pressed: true })
        {
            Tapped?.Invoke();
            AcceptEvent();
        }
    }

    public override void _Process(double delta)
    {
        for (int index = _shown.Count - 1; index >= 0; index--)
        {
            (Label line, double age) = _shown[index];
            age += delta;

            if (age >= StaysFor + FadesFor)
            {
                line.QueueFree();
                _shown.RemoveAt(index);
                continue;
            }

            line.Modulate = new Color(1, 1, 1, 1f - (float)Math.Clamp((age - StaysFor) / FadesFor, 0, 1));
            _shown[index] = (line, age);
        }

        // 보이는 줄이 있을 때만 탭을 받는다 — 빈 자리가 바닥을 누르는 손가락을 가로채면 안 된다.
        MouseFilter = _shown.Count > 0 ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }
}
