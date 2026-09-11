namespace Lod.DatExtract.Tests;

/// <summary>
/// The blocking rule, which the server also applies (Hades.Server.Base Types/Area.cs). Two things here are
/// easy to get wrong and silent when wrong: wall numbers count from 1, and a number of 0 means "no wall on
/// that side" rather than "tile 0".
/// </summary>
public sealed class WallsTests
{
    // Tile 1 is a wall, tile 2 is open, tile 3 is a wall that is also transparent.
    private static readonly byte[] Sotp = [0x0F, 0x00, 0x8F];

    [Fact]
    public void A_cell_with_no_wall_numbers_is_open()
    {
        Assert.False(Walls.Blocks(Sotp, 0, 0));
    }

    [Fact]
    public void One_side_alone_decides()
    {
        Assert.True(Walls.Blocks(Sotp, 1, 0));
        Assert.True(Walls.Blocks(Sotp, 0, 1));
        Assert.False(Walls.Blocks(Sotp, 2, 0));
        Assert.False(Walls.Blocks(Sotp, 0, 2));
    }

    [Fact]
    public void With_both_sides_either_one_blocking_is_enough()
    {
        Assert.True(Walls.Blocks(Sotp, 1, 2));
        Assert.True(Walls.Blocks(Sotp, 2, 1));
        Assert.False(Walls.Blocks(Sotp, 2, 2));
    }

    [Fact]
    public void Wall_numbers_count_from_one()
    {
        // Number 1 reads flag 0 — reading flag 1 instead would call this open.
        Assert.True(Walls.Blocks(Sotp, 1, 0));
        Assert.False(Walls.Blocks(Sotp, 2, 0));
    }

    [Fact]
    public void A_wall_that_is_also_transparent_is_not_the_plain_wall_flag()
    {
        // 0x8F is wall|transparent. The server compares against 0x0F exactly, so this does not block.
        Assert.False(Walls.Blocks(Sotp, 3, 0));
    }

    [Fact]
    public void A_number_past_the_table_answers_open_instead_of_throwing()
    {
        Assert.False(Walls.Blocks(Sotp, 99, 0));
        Assert.False(Walls.Blocks(Sotp, 0, 99));
    }

    [Fact]
    public void A_map_cell_is_three_little_endian_numbers()
    {
        // floor 0x0201, left 0x0403, right 0x0605 — then one short cell that must be ignored.
        byte[] map = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0xFF, 0xFF];

        var cells = Walls.Read(map);

        Assert.Single(cells);
        Assert.Equal(0x0201, cells[0].Floor);
        Assert.Equal(0x0403, cells[0].Left);
        Assert.Equal(0x0605, cells[0].Right);
    }
}
