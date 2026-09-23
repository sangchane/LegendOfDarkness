using Godot;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// One automatic-potion switch on the game screen, so it is never more than one touch away.
/// Tap turns it on or off; press and hold, then slide up or down, moves the line in steps of five.
/// </summary>
/// <remarks>
/// The rule is read and written through Main, which keeps the one copy that is remembered on the device.
/// </remarks>
public partial class PotionChip : Button
{
    private const ulong HoldMilliseconds = 400;
    private const float PixelsPerStep = 12;
    private const int Step = 5;

    private readonly string _name;
    private readonly Color _paint;
    private readonly System.Func<PotionRule> _read;
    private readonly System.Action<PotionRule> _write;

    private ulong _downAt;
    private float _downY;
    private bool _down;
    private bool _adjusting;
    private int _percent;

    public PotionChip(string name, Color paint, System.Func<PotionRule> read, System.Action<PotionRule> write)
    {
        _name = name;
        _paint = paint;
        _read = read;
        _write = write;
        CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum);
        Greybox.Plain(this);
        Show(_read());
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button when button.Pressed:
                _down = true;
                _adjusting = false;
                _downAt = Time.GetTicksMsec();
                _downY = button.Position.Y;
                _percent = _read().Percent;
                AcceptEvent();
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } when _down:
                _down = false;
                PotionRule now = _read();
                // 길게 눌러 옮긴 줄은 켜진 채로 남는다 — 줄을 옮긴 것은 쓰겠다는 뜻이다.
                _write(_adjusting ? new PotionRule(true, _percent) : now with { Enabled = !now.Enabled });
                _adjusting = false;
                Show(_read());
                AcceptEvent();
                break;

            case InputEventMouseMotion motion when _adjusting:
                int steps = Mathf.RoundToInt((_downY - motion.Position.Y) / PixelsPerStep);
                _percent = Mathf.Clamp(_read().Percent + steps * Step, Step, 100 - Step);
                Text = $"▲{_percent}%▼";
                AcceptEvent();
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (_down && !_adjusting && Time.GetTicksMsec() - _downAt >= HoldMilliseconds)
        {
            _adjusting = true;
            Text = $"▲{_percent}%▼";
        }
    }

    /// <summary>On: the line in the bar's colour. Off: greyed, and the word says so — colour is not the only sign.</summary>
    private void Show(PotionRule rule)
    {
        Text = rule.Enabled ? $"{_name} {rule.Percent}%" : $"{_name} 끔";
        Color ink = rule.Enabled ? _paint.Lightened(0.25f) : Greybox.Muted;
        AddThemeColorOverride("font_color", ink);
        AddThemeColorOverride("font_hover_color", ink);
        AddThemeColorOverride("font_pressed_color", ink);
    }
}
