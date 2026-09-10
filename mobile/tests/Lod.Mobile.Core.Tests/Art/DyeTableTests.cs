using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.Tests.Art;

public sealed class DyeTableTests
{
    /// <summary>The shape of color0.tbl: a count, then a number and the colours belonging to it.</summary>
    private const string Table = """
        6
        0
        239,211,163
        227,191,143
        1
        255,255,255
        235,235,235
        """;

    [Fact]
    public void The_opening_count_is_not_mistaken_for_a_colour()
    {
        IReadOnlyDictionary<int, IReadOnlyList<Colour>> table = DyeTable.Read(Table);

        Assert.Equal([0, 1], table.Keys.Order());
        Assert.Equal(new Colour(239, 211, 163), table[0][0]);
        Assert.Equal(new Colour(235, 235, 235), table[1][1]);
    }

    /// <summary>color.tbl opens straight into an entry instead, and must read the same way.</summary>
    [Fact]
    public void A_table_without_a_count_reads_the_same()
    {
        IReadOnlyDictionary<int, IReadOnlyList<Colour>> table = DyeTable.Read("14\n1,2,3\n15\n4,5,6\n");

        Assert.Equal([14, 15], table.Keys.Order());
        Assert.Equal(new Colour(4, 5, 6), table[15][0]);
    }

    [Fact]
    public void A_plain_list_of_colours_is_read_in_order()
    {
        Assert.Equal(
            [new Colour(255, 0, 250), new Colour(255, 0, 255)],
            DyeTable.ReadColours("255,0,250\r\n255,0,255\r\n"));
    }
}
