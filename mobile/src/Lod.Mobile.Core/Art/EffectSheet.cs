namespace Lod.Mobile.Core.Art;

/// <summary>
/// One effect's drawings and the order they are shown in. The picture is a row of <see cref="Frames" /> drawings;
/// the original plays them in the order effect.tbl gives for that effect number (203 is "0 1 1"), one step per
/// interval the server asks for. With no order written, every drawing is shown once in turn.
/// </summary>
public sealed record EffectSheet(int Frames, IReadOnlyList<int> Order)
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
    /// Reads what scripts/build-client-effects.py writes: "number drawings order…" per line, '#' lines are notes.
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

            if (numbers.Length < 2 || numbers.Any(value => value < 0))
            {
                continue;
            }

            sheets[numbers[0]] = new EffectSheet(numbers[1], numbers[2..]);
        }

        return sheets;
    }
}
