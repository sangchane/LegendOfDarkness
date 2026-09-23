using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// The group on the wire (<see cref="Party" />): what goes out to ask, take an ask, leave and talk to the group, and
/// what comes back — who asked (0x63) and who is in it (0x39, laid out as Hades' ServerFormat39 writes it).
/// </summary>
public sealed class PartyTests
{
    [Fact]
    public void Asking_is_kind_two_and_the_name()
    {
        Assert.Equal(new byte[] { 0x02, 0x04, (byte)'w', (byte)'r', (byte)'e', (byte)'n' }, Party.Ask("wren"));
    }

    [Fact]
    public void Accepting_is_kind_three_and_the_askers_name()
    {
        Assert.Equal(new byte[] { 0x03, 0x03, (byte)'n', (byte)'o', (byte)'v' }, Party.Accept("nov"));
    }

    /// <summary>A whisper to "!" — the name first, then the words, each a one-byte-length string.</summary>
    [Fact]
    public void Group_talk_is_a_whisper_to_the_bang()
    {
        Assert.Equal(new byte[] { 0x01, (byte)'!', 0x02, (byte)'h', (byte)'i' }, Party.Chat("hi"));
    }

    [Fact]
    public void Korean_names_go_out_in_cp949()
    {
        byte[] body = Party.Ask("무도");

        Assert.Equal(0x02, body[0]);
        Assert.Equal(4, body[1]);
        Assert.Equal("무도", LegacyKoreanEncoding.DecodeStringA(body.AsSpan(1), out _));
    }

    [Fact]
    public void An_ask_names_who_is_asking()
    {
        Assert.Equal("nov", Party.ReadAsk([0x01, 0x03, (byte)'n', (byte)'o', (byte)'v']));
    }

    /// <summary>Kinds 4 and 5 are the recruiting board, which this screen does not have.</summary>
    [Fact]
    public void Other_kinds_are_not_an_ask()
    {
        Assert.Null(Party.ReadAsk([0x04, 0x03, (byte)'n', (byte)'o', (byte)'v']));
        Assert.Null(Party.ReadAsk([0x01]));
    }

    [Fact]
    public void The_profile_lists_the_group_with_its_leader()
    {
        PartyRoster roster = Party.ReadRoster(Profile("그룹구성원\n* nov\n  무도\n총 2명"));

        Assert.True(roster.Grouped);
        Assert.Equal([new PartyMember("nov", true), new PartyMember("무도", false)], roster.Members);
    }

    [Fact]
    public void Adventuring_alone_is_nobody()
    {
        PartyRoster roster = Party.ReadRoster(Profile("Adventuring Alone"));

        Assert.False(roster.Grouped);
        Assert.Empty(roster.Members);
    }

    /// <summary>The clan is written before the group; a clan name must not shift where the group is read from.</summary>
    [Fact]
    public void A_clan_does_not_move_the_group()
    {
        PartyRoster roster = Party.ReadRoster(Profile("그룹구성원\n* a\n  b\n총 2명", clan: "어둠"));

        Assert.Equal(["a", "b"], roster.Members.Select(member => member.Name));
    }

    [Fact]
    public void A_profile_cut_before_the_group_is_refused()
    {
        Assert.Throws<ProtocolException>(() => Party.ReadRoster([0x01, 0x00, 0x07]));
    }

    /// <summary>ServerFormat39: nation, clan, 0x07 and seven more bytes, the group, then the rest.</summary>
    private static byte[] Profile(string group, string clan = "") =>
    [
        0x01,
        .. LegacyKoreanEncoding.EncodeStringA(clan),
        0x07, 0, 0, 0, 0, 0, 0, 1,
        .. LegacyKoreanEncoding.EncodeStringA(group),
        0x01, 0x00, 0x05
    ];
}
