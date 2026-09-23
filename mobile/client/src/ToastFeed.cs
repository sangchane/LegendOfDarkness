using Godot;

namespace LodClient;

/// <summary>
/// What was just gained or lost — "쿠룸 +1", "금화 +120" — stacked down one side and fading on its own, the way mobile
/// games show loot. It never takes a tap: the floor under it stays the floor.
/// </summary>
public sealed partial class ToastFeed : VBoxContainer
{
    private const int FontSize = 13;
    private const int Most = 4;

    private const double StaysFor = 3;
    private const double FadesFor = 0.8;

    private readonly List<(Label Line, double Age)> _shown = [];

    public ToastFeed()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 2);
    }

    public void Add(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        Label line = new()
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = MouseFilterEnum.Ignore,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            ClipText = true
        };

        // 얻은 것은 원작 구슬에서 뽑은 강조색(체력 색)으로 — 창에는 색이 없어 강조색은 거기서만 나온다(docs/original-ui-451.md).
        MessageLog.Outlined(line, FontSize, Greybox.Accent.Lightened(0.25f));

        AddChild(line);
        _shown.Add((line, 0));

        while (_shown.Count > Most)
        {
            _shown[0].Line.QueueFree();
            _shown.RemoveAt(0);
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
    }
}
