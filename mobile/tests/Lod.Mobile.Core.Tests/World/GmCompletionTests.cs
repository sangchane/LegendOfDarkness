using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>운영자 명령 고를 거리 — 명령, 그 뒤의 이름, 서버가 알아듣는 줄(사용자 2026-10-04).</summary>
public sealed class GmCompletionTests
{
    private static readonly GmCompletion Names = new(
        "# 머리줄\nitem 견습자의글러브\nitem 글러브1\nitem Dark Belt\nspell 쿠로토\nskill 기본공격\nmap 죽음의마을입구5 11 17\n");

    [Fact]
    public void A_slash_alone_offers_the_commands()
    {
        Assert.Contains(Names.Suggest("/"), s => s.Text == "/give ");
        Assert.Equal(["/give "], Names.Suggest("/gi").Select(s => s.Text));
    }

    [Fact]
    public void Names_holding_the_typed_letters_come_back_ready_to_send_with_those_starting_with_them_first()
    {
        Assert.Equal(["/give \"글러브1\"", "/give \"견습자의글러브\""], Names.Suggest("/give 글러브").Select(s => s.Text));
        Assert.Equal("/give \"Dark Belt\"", Assert.Single(Names.Suggest("/give dark")).Text);
    }

    [Fact]
    public void A_map_brings_the_tile_to_land_on()
    {
        Assert.Equal("/tp \"죽음의마을입구5\" 11 17", Assert.Single(Names.Suggest("/tp 죽음")).Text);
    }

    [Fact]
    public void Ordinary_words_get_nothing()
    {
        Assert.Empty(Names.Suggest("안녕"));
        Assert.Empty(Names.Suggest("/pt 누구"));
    }
}
