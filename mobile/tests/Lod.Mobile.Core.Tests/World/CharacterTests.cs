using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The bytes the server writes when it shows somebody (ServerFormat33). Written out here rather than
/// captured, so the layout is readable: whoever changes the reader has to change these numbers too.
/// </summary>
public sealed class CharacterTests
{
    // 자리·방향·일련번호까지 9바이트, 그 뒤로 입은 것 21바이트, 살인 가능 표시 1바이트, 그리고 이름.
    private static readonly byte[] Standing =
    [
        0x00, 0x0A,             // 가로 10
        0x00, 0x14,             // 세로 20
        0x02,                   // 남쪽
        0x00, 0x00, 0x30, 0x39, // 일련번호 12345
        0x01, 0x1D,             // 머리 — 투구를 썼으면 투구 번호가 온다 (285)
        0x22,                   // 몸 + 바지 (34)
        0x00, 0x3D,             // 갑옷 61
        0x07,                   // 신발 7
        0x00, 0x3D,             // 갑옷 — 서버가 한 번 더 쓴다
        0x03,                   // 방패 3
        0x11,                   // 무기 17
        0x05,                   // 머리 색 5
        0x06,                   // 신발 색 6
        0x00, 0x09,             // 머리 장식 9
        0x01,                   // 등불 1
        0x00, 0x0B,             // 얼굴 장식 11
        0x00,                   // 서버가 비워 두는 자리
        0x01,                   // 앉아 있음
        0x00, 0x65,             // 도포 101
        0x00,                   // 살인 가능 지역 아님
        0x04, (byte)'W', (byte)'r', (byte)'e', (byte)'n'
    ];

    [Fact]
    public void Somebody_shown_arrives_dressed_and_named()
    {
        Character one = WorldClient.ReadCharacter(Standing);

        Assert.Equal(12345u, one.Serial);
        Assert.Equal(new Tile(10, 20), one.Where);
        Assert.Equal(Direction.South, one.Facing);
        Assert.Equal("Wren", one.Name);

        Appearance worn = Assert.IsType<Appearance>(one.Wearing);

        Assert.Equal(285, worn.Head);
        Assert.Equal(34, worn.Body);
        Assert.Equal(61, worn.Armor);
        Assert.Equal(7, worn.Boots);
        Assert.Equal(3, worn.Shield);
        Assert.Equal(17, worn.Weapon);
        Assert.Equal(5, worn.HairColor);
        Assert.Equal(6, worn.BootColor);
        Assert.Equal(9, worn.HeadAccessory1);
        Assert.Equal(1, worn.Lantern);
        Assert.Equal(11, worn.HeadAccessory2);
        Assert.Equal(1, worn.Resting);
        Assert.Equal(101, worn.OverCoat);
    }

    /// <summary>A dead character is written bare and the server stops before the name.</summary>
    [Fact]
    public void A_body_on_the_floor_is_read_without_a_name()
    {
        Character one = WorldClient.ReadCharacter(Standing.AsSpan(0, 31));

        Assert.Equal(12345u, one.Serial);
        Assert.Equal(string.Empty, one.Name);
    }

    /// <summary>
    /// Somebody who has taken a monster's shape carries a creature number where the wardrobe goes. This
    /// server's safe house has no such map, so all we promise is that it does not stop the pump.
    /// </summary>
    [Fact]
    public void A_transformed_character_is_read_without_clothes()
    {
        byte[] shifted =
        [
            .. Standing.AsSpan(0, 9),
            0xFF, 0xFF,             // 변신했다는 표시
            0x00, 0x2A,             // 괴물 42
            0x01, 0x3A,
            0, 0, 0, 0, 0, 0, 0,
            0x04, (byte)'W', (byte)'r', (byte)'e', (byte)'n'
        ];

        Character one = WorldClient.ReadCharacter(shifted);

        Assert.Null(one.Wearing);
        Assert.Equal("Wren", one.Name);
    }

    [Fact]
    public void A_packet_that_stops_short_is_refused()
    {
        Assert.Throws<ProtocolException>(() => WorldClient.ReadCharacter(Standing.AsSpan(0, 20).ToArray()));
    }
}
