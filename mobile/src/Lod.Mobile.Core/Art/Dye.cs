namespace Lod.Mobile.Core.Art;

/// <summary>One colour, as the original's tables write them: three numbers, no transparency.</summary>
public readonly record struct Colour(byte R, byte G, byte B);

/// <summary>
/// The original recolours a piece of clothing without drawing it twice: six entries of the palette it was
/// drawn with are simply overwritten with a numbered set of colours. <c>color0.tbl</c> in Legend.dat is
/// that set, one numbered entry per colour.
/// </summary>
/// <remarks>
/// The sheets in this repository are cut with those six entries left as a marker colour
/// (<c>dat-extract pose … marker</c>), so recolouring at run time is finding those exact colours and
/// putting the wanted entry in their place.
/// </remarks>
public static class DyeTable
{
    /// <summary>Reads a file of nothing but colours, one per line — the marker list.</summary>
    public static IReadOnlyList<Colour> ReadColours(string text) =>
        [.. Lines(text).Select(Parse).Where(colour => colour is not null).Select(colour => colour!.Value)];

    /// <summary>
    /// Reads a numbered table. A line with no comma opens an entry; the colour lines under it belong to
    /// it. A leading line saying how many colours an entry holds looks exactly like an entry with none, so
    /// entries without colours are dropped.
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<Colour>> Read(string text)
    {
        Dictionary<int, IReadOnlyList<Colour>> table = [];
        List<Colour> colours = [];
        int entry = -1;

        foreach (string line in Lines(text))
        {
            if (Parse(line) is { } colour)
            {
                colours.Add(colour);
                continue;
            }

            Close();
            entry = int.TryParse(line, out int number) ? number : -1;
        }

        Close();

        return table;

        void Close()
        {
            if (entry >= 0 && colours.Count > 0)
            {
                table[entry] = [.. colours];
            }

            colours.Clear();
        }
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);

    private static Colour? Parse(string line)
    {
        string[] parts = line.Split(',');

        return parts.Length == 3
               && byte.TryParse(parts[0].Trim(), out byte red)
               && byte.TryParse(parts[1].Trim(), out byte green)
               && byte.TryParse(parts[2].Trim(), out byte blue)
            ? new Colour(red, green, blue)
            : null;
    }
}
