namespace Lod.DatExtract;

/// <summary>
/// The floor colours that turn over — water, mostly. 5.99 seo.dat carries a handful of <c>mpt%04d.tbl</c> beside the
/// palettes (mpt0018 · 0027 · 0036 · 0039 · 0056); the number is the palette's and each line is <c>처음 끝 빠르기</c>,
/// a run of palette entries and how many 100 ms ticks pass between two turns ("206 219 2").
/// </summary>
/// <remarks>
/// What Legend.exe does with them (5.99, addresses in the exe): the loader at 0x5dab02 reads mpt%04d.tbl for every
/// mpt palette, up to ten lines each, into {처음, 끝, 빠르기, 셈}. The handler at 0x5df450 runs on a message it re-posts to
/// itself 100 later (0x5df867 `push 100` → 0x62e220 → 0x4ac9b0, which adds the queue's clock, timeGetTime); each run
/// adds one to every line's count and, once the count reaches 빠르기, sets it back to 0 and turns the run by one:
/// the last entry's colour goes to the first and every other moves up one (0x5df563~0x5df5e8). 처음 is held at 1 or
/// more and 끝 at 255 or less; a run of one entry, or 빠르기 0, never turns.
/// </remarks>
internal sealed class PaletteCycles
{
    /// <summary>How often the original looks at its counts, in milliseconds.</summary>
    public const int TickMilliseconds = 100;

    private readonly Dictionary<int, List<Cycle>> _byPalette = [];

    /// <summary>One run of palette entries that turns over, and how many ticks each turn waits.</summary>
    public readonly record struct Cycle(int First, int Last, int Ticks)
    {
        public int Length => Last - First + 1;

        public bool Holds(int index) => index >= First && index <= Last;
    }

    public IReadOnlyCollection<int> Palettes => _byPalette.Keys;

    public static PaletteCycles FromArchive(IEnumerable<(string Name, byte[] Data)> entries)
    {
        PaletteCycles cycles = new();

        foreach ((string name, byte[] data) in entries)
        {
            if (PaletteOf(name) is int palette && Read(System.Text.Encoding.ASCII.GetString(data)) is { Count: > 0 } runs)
            {
                cycles._byPalette[palette] = runs;
            }
        }

        return cycles;
    }

    /// <summary>The palette a table belongs to — <c>mpt0018.tbl</c> is palette 18. <c>mptpal.tbl</c> is not one.</summary>
    internal static int? PaletteOf(string name)
    {
        string stem = Path.GetFileNameWithoutExtension(name);

        return name.EndsWith(".tbl", StringComparison.OrdinalIgnoreCase)
               && stem.StartsWith(Program.FloorPaletteFamily, StringComparison.OrdinalIgnoreCase)
               && int.TryParse(stem[Program.FloorPaletteFamily.Length..], out int number)
            ? number
            : null;
    }

    /// <summary>
    /// The runs in one table, as the client keeps them: at most ten, 처음 held at 1 or more and 끝 at 255 or less, and
    /// only those that will ever turn.
    /// </summary>
    internal static List<Cycle> Read(string table)
    {
        List<Cycle> runs = [];

        foreach (string line in table.Split('\n'))
        {
            int[] numbers = [.. line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => int.TryParse(word, out int value) ? value : -1)];

            if (numbers.Length != 3 || numbers.Contains(-1) || runs.Count == 10)
            {
                continue;
            }

            Cycle run = new(Math.Max(numbers[0], 1), Math.Min(numbers[1], 255), numbers[2]);

            if (run.First < run.Last && run.Ticks > 0)
            {
                runs.Add(run);
            }
        }

        return runs;
    }

    /// <summary>The run a palette entry turns with, if it does.</summary>
    public Cycle? RunOf(int palette, int index) =>
        _byPalette.TryGetValue(palette, out List<Cycle>? runs) && runs.FirstOrDefault(run => run.Holds(index)) is { Length: > 1 } found
            ? found
            : null;

    /// <summary>
    /// Which original entry's colour a palette entry shows after this many turns — the same sum the client's shader
    /// does. One turn moves every colour one entry up and the last one round to the first.
    /// </summary>
    public static int Shown(Cycle run, int index, long turns) =>
        run.First + (int)((((index - run.First - turns) % run.Length) + run.Length) % run.Length);
}
