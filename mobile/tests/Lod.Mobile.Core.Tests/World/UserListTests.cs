using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// Who is on (0x36), laid out the way Hades <c>ServerFormat36</c> writes it: a total and a count, then for each person
/// class (with the 0x88 the server always adds), colour, status, title, stage and the name.
/// </summary>
public sealed class UserListTests
{
    private static byte[] Packet(bool guilds = false)
    {
        List<byte> body = [0x00, 0x02, 0x00, 0x02];

        body.AddRange([0x88 | 5, 0x97, 0x00, 0x00, 0x00]);
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("운영자"));
        body.AddRange([0x88 | 3, 0x90, 0x00, 0x01, 0x00]);
        body.AddRange(LegacyKoreanEncoding.EncodeStringA("Nov"));

        if (guilds)
        {
            body.AddRange(LegacyKoreanEncoding.EncodeStringA("어둠"));
            body.AddRange(LegacyKoreanEncoding.EncodeStringA(""));
        }

        return [.. body];
    }

    [Fact]
    public void Reads_class_and_name_in_the_order_sent()
    {
        IReadOnlyList<OnlineUser> users = UserList.Read(Packet());

        Assert.Equal([(5, "운영자"), (3, "Nov")], users.Select(one => (one.Path, one.Name)));
        Assert.All(users, one => Assert.Equal("", one.Guild));
    }

    [Fact]
    public void Reads_the_guild_names_our_server_adds_after_the_list()
    {
        Assert.Equal(["어둠", ""], UserList.Read(Packet(guilds: true)).Select(one => one.Guild));
    }

    [Fact]
    public void A_cut_packet_is_refused()
    {
        Assert.Throws<ProtocolException>(() => UserList.Read(Packet()[..8]));
    }
}
