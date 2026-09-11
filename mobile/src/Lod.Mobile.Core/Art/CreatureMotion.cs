namespace Lod.Mobile.Core.Art;

/// <summary>
/// Which drawing a creature shows while it stands, walks and swings. Unlike a person — whose sheet always
/// holds the same ten drawings in the same order, which is what <see cref="WalkMotion" /> knows — every
/// creature numbers its own: one walks on frames 0..2 and swings on 6, the next walks on 0..4 and swings
/// on 10. Its own file says which, in the header of <c>MNS###.MPF</c>, and the extractor writes that out
/// beside the picture so this can be read rather than guessed.
/// </summary>
/// <remarks>
/// Guessing is what went wrong before: creatures were played with the person's numbering, which asked for
/// frames 6 to 9 of an eight-frame sheet and drew nothing at all for half of every step.
/// docs/original-sprite-animation.md 4절 has the header layout and what <c>MobTile.tbl</c> adds to it.
/// </remarks>
public sealed record CreatureMotion(
    int Frames,
    int StandStart,
    int StandCount,
    int WalkStart,
    int WalkCount,
    int AttackStart,
    int AttackCount)
{
    /// <summary>
    /// Reads what the extractor wrote beside the picture. Returns nothing when there is no frame count to
    /// read — a creature with no frames is not a creature, and saying so beats drawing an empty tile.
    /// </summary>
    public static CreatureMotion? Read(string text)
    {
        int frames = 0;
        int[] stand = [0, 0];
        int[] walk = [0, 0];
        int[] attack = [0, 0];

        foreach (string line in text.Split('\n'))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 2 && parts[0] == "frames" && int.TryParse(parts[1], out int counted))
            {
                frames = counted;
                continue;
            }

            if (parts.Length != 3
                || !int.TryParse(parts[1], out int start)
                || !int.TryParse(parts[2], out int count))
            {
                continue;
            }

            switch (parts[0])
            {
                case "stand": stand = [start, count]; break;
                case "walk": walk = [start, count]; break;
                case "attack": attack = [start, count]; break;
            }
        }

        return frames > 0
            ? new CreatureMotion(frames, stand[0], stand[1], walk[0], walk[1], attack[0], attack[1])
            : null;
    }

    /// <summary>
    /// The drawing to show while it is still. A creature with no standing frames may not stand still — the
    /// original's <c>fStop</c> is 0 for a wasp because its wings have to keep going — so it shows the first
    /// of its walking frames rather than a blank.
    /// </summary>
    public int Stand() => Inside(StandCount > 0 ? StandStart : WalkStart);

    /// <summary>One step of the walk, starting again at the beginning when it runs out.</summary>
    public int Walk(int step) => WalkCount > 0 ? Inside(WalkStart + Cycle(step, WalkCount)) : Stand();

    /// <summary>One step of a blow. A creature with none drawn simply stays as it was.</summary>
    public int Strike(int step) => AttackCount > 0 ? Inside(AttackStart + Cycle(step, AttackCount)) : Stand();

    /// <summary>A step that has gone backwards still lands on a real frame.</summary>
    private static int Cycle(int step, int count) => ((step % count) + count) % count;

    private int Inside(int frame) => Math.Clamp(frame, 0, Frames - 1);
}
