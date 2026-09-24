namespace Lod.DatExtract.Tests;

/// <summary>
/// The floor colours that turn over (5.99 Legend.exe 0x5dab02 reads mpt%04d.tbl, 0x5df450 turns them every 100 ms).
/// The client's shader does the same sum as <see cref="PaletteCycles.Shown" />, so it is pinned here against the
/// original's one-step shift.
/// </summary>
public sealed class PaletteCyclesTests
{
    [Fact]
    public void A_table_is_named_after_its_palette_and_mptpal_is_not_one()
    {
        Assert.Equal(18, PaletteCycles.PaletteOf("mpt0018.tbl"));
        Assert.Equal(56, PaletteCycles.PaletteOf("MPT0056.TBL"));
        Assert.Null(PaletteCycles.PaletteOf("mptpal.tbl"));
        Assert.Null(PaletteCycles.PaletteOf("mpt0018.pal"));
        Assert.Null(PaletteCycles.PaletteOf("stc0006.tbl"));
    }

    [Fact]
    public void Lines_are_first_last_and_ticks_as_seo_dat_writes_them()
    {
        // mpt0027.tbl and mpt0039.tbl as they are in 5.99 seo.dat — CRLF, a blank line at the end.
        List<PaletteCycles.Cycle> runs = PaletteCycles.Read("48 54 2\r\n56 62 2\r\n");

        Assert.Equal([new(48, 54, 2), new(56, 62, 2)], runs);
        Assert.Equal([new PaletteCycles.Cycle(210, 213, 2)], PaletteCycles.Read("210 213 2\r\n\r\n"));
    }

    [Fact]
    public void Runs_that_never_turn_are_dropped_and_ends_are_held_inside_the_palette()
    {
        // 0x5df540~0x5df55d: 처음 at least 1, 끝 at most 255, nothing done when they meet; 빠르기 0 never fires.
        List<PaletteCycles.Cycle> runs = PaletteCycles.Read("0 5 2\n250 300 1\n7 7 2\n9 12 0\n");

        Assert.Equal([new(1, 5, 2), new(250, 255, 1)], runs);
    }

    [Fact]
    public void Only_entries_inside_a_run_of_that_palette_turn()
    {
        PaletteCycles cycles = PaletteCycles.FromArchive([("mpt0018.tbl", "206 219 2"u8.ToArray()), ("mptpal.tbl", "2 188 18"u8.ToArray())]);

        Assert.Equal([18], cycles.Palettes);
        Assert.Equal(new PaletteCycles.Cycle(206, 219, 2), cycles.RunOf(18, 206));
        Assert.Equal(new PaletteCycles.Cycle(206, 219, 2), cycles.RunOf(18, 219));
        Assert.Null(cycles.RunOf(18, 205));
        Assert.Null(cycles.RunOf(18, 220));
        Assert.Null(cycles.RunOf(0, 210));
    }

    [Fact]
    public void The_shown_colour_matches_turning_the_palette_step_by_step_as_the_original_does()
    {
        PaletteCycles.Cycle run = new(48, 54, 2);
        int[] palette = Enumerable.Range(0, 256).ToArray();

        for (int turn = 0; turn <= 20; turn++)
        {
            for (int index = run.First; index <= run.Last; index++)
            {
                Assert.Equal(palette[index], PaletteCycles.Shown(run, index, turn));
            }

            // Legend.exe 0x5df563~0x5df5e8: keep the last, move every other entry up one, put the kept one first.
            int last = palette[run.Last];
            for (int index = run.Last; index > run.First; index--)
            {
                palette[index] = palette[index - 1];
            }

            palette[run.First] = last;
        }
    }
}
