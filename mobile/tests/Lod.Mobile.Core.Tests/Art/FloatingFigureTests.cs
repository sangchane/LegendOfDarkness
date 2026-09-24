using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>Floating damage and heal numbers: their colour, their words, where they start and how they fade.</summary>
public sealed class FloatingFigureTests
{
    private const uint Me = 100;
    private const uint Monster = 200;
    private const uint Friend = 300;

    [Fact]
    public void Colour_follows_whose_number_it_is()
    {
        Assert.Equal(FigureTone.Dealt, FloatingFigure.Tone(new Figure(Monster, Me, 5, FigureKind.Damage), Me));
        Assert.Equal(FigureTone.Seen, FloatingFigure.Tone(new Figure(Monster, Friend, 5, FigureKind.Damage), Me));
        Assert.Equal(FigureTone.Taken, FloatingFigure.Tone(new Figure(Me, Monster, 5, FigureKind.Damage), Me));
        Assert.Equal(FigureTone.Healed, FloatingFigure.Tone(new Figure(Me, Me, 5, FigureKind.Heal), Me));
        Assert.Equal(FigureTone.Healed, FloatingFigure.Tone(new Figure(Friend, Me, 5, FigureKind.Heal), Me));
    }

    [Fact]
    public void A_heal_reads_plus_and_a_blow_reads_the_bare_number()
    {
        Assert.Equal("+120", FloatingFigure.Text(new Figure(Me, 0, 120, FigureKind.Heal)));
        Assert.Equal("1234", FloatingFigure.Text(new Figure(Monster, Me, 1234, FigureKind.Damage)));
    }

    /// <summary>It never starts on the bar, a badge or the head slot: always above the highest of them.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void It_starts_above_whatever_stands_over_the_head(bool bar, bool badges)
    {
        const float head = -60;
        const float barHeight = 5;
        const float rowHeight = 12;
        (float barTop, float badgeTop) = Overhead.Place(head, bar, barHeight, rowHeight);
        float slotTop = head - Overhead.Gap - Overhead.SlotHeight;
        float highest = Math.Min(slotTop, Math.Min(bar ? barTop : 0, badges ? badgeTop : 0));

        float start = FloatingFigure.Start(head, bar, badges, barHeight, rowHeight);

        Assert.Equal(highest - Overhead.Gap, start);
    }

    [Fact]
    public void It_rises_holds_then_fades_out_in_under_a_second()
    {
        Assert.Equal((0f, 1f), FloatingFigure.At(0));

        (float midLift, float midAlpha) = FloatingFigure.At(FloatingFigure.Holds);
        Assert.InRange(midLift, 1, FloatingFigure.Rise);
        Assert.Equal(1f, midAlpha);

        (float endLift, float endAlpha) = FloatingFigure.At(FloatingFigure.Seconds);
        Assert.Equal(FloatingFigure.Rise, endLift);
        Assert.Equal(0f, endAlpha);
        Assert.True(FloatingFigure.Seconds < 1);
    }
}
