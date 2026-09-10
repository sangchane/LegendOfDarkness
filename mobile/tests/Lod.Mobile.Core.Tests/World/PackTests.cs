using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The bytes the server writes when it puts something in a pack (ServerFormat0F). The name sits in the
/// middle of it, so everything after it moves when the name does — which is what these numbers pin down.
/// </summary>
public sealed class PackTests
{
    private static byte[] Carrying(string name) =>
    [
        0x03,                    // 세 번째 칸
        0x80, 0xBD,              // 그림 32957
        0x07,                    // 색 7
        (byte)name.Length, .. name.Select(letter => (byte)letter),
        0x00, 0x00, 0x00, 0x05,  // 다섯 개
        0x01,                    // 겹쳐지는 물건
        0x00, 0x00, 0x00, 0x64,  // 새것일 때 100
        0x00, 0x00, 0x00, 0x2A,  // 지금 42
        0x00, 0x00, 0x00, 0x00   // 서버가 비워 두는 자리
    ];

    [Fact]
    public void Something_put_in_the_pack_is_read_whole()
    {
        InventoryItem carried = WorldClient.ReadPackItem(Carrying("Shagreen Boots"));

        Assert.Equal(3, carried.Slot);
        Assert.Equal(32957, carried.Icon);
        Assert.Equal(7, carried.Colour);
        Assert.Equal("Shagreen Boots", carried.Name);
        Assert.Equal(5, carried.Stacks);
        Assert.Equal(42, carried.Durability);
        Assert.Equal(100, carried.MaxDurability);
    }

    /// <summary>A longer name pushes everything after it along, and the reader has to follow.</summary>
    [Fact]
    public void A_longer_name_does_not_shift_the_numbers_after_it()
    {
        InventoryItem carried = WorldClient.ReadPackItem(Carrying("Luathas Coral Earrings"));

        Assert.Equal("Luathas Coral Earrings", carried.Name);
        Assert.Equal(5, carried.Stacks);
        Assert.Equal(42, carried.Durability);
    }

    [Fact]
    public void A_packet_that_stops_short_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadPackItem(new byte[] { 1, 2, 3 }));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadPackItem(Carrying("Boots").AsSpan(0, 12).ToArray()));
    }
}
