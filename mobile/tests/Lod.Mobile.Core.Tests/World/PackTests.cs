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

    /// <summary>Our server adds the item's numbers after the original's end (우리 확장); the original's end alone has none.</summary>
    [Fact]
    public void Our_servers_numbers_after_the_end_are_read_and_said()
    {
        byte[] numbers =
        [
            0x01,                                       // 표식
            0xFF, 0xFB, 0x00, 0x02, 0x00, 0x00,         // 방어 -5 · 명중 +2 · 타격 0
            0x00, 0x03, 0x00, 0x00, 0x00, 0x00,         // 힘 +3 · 지능 · 지혜
            0x00, 0x00, 0x00, 0x00, 0x00, 0x0A,         // 체력 · 민첩 · 마법 방어 +10
            0x00, 0x00, 0x01, 0xF4, 0x00, 0x00, 0x00, 0x00, // HP +500 · MP
            0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x28, // 공격력 20~40
            41, 5, 0, 3, 1, 0,                          // 레벨 41 · 무도가 · 단계 · 무게 3 · 불 · 없음
            0x00, 0x00, 0x03, 0xE8                      // 값 1000
        ];

        InventoryItem carried = WorldClient.ReadPackItem([.. Carrying("Boots"), .. numbers]);

        Assert.Equal(42, carried.Durability);
        Assert.Equal(
            ["공격력 20~40", "방어 -5", "명중 +2", "힘 +3", "마법 방어 +10", "HP +500", "공격 속성 불", "요구 레벨 41 · 무도가", "무게 3"],
            ItemActions.Stats(carried));
        Assert.Empty(ItemActions.Stats(WorldClient.ReadPackItem(Carrying("Boots"))));
    }

    [Fact]
    public void A_packet_that_stops_short_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadPackItem(new byte[] { 1, 2, 3 }));
        Assert.Throws<ProtocolException>(() => WorldClient.ReadPackItem(Carrying("Boots").AsSpan(0, 12).ToArray()));
    }
}
