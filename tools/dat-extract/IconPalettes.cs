namespace Lod.DatExtract;

/// <summary>
/// The <c>*pal.tbl</c> files say which palette a numbered tile uses. Each line is a range and a palette
/// number: two words mean a single tile, three mean either a range or a tile with a mood, four mean both.
/// A tile that no line covers uses palette 0.
/// </summary>
internal static class IconPalettes
{
    private readonly record struct Row(int Lowest, int Highest, int Palette, int Mood);

    /// <summary>Palette number for one tile, or 0 when no line covers it.</summary>
    public static int PaletteFor(byte[] table, int tile, int mood = 0)
    {
        foreach (Row row in Rows(table))
        {
            if (tile >= row.Lowest && tile <= row.Highest && (mood == row.Mood || row.Mood == 0))
            {
                return row.Palette;
            }
        }

        return 0;
    }

    private static List<Row> Rows(byte[] table)
    {
        List<Row> rows = [];

        foreach (string line in System.Text.Encoding.ASCII.GetString(table)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 2 || !int.TryParse(words[0], out int lowest))
            {
                continue;
            }

            switch (words.Length)
            {
                case 2:
                    rows.Add(new Row(lowest, lowest, int.Parse(words[1]), 0));
                    break;

                // Three words are ambiguous: a negative last word is a mood, otherwise it closes a range.
                case 3:
                    int second = int.Parse(words[1]);
                    int third = int.Parse(words[2]);
                    rows.Add(third < 0
                        ? new Row(lowest, lowest, second, third)
                        : new Row(lowest, second, third, 0));
                    break;

                default:
                    rows.Add(new Row(lowest, int.Parse(words[1]), int.Parse(words[2]), int.Parse(words[3])));
                    break;
            }
        }

        return rows;
    }
}
