namespace Lod.DatExtract.Tests;

/// <summary>
/// Turning the number the server sends into a file and a frame. Both edges are easy to get wrong: the
/// 0x8000 flag and the fact that tiles count from 1, not 0.
/// </summary>
public sealed class IconCellTests
{
    [Fact]
    public void The_first_tile_is_the_first_frame_of_the_first_file()
    {
        Assert.Equal((1, 0, 1), Program.IconCell(1));
    }

    [Fact]
    public void The_item_flag_is_taken_off_first()
    {
        // 0x8000 + 1 and a bare 1 must name the same picture.
        Assert.Equal(Program.IconCell(1), Program.IconCell(0x8000 + 1));
    }

    [Fact]
    public void A_file_holds_266_frames_and_the_next_tile_starts_the_next_file()
    {
        Assert.Equal((1, 265, 266), Program.IconCell(266));
        Assert.Equal((2, 0, 267), Program.IconCell(267));
        Assert.Equal((2, 265, 532), Program.IconCell(532));
        Assert.Equal((3, 0, 533), Program.IconCell(533));
    }

    [Fact]
    public void A_number_the_server_actually_sent_lands_where_the_assets_say()
    {
        // 32882 is one of the icons already pulled into the client's assets.
        (int file, int frame, int tile) = Program.IconCell(32882);

        Assert.Equal(114, tile);
        Assert.Equal(1, file);
        Assert.Equal(113, frame);
    }
}
