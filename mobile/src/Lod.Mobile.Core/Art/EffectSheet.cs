namespace Lod.Mobile.Core.Art;

/// <summary>
/// One effect's drawings, where they sit, and the order they are shown in. The picture is a row of
/// <see cref="Frames" /> cells, each one the whole canvas the original drew that effect on; the original plays
/// them in the order effect.tbl gives for that effect number (203 is "0 1 1"), one step per interval the server
/// asks for. With no order written, every drawing is shown once in turn.
/// </summary>
/// <remarks>
/// <see cref="AnchorX" />/<see cref="AnchorY" /> is the point of the canvas that lands on whoever the effect was
/// cast at. It is what keeps an effect the size and place the original gave it: 일음지 (efct042) is a 13x13
/// sparkle high up a 111x85 canvas, so anchored at 55,70 it plays above the head. Cut out on its own and scaled
/// to a body it covered the whole target instead — which is what the screen did before the canvas was kept
/// (scripts/build-client-effects.py, docs/disassembly.md 4.51 0x44ba04).
/// </remarks>
public sealed record EffectSheet(int Frames, int Wide, int Tall, int AnchorX, int AnchorY, IReadOnlyList<int> Order)
{
    /// <summary>How many steps the effect lasts.</summary>
    public int Steps => Order.Count > 0 ? Order.Count : Frames;

    /// <summary>The drawing to show at this step, kept inside the sheet.</summary>
    public int FrameAt(int step)
    {
        int frame = Order.Count > 0 ? Order[Math.Clamp(step, 0, Order.Count - 1)] : step;
        return Math.Clamp(frame, 0, Math.Max(0, Frames - 1));
    }

    /// <summary>
    /// Reads what scripts/build-client-effects.py writes: "number drawings wide tall anchorX anchorY order…"
    /// per line, '#' lines are notes.
    /// </summary>
    public static IReadOnlyDictionary<int, EffectSheet> Read(string text)
    {
        Dictionary<int, EffectSheet> sheets = [];

        foreach (string line in text.Split('\n'))
        {
            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            int[] numbers = [.. line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => int.TryParse(part, out int value) ? value : -1)];

            // A row without its canvas and anchor cannot be placed, so it is passed over rather than drawn
            // somewhere invented.
            if (numbers.Length < 6 || numbers.Any(value => value < 0))
            {
                continue;
            }

            sheets[numbers[0]] = new EffectSheet(
                numbers[1], numbers[2], numbers[3], numbers[4], numbers[5], numbers[6..]);
        }

        return sheets;
    }
}
