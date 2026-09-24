using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Art;

/// <summary>Whose number it is, which decides its colour.</summary>
public enum FigureTone
{
    /// <summary>What our own blow took from somebody.</summary>
    Dealt,

    /// <summary>What somebody else's blow took from somebody else.</summary>
    Seen,

    /// <summary>What a blow took from us.</summary>
    Taken,

    /// <summary>Health given back to anybody.</summary>
    Healed,
}

/// <summary>
/// The numbers that float up over somebody when a blow lands or a heal comes in (0x5D, our server's own packet).
/// They start just above whatever already stands over the head (<see cref="Overhead" />) — the bar when it is up,
/// the badges when there are any — so they never cover the bar, a badge, or the head slot where Miss and the coma
/// play; then they rise and fade.
/// </summary>
public static class FloatingFigure
{
    /// <summary>How long one number lives.</summary>
    public const double Seconds = 0.8;

    /// <summary>How long it stays fully drawn before it starts to fade.</summary>
    public const double Holds = 0.45;

    /// <summary>How far it rises over its life, in pixels.</summary>
    public const float Rise = 22;

    /// <summary>How far apart two numbers on the same one sit when they come close together.</summary>
    public const float Line = 12;

    /// <summary>Numbers younger than this on the same one push a new number up a line so they do not overprint.</summary>
    public const double Crowded = 0.3;

    public static FigureTone Tone(Figure figure, uint self) =>
        figure.Kind == FigureKind.Heal ? FigureTone.Healed
        : figure.Target == self ? FigureTone.Taken
        : figure.Source == self ? FigureTone.Dealt
        : FigureTone.Seen;

    public static string Text(Figure figure) =>
        figure.Kind == FigureKind.Heal ? $"+{figure.Amount}" : figure.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Where a new number's baseline starts, from the feet (negative is up): <see cref="Overhead.Gap" /> above the
    /// highest thing over the head now.
    /// </summary>
    /// <param name="barShown">Whether the health bar is up.</param>
    /// <param name="badgesShown">Whether any badge is drawn.</param>
    public static float Start(float headTop, bool barShown, bool badgesShown, float barHeight, float rowHeight)
    {
        (float bar, float badges) = Overhead.Place(headTop, barShown, barHeight, rowHeight);
        float top = badgesShown ? badges : barShown ? bar : headTop - Overhead.Gap - Overhead.SlotHeight;

        return top - Overhead.Gap;
    }

    /// <summary>
    /// How far up a number has risen and how strongly it is drawn at <paramref name="age" /> seconds: it rises
    /// quickly then slows, holds, then fades out by <see cref="Seconds" />.
    /// </summary>
    public static (float Lift, float Alpha) At(double age)
    {
        double t = Math.Clamp(age / Seconds, 0, 1);
        float lift = (float)(Rise * (1 - (1 - t) * (1 - t)));
        float alpha = age <= Holds ? 1f : (float)Math.Clamp(1 - (age - Holds) / (Seconds - Holds), 0, 1);

        return (lift, alpha);
    }
}
