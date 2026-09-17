using Godot;

namespace LodClient;

/// <summary>
/// The last few things said — by the server, or by this screen when it refuses something. Each line stays a moment, then
/// fades out the way the movement pad does, and the plate behind them fades with the last line, so the floor comes back
/// once nothing new has been said.
/// </summary>
/// <remarks>
/// The box keeps its size while empty. In portrait the character is stood in the middle of the part above it, and a
/// box that came and went would move the character each time.
/// </remarks>
public sealed partial class MessageLog : PanelContainer
{
    private const int FontSize = 14;

    /// <summary>How long a line stays fully readable, and how long it then takes to fade.</summary>
    private const double StaysFor = 4;
    private const double FadesFor = 1.5;

    private readonly int _most;
    private readonly VBoxContainer _lines = new() { Alignment = BoxContainer.AlignmentMode.End };
    private readonly List<(Label Line, double Age)> _shown = [];

    public MessageLog(int most)
    {
        _most = most;
        MouseFilter = MouseFilterEnum.Ignore;
        SelfModulate = Colors.Transparent;

        AddThemeStyleboxOverride("panel", Greybox.Plate());
        _lines.AddThemeConstantOverride("separation", 2);
        AddChild(_lines);
    }

    public void Add(string text)
    {
        text = Clean(text);

        if (text.Length == 0)
        {
            return;
        }

        Label line = new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        line.AddThemeFontSizeOverride("font_size", FontSize);
        line.AddThemeColorOverride("font_color", Greybox.Muted);

        _lines.AddChild(line);
        _shown.Add((line, 0));

        while (_shown.Count > _most)
        {
            _shown[0].Line.QueueFree();
            _shown.RemoveAt(0);
        }
    }

    /// <summary>
    /// Throws away what cannot be read. The server sends lines that are a single zero byte, or a bare newline (실제
    /// 서버에서 봤다) — as a line each of those is an empty plate lying over the map.
    /// </summary>
    public static string Clean(string text) => new string([.. text.Where(letter => !char.IsControl(letter))]).Trim();

    public override void _Process(double delta)
    {
        float brightest = 0;

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

            float alpha = 1f - (float)Math.Clamp((age - StaysFor) / FadesFor, 0, 1);
            line.Modulate = new Color(1, 1, 1, alpha);
            brightest = Math.Max(brightest, alpha);
            _shown[index] = (line, age);
        }

        SelfModulate = new Color(1, 1, 1, brightest);
    }
}
