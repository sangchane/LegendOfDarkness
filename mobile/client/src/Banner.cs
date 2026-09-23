using Godot;

namespace LodClient;

/// <summary>
/// One short line in the middle of the screen for what must not be missed — a level, a coma, a death, the name of the
/// place just entered. It comes, stays a moment and goes; it never takes a tap.
/// </summary>
public sealed partial class Banner : Label
{
    private const int FontSize = 22;

    private const double FadesIn = 0.2;
    private const double StaysFor = 2.2;
    private const double FadesFor = 0.8;

    /// <summary>
    /// The same words again this soon are not shown again. The server repeats the coma lines every tick
    /// (debuff_reeping.cs:92), and a banner each time would be a banner that never leaves.
    /// </summary>
    private const double RepeatAfter = 30;

    private readonly Dictionary<string, double> _lastShown = [];
    private double _clock;
    private double _age = double.MaxValue;

    public Banner()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        AutowrapMode = TextServer.AutowrapMode.WordSmart;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <param name="bright">A level: in the theme's accent, drawn from the original's orbs. Anything else in the title colour.</param>
    public void Show(string text, bool bright = false)
    {
        if (text.Length == 0 || (_lastShown.TryGetValue(text, out double at) && _clock - at < RepeatAfter))
        {
            return;
        }

        _lastShown[text] = _clock;
        Text = text;
        MessageLog.Outlined(this, FontSize, bright ? Greybox.Accent.Lightened(0.2f) : Greybox.Title.Lightened(0.3f));
        AddThemeConstantOverride("outline_size", 6);
        _age = 0;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        _clock += delta;

        if (!Visible)
        {
            return;
        }

        _age += delta;

        if (_age >= FadesIn + StaysFor + FadesFor)
        {
            Visible = false;
            return;
        }

        float alpha = _age < FadesIn
            ? (float)(_age / FadesIn)
            : 1f - (float)Math.Clamp((_age - FadesIn - StaysFor) / FadesFor, 0, 1);

        Modulate = new Color(1, 1, 1, alpha);
    }
}
