using Godot;

namespace LodClient;

/// <summary>
/// The bar the original draws over somebody's head when they are struck — the only place a player can see how
/// far along a fight is. The server sends it as <c>0x13</c> with what is left as a percentage; it shows for a
/// moment and fades, so a quiet field is not covered in bars.
/// </summary>
/// <remarks>
/// Sits on the actor, so it walks with whoever it belongs to. It is drawn rather than built out of nodes: one
/// bar over each of thirty monsters would otherwise be thirty more things to lay out every frame.
/// </remarks>
public sealed partial class HealthBar : Node2D
{
    /// <summary>How wide the bar is. A tile is 32 across, and the bar sits a little inside that.</summary>
    private const int Width = 26;

    private const int Height = 3;

    /// <summary>How long it stays at full strength, and how long it takes to fade after that.</summary>
    private const double StaySeconds = 3.0;

    private const double FadeSeconds = 1.0;

    private static readonly Color Backing = new(0.05f, 0.05f, 0.06f, 0.85f);
    private static readonly Color Edge = new(0f, 0f, 0f, 0.9f);

    /// <summary>
    /// The theme's own health colour while healthy, amber when worn down, red when nearly gone — the values the
    /// 4.51 mockup settles on (data/ui-vault). Colour is never the only sign: the length of the bar says the
    /// same thing, and that is what somebody who cannot tell the colours apart reads.
    /// </summary>
    private static readonly Color Hale = new("#c8783c");

    private static readonly Color Worn = new("#e0b336");
    private static readonly Color Dying = new("#d24439");

    private int _left = 100;
    private double _shown = double.MaxValue;

    /// <summary>Says somebody was struck and this much of them is left, as a percentage.</summary>
    public void Struck(int left)
    {
        _left = Mathf.Clamp(left, 0, 100);
        _shown = 0;
        Visible = true;
        Modulate = Colors.White;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _shown += delta;

        if (_shown > StaySeconds + FadeSeconds)
        {
            Visible = false;
            return;
        }

        Modulate = new Color(1f, 1f, 1f, _shown <= StaySeconds
            ? 1f
            : (float)(1.0 - (_shown - StaySeconds) / FadeSeconds));
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-Width / 2f - 1, -1, Width + 2, Height + 2), Edge);
        DrawRect(new Rect2(-Width / 2f, 0, Width, Height), Backing);

        if (_left <= 0)
        {
            return;
        }

        Color paint = _left > 60 ? Hale : _left > 25 ? Worn : Dying;

        DrawRect(new Rect2(-Width / 2f, 0, Width * (_left / 100f), Height), paint);
    }
}
