using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// 서버가 월드맵 창에 쓰는 바이트(ServerFormat2E). 점은 Y 가 먼저 나오고, 그 뒤에 이름·갈 맵·설 칸이 온다.
/// </summary>
public sealed class WorldMapTests
{
    [Fact]
    public void A_field_names_its_picture_and_every_place_on_it()
    {
        WorldMapInfo field = WorldClient.ReadWorldMap(
        [
            0x08, 0x66, 0x69, 0x65, 0x6C, 0x64, 0x30, 0x30, 0x31, // "field001"
            0x01,                                                 // 노드 하나
            0x01,                                                 // 마당 1
            0x00, 0x70,                                           // 점 Y 112
            0x01, 0x59,                                           // 점 X 345
            0x06, 0xBC, 0xF6, 0xBF, 0xC0, 0xB9, 0xCC,             // "수오미"
            0x00, 0x00, 0x4F, 0x83,                               // 맵 20355
            0x00, 0x28,                                           // X 40
            0x00, 0x0B,                                           // Y 11
            0x11, 0x22, 0x33, 0x44, 0x55, 0x66                    // 서버가 채우는 아무 값
        ]);

        Assert.Equal("field001", field.Field);
        Assert.Equal(1, field.FieldNumber);
        Assert.Equal(new WorldMapNode("수오미", 20355, 40, 11, 345, 112), Assert.Single(field.Nodes));
    }

    [Fact]
    public void Two_places_are_read_one_after_the_other()
    {
        WorldMapInfo field = WorldClient.ReadWorldMap(
        [
            0x08, 0x66, 0x69, 0x65, 0x6C, 0x64, 0x30, 0x30, 0x31,
            0x02,
            0x01,
            0x00, 0x70, 0x01, 0x59,
            0x06, 0xBC, 0xF6, 0xBF, 0xC0, 0xB9, 0xCC,             // "수오미"
            0x00, 0x00, 0x4F, 0x83, 0x00, 0x28, 0x00, 0x0B,
            0x01, 0x0D, 0x01, 0x44,
            0x04, 0xBE, 0xC6, 0xBA, 0xA7,                         // "아벨"
            0x00, 0x00, 0x4E, 0x36, 0x00, 0x3A, 0x00, 0x16,       // 맵 20022... 아래 주석
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ]);

        Assert.Equal(2, field.Nodes.Count);
        Assert.Equal("수오미", field.Nodes[0].Name);
        Assert.Equal("아벨", field.Nodes[1].Name);
        Assert.Equal(58, field.Nodes[1].X);
        Assert.Equal(22, field.Nodes[1].Y);
    }

    [Fact]
    public void Choosing_a_place_sends_its_map_number_in_four_bytes()
    {
        Assert.Equal(new byte[] { 0x00, 0x00, 0x4F, 0x83 }, WorldClient.FieldChoice(20355));
    }

    [Fact]
    public void Closing_the_field_sends_a_map_number_of_zero()
    {
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, WorldClient.FieldChoice(0));
    }

    /// <summary>
    /// 몸통이 둘째 노드 중간(이름 시작 전)에서 끊겨도, 이미 다 읽은 첫째 노드는 살려서 돌려준다 —
    /// 하나라도 있으면 사람이 창을 보고 빠져나갈 수 있다. 개수(0x02)와 몸통이 어긋난 경우를 흉내낸다.
    /// </summary>
    [Fact]
    public void A_cut_second_node_still_returns_the_first()
    {
        WorldMapInfo field = WorldClient.ReadWorldMap(
        [
            0x08, 0x66, 0x69, 0x65, 0x6C, 0x64, 0x30, 0x30, 0x31, // "field001"
            0x02,                                                 // 노드 둘이라고 예고
            0x01,
            0x00, 0x70, 0x01, 0x59,
            0x06, 0xBC, 0xF6, 0xBF, 0xC0, 0xB9, 0xCC,             // "수오미"
            0x00, 0x00, 0x4F, 0x83, 0x00, 0x28, 0x00, 0x0B,
            0x01, 0x0D, 0x01, 0x44                                // 둘째 노드 점만 있고 이름부터 끊겼다
        ]);

        Assert.Equal("field001", field.Field);
        Assert.Equal(new WorldMapNode("수오미", 20355, 40, 11, 345, 112), Assert.Single(field.Nodes));
    }
}
