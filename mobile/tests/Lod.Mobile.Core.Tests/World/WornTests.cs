using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The bytes the server writes when a piece of gear goes on (ServerFormat37). Two names sit in the middle
/// of it, one after the other, so everything behind them moves twice — which is what these numbers pin down.
/// </summary>
public sealed class WornTests
{
    private static byte[] Wearing(string name, string called) =>
    [
        0x0D,                    // 열세 번째 자리 — 신발
        0x80, 0x72,              // 그림 32882
        0x03,                    // 서버가 늘 3 으로 적는 자리
        (byte)name.Length, .. name.Select(letter => (byte)letter),
        (byte)called.Length, .. called.Select(letter => (byte)letter),
        0x00, 0x00, 0x00, 0x2A,  // 지금 42
        0x00, 0x00, 0x3A, 0x98   // 새것일 때 15000
    ];

    [Fact]
    public void Something_worn_is_read_whole()
    {
        WornItem gear = WorldClient.ReadWorn(Wearing("Shagreen Boots", "Shagreen Boots"));

        Assert.Equal(13, gear.Slot);
        Assert.Equal(32882, gear.Icon);
        Assert.Equal("Shagreen Boots", gear.Name);
        Assert.Equal("Shagreen Boots", gear.Called);
        Assert.Equal(42, gear.Durability);
        Assert.Equal(15000, gear.MaxDurability);
    }

    /// <summary>The second name is the upgraded one, and it is usually longer than the first.</summary>
    [Fact]
    public void The_second_name_moves_everything_behind_it()
    {
        WornItem gear = WorldClient.ReadWorn(Wearing("Shagreen Boots", "Godly Shagreen Boots"));

        Assert.Equal("Shagreen Boots", gear.Name);
        Assert.Equal("Godly Shagreen Boots", gear.Called);
        Assert.Equal(42, gear.Durability);
        Assert.Equal(15000, gear.MaxDurability);
    }

    [Theory]
    [InlineData(1, "무기")]
    [InlineData(2, "갑옷")]
    [InlineData(13, "신발")]
    [InlineData(99, "99")]
    public void A_place_is_named_where_we_know_the_number(int slot, string expected)
    {
        Assert.Equal(expected, WornPlace.Of(slot));
    }

    [Fact]
    public void A_packet_that_stops_short_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadWorn([0x0D, 0x80, 0x72]));
    }

    /// <summary>A name that runs to the end leaves no room for how worn out the thing is.</summary>
    [Fact]
    public void A_packet_that_stops_after_the_names_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadWorn(
            [0x0D, 0x80, 0x72, 0x03, 0x01, (byte)'a', 0x01, (byte)'b', 0x00]));
    }
}
