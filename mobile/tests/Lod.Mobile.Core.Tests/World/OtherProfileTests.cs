using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Somebody else's equipment window (0x34), laid out the way Hades <c>ServerFormat34</c> writes it: serial, seventeen
/// pictures with a colour each, then status, name, nation, "Lev N", group status, clan title, class and clan.
/// </summary>
public sealed class OtherProfileTests
{
    private static byte[] Packet(bool open)
    {
        List<byte> body = [0x00, 0x00, 0x30, 0x39];

        // 무기(첫째)와 신발(열넷째 — 장신구 뒤)만 걸쳤다.
        for (int i = 0; i < 17; i++)
        {
            ushort icon = i switch { 0 => 0x8123, 13 => 0x8200, _ => 0 };
            body.AddRange([(byte)(icon >> 8), (byte)icon, 0x00]);
        }

        body.Add(0x00);
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("가나다"));
        body.Add(0x01);
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("Lev 41"));
        body.Add(open ? (byte)1 : (byte)0);
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("단원"));
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("Monk"));
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("어둠"));
        body.Add(0x00);

        return [.. body];
    }

    [Fact]
    public void Reads_who_they_are_and_what_they_wear()
    {
        OtherProfile seen = OtherProfile.Read(Packet(open: true));

        Assert.Equal(12345u, seen.Serial);
        Assert.Equal("가나다", seen.Name);
        Assert.Equal("Monk", seen.Path);
        Assert.Equal("어둠", seen.Clan);
        Assert.Equal("단원", seen.ClanTitle);
        Assert.True(seen.GroupOpen);
        Assert.Equal([(1, 0x8123), (13, 0x8200)], seen.Worn.Select(one => (one.Slot, one.Icon)));
    }

    [Fact]
    public void A_closed_group_reads_closed()
    {
        Assert.False(OtherProfile.Read(Packet(open: false)).GroupOpen);
    }

    [Fact]
    public void A_cut_packet_is_refused()
    {
        Assert.Throws<ProtocolException>(() => OtherProfile.Read(Packet(open: true)[..20]));
    }
}
