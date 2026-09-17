namespace Lod.Mobile.Core.Art;

/// <summary>
/// A face or a balloon shown over a person's head when the server sends an emote as a body motion (0x1A, 9 and
/// up): which drawings of emot01.epf, how many, and how long each is held. The body itself stands.
/// </summary>
/// <remarks>
/// The original keeps a row per emote (Legend.exe 2005 0x869880: number, first drawing, drawings + 1, milliseconds
/// per drawing) and finds the first drawing by adding up the rows before it (0x4e1226); a row whose own first
/// drawing is -1 carries on from there. 18~20 have rows with no drawing. The drawings bear the order out — the snore
/// bubble grows and shrinks over 7 and 8, the impatient dots count up over 15, 16 and 17.
/// docs/original-sprite-animation.md 3.5절.
/// </remarks>
public sealed record Emote(int Frame, int Drawings, double SecondsPerDrawing)
{
    public static Emote? Of(int number) =>
        number switch
        {
            >= 9 and <= 15 => new Emote(number - 9, 1, 1.5),
            16 => new Emote(7, 2, 1.0),
            17 => new Emote(9, 2, 1.0),
            >= 23 and <= 26 => new Emote(number - 12, 1, 1.5),
            27 => new Emote(15, 3, 0.5),
            >= 28 and <= 41 => new Emote(number - 10, 1, 1.5),
            42 => new Emote(32, 3, 0.5),
            43 => new Emote(35, 3, 0.5),
            44 => new Emote(38, 4, 0.5),
            _ => null
        };

    /// <summary>How long the emote stays up.</summary>
    public double Seconds => Drawings * SecondsPerDrawing;

    /// <summary>The drawing showing this long after the emote began; the last one once its time is up.</summary>
    public int FrameAt(double seconds) =>
        Frame + Math.Clamp((int)(seconds / SecondsPerDrawing), 0, Drawings - 1);
}
