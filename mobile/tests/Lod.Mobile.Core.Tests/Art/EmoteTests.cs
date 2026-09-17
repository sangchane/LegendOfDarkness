using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

/// <summary>
/// Which drawings of emot01.epf an emote from the server (0x1A, 9 and up) shows over the head. The original keeps
/// one row per emote in a table (Legend.exe 2005 0x869880) and finds the first drawing by adding up the rows before
/// it; the reference repositories name the same numbers (Arbiter BodyAnimation: 9 Smile … 44 Confused).
/// </summary>
public sealed class EmoteTests
{
    [Theory]
    [InlineData(9, 0)]      // Smile — the first face
    [InlineData(15, 6)]     // Pleasant
    [InlineData(23, 11)]    // RockOn — the first balloon, a fist
    [InlineData(26, 14)]    // Ouch
    [InlineData(28, 18)]    // Shock
    [InlineData(30, 20)]    // Love — a heart
    [InlineData(41, 31)]    // StoneFaced
    public void A_one_drawing_emote_holds_its_drawing_for_a_second_and_a_half(int number, int frame)
    {
        Assert.Equal(new Emote(frame, 1, 1.5), Emote.Of(number));
    }

    [Theory]
    [InlineData(16, 7, 2, 1.0)]     // Snore — the bubble grows and shrinks
    [InlineData(17, 9, 2, 1.0)]     // Mouth
    [InlineData(27, 15, 3, 0.5)]    // Impatient — one dot, two, three
    [InlineData(42, 32, 3, 0.5)]    // Tears
    [InlineData(43, 35, 3, 0.5)]    // FiredUp
    [InlineData(44, 38, 4, 0.5)]    // Confused — the last drawings in the file
    public void An_emote_of_several_drawings_runs_through_them(int number, int frame, int drawings, double seconds)
    {
        Assert.Equal(new Emote(frame, drawings, seconds), Emote.Of(number));
    }

    /// <summary>6, 21 and 22 are body motions; 18~20 have a row with no drawing; the rest are no emote at all.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(18)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(45)]
    [InlineData(128)]
    public void Anything_else_is_no_emote(int number)
    {
        Assert.Null(Emote.Of(number));
    }

    [Fact]
    public void The_drawing_moves_on_with_time_and_stays_on_the_last()
    {
        Emote confused = Emote.Of(44)!;

        Assert.Equal([38, 38, 39, 41, 41], new[] { 0.0, 0.49, 0.5, 1.6, 9.0 }.Select(confused.FrameAt));
        Assert.Equal(2.0, confused.Seconds, 5);
    }
}
