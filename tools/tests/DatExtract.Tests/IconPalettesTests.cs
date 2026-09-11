using System.Text;
using Lod.DatExtract;

namespace Lod.DatExtract.Tests;

/// <summary>
/// The <c>*pal.tbl</c> lines are read by word count, and three words are genuinely ambiguous — the same
/// shape means a range or a mood depending on the sign of the last word. That branch had only ever been
/// checked by eye, which is what these cover.
/// </summary>
public sealed class IconPalettesTests
{
    private static byte[] Table(params string[] lines) => Encoding.ASCII.GetBytes(string.Join("\n", lines));

    [Fact]
    public void Two_words_name_one_tile()
    {
        byte[] table = Table("7 3");

        Assert.Equal(3, IconPalettes.PaletteFor(table, 7));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 6));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 8));
    }

    [Fact]
    public void Three_words_ending_positive_close_a_range()
    {
        byte[] table = Table("10 20 4");

        Assert.Equal(0, IconPalettes.PaletteFor(table, 9));
        Assert.Equal(4, IconPalettes.PaletteFor(table, 10));
        Assert.Equal(4, IconPalettes.PaletteFor(table, 15));
        Assert.Equal(4, IconPalettes.PaletteFor(table, 20));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 21));
    }

    [Fact]
    public void Three_words_ending_negative_name_a_mood_on_one_tile()
    {
        // 30 is one tile, palette 5, and only in mood -2 — not a range from 30 to 5.
        byte[] table = Table("30 5 -2");

        Assert.Equal(5, IconPalettes.PaletteFor(table, 30, mood: -2));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 30, mood: -1));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 30));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 5, mood: -2));
    }

    [Fact]
    public void Four_words_name_a_range_and_a_mood()
    {
        byte[] table = Table("40 49 6 -3");

        Assert.Equal(6, IconPalettes.PaletteFor(table, 45, mood: -3));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 45, mood: -4));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 50, mood: -3));
    }

    [Fact]
    public void A_line_without_a_mood_answers_whatever_mood_is_asked()
    {
        byte[] table = Table("60 7");

        Assert.Equal(7, IconPalettes.PaletteFor(table, 60));
        Assert.Equal(7, IconPalettes.PaletteFor(table, 60, mood: -9));
    }

    [Fact]
    public void Lines_that_are_not_rows_are_passed_over()
    {
        byte[] table = Table("# a comment", "", "99", "nonsense 1", "  ", "70 8");

        Assert.Equal(8, IconPalettes.PaletteFor(table, 70));
        Assert.Equal(0, IconPalettes.PaletteFor(table, 99));
    }

    [Fact]
    public void The_first_line_that_covers_a_tile_wins()
    {
        byte[] table = Table("80 90 1", "85 2");

        Assert.Equal(1, IconPalettes.PaletteFor(table, 85));
    }

    [Fact]
    public void A_tile_no_line_covers_uses_palette_zero()
    {
        Assert.Equal(0, IconPalettes.PaletteFor(Table("1 2"), 500));
        Assert.Equal(0, IconPalettes.PaletteFor(Table(), 1));
    }
}
