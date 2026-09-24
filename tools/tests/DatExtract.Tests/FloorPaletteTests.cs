namespace Lod.DatExtract.Tests;

/// <summary>
/// Which palette a floor tile takes. Getting it wrong still draws the tile — as speckled noise, white water or
/// red-and-blue flower beds — so the rule is pinned here.
/// </summary>
public sealed class FloorPaletteTests
{
    [Fact]
    public void The_client_reads_the_mpt_family_even_when_mps_files_are_beside_it()
    {
        // 5.99 Legend.exe names only mptpal.tbl · mpt%04d.pal · mpt%04d.tbl (file 0x313180~0x313306);
        // "mps" appears nowhere in it, though seo.dat also carries mps0000~0022.pal and mpspal.tbl.
        Assert.Equal("mpt", Program.FloorPaletteFamily);
    }

    [Fact]
    public void A_tile_is_asked_for_by_its_index_plus_two()
    {
        // da-lib Graphics.RenderMap: GetPaletteForId(index + 2), index = map floor number - 1.
        // The tables start at 2 for the same reason ("2 188 1" is the first line of mptpal.tbl's last block).
        MapObjects.PaletteChoice choice = MapObjects.PaletteChoice.Read("2 188 1\n189 375 2\n");

        Assert.Equal(1, Program.FloorPalette(choice, tileIndex: 0));
        Assert.Equal(1, Program.FloorPalette(choice, tileIndex: 186));
        Assert.Equal(2, Program.FloorPalette(choice, tileIndex: 187));
    }

    [Fact]
    public void A_single_line_names_one_tile_and_beats_a_later_range()
    {
        MapObjects.PaletteChoice choice = MapObjects.PaletteChoice.Read("16439 34\n16400 16500 7\n");

        Assert.Equal(34, Program.FloorPalette(choice, tileIndex: 16437));
        Assert.Equal(7, Program.FloorPalette(choice, tileIndex: 16436));
    }
}
