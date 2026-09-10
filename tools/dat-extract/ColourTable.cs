using System.Drawing;

namespace Lod.DatExtract;

/// <summary>
/// A dye table: one numbered entry per colour, each a handful of colours. Dyeing a wardrobe piece means
/// overwriting a fixed run of palette entries with one of these, which is why a hat can be any colour
/// without a second drawing of it.
/// </summary>
/// <remarks>
/// The first palette entry a dye replaces is 98 — DALib's <c>CONSTANTS.PALETTE_DYE_INDEX_START</c>, and
/// <c>Palette.Dye</c> there does exactly this overwrite. The tables are <c>color.tbl</c> and
/// <c>color0.tbl</c> in Legend.dat; the second one opens with the number of colours per entry and the
/// first does not, so both shapes are read.
/// </remarks>
internal static class ColourTable
{
    public const int FirstDyedIndex = 98;

    /// <summary>
    /// What to draw in the dyed slots when the picture is going to be recoloured later. Six colours no
    /// palette in these archives uses, so the client can find them by their exact value and swap in the
    /// real dye. A sheet cut this way is not meant to be looked at as it is.
    /// </summary>
    public static readonly Color[] Markers =
    [
        Color.FromArgb(255, 0, 250),
        Color.FromArgb(255, 0, 251),
        Color.FromArgb(255, 0, 252),
        Color.FromArgb(255, 0, 253),
        Color.FromArgb(255, 0, 254),
        Color.FromArgb(255, 0, 255)
    ];

    public static Dictionary<int, Color[]> Read(string path)
    {
        string[] lines = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        Dictionary<int, Color[]> table = [];
        List<Color> colours = [];
        int entry = -1;

        foreach (string line in lines)
        {
            if (line.Contains(','))
            {
                string[] parts = line.Split(',');

                if (parts.Length == 3
                    && int.TryParse(parts[0], out int red)
                    && int.TryParse(parts[1], out int green)
                    && int.TryParse(parts[2], out int blue))
                {
                    colours.Add(Color.FromArgb(red % 256, green % 256, blue % 256));
                }

                continue;
            }

            if (entry >= 0)
            {
                table[entry] = [.. colours];
            }

            colours.Clear();
            entry = int.TryParse(line, out int number) ? number : -1;
        }

        if (entry >= 0)
        {
            table[entry] = [.. colours];
        }

        // A leading line saying how many colours an entry has looks exactly like an entry with none.
        foreach (int empty in table.Where(pair => pair.Value.Length == 0).Select(pair => pair.Key).ToList())
        {
            table.Remove(empty);
        }

        return table;
    }
}
