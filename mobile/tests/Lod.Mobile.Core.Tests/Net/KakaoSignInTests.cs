using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;

namespace Lod.Mobile.Core.Tests.Net;

public sealed class KakaoSignInTests
{
    private static readonly string Token = "g4411.1762900000." + new string('a', 64);

    [Fact]
    public void The_site_is_the_download_page_site() =>
        Assert.Equal("https://lodgame.duckdns.org/api/kakao/app-ticket", KakaoSignIn.TicketUrl);

    [Fact]
    public void A_ticket_opens_the_start_page_on_our_site()
    {
        Assert.Equal(
            ("abc_-1", "https://lodgame.duckdns.org/api/kakao/start?app=abc_-1"),
            KakaoSignIn.ReadTicket(200, "{\"ticket\":\"abc_-1\",\"start\":\"/api/kakao/start?app=abc_-1\"}"));
    }

    [Theory]
    [InlineData(503, "{\"error\":\"카카오 로그인이 아직 준비되지 않았습니다.\"}")]
    [InlineData(200, "{\"ticket\":\"abc\",\"start\":\"https://evil.example/api/kakao/start?app=abc\"}")]
    [InlineData(200, "{\"ticket\":\"\",\"start\":\"/api/kakao/start?app=\"}")]
    [InlineData(200, "<html>")]
    [InlineData(0, "")]
    public void No_ticket_without_a_good_answer(long status, string body) =>
        Assert.Null(KakaoSignIn.ReadTicket(status, body));

    [Fact]
    public void The_token_request_carries_only_the_ticket() =>
        Assert.Equal("{\"ticket\":\"abc\"}", KakaoSignIn.TokenRequest("abc"));

    [Theory]
    [InlineData(202, "{\"waiting\":true}", KakaoWait.Waiting)]
    [InlineData(202, "{\"code\":true}", KakaoWait.Waiting)]   // 옛 서비스의 번호 요구 — 앱은 번호를 보내지 않는다
    [InlineData(0, "", KakaoWait.Waiting)]
    [InlineData(502, "<html>Bad Gateway</html>", KakaoWait.Waiting)]
    [InlineData(404, "{\"error\":\"로그인 표가 없거나 지났습니다.\"}", KakaoWait.StartOver)]
    [InlineData(400, "{\"error\":\"JSON 으로 보내야 합니다.\"}", KakaoWait.StartOver)]
    public void Each_answer_says_whether_to_keep_waiting(long status, string body, KakaoWait state) =>
        Assert.Equal(state, KakaoSignIn.ReadToken(status, body).State);

    [Fact]
    public void Pending_carries_the_nickname() =>
        Assert.Equal(new KakaoReply(KakaoWait.Pending, "길동"), KakaoSignIn.ReadToken(202, "{\"pending\":true,\"name\":\"길동\"}"));

    [Fact]
    public void Granted_carries_the_game_token_and_nickname() =>
        Assert.Equal(new KakaoReply(KakaoWait.Granted, "길동", Token), KakaoSignIn.ReadToken(200, $"{{\"token\":\"{Token}\",\"name\":\"길동\"}}"));

    [Theory]
    [InlineData("{\"token\":\"k4411.1762900000.aaaa\",\"name\":\"길동\"}")]   // 웹 쿠키 꼴은 게임 표가 아니다
    [InlineData("{\"name\":\"길동\"}")]
    public void A_bad_token_starts_over(string body) =>
        Assert.Equal(KakaoWait.StartOver, KakaoSignIn.ReadToken(200, body).State);

    [Fact]
    public void Refused_keeps_the_server_words_or_ours()
    {
        Assert.Equal(new KakaoReply(KakaoWait.Refused, Error: "거부"), KakaoSignIn.ReadToken(403, "{\"error\":\"거부\"}"));
        Assert.Equal("들어올 수 없는 카카오 계정입니다.", KakaoSignIn.ReadToken(403, "").Error);
    }

    [Fact]
    public void Error_text_falls_back()
    {
        Assert.Equal("잠시 뒤", KakaoSignIn.ErrorOf("{\"error\":\"잠시 뒤\"}", "x"));
        Assert.Equal("x", KakaoSignIn.ErrorOf("<html>", "x"));
    }

    [Fact]
    public void The_game_token_shape()
    {
        Assert.True(KakaoSignIn.IsGameToken(Token));
        Assert.False(KakaoSignIn.IsGameToken("g1.2.aaa"));
        Assert.False(KakaoSignIn.IsGameToken("g٤٤.1762900000." + new string('a', 64)));   // 다른 문자의 숫자
        Assert.False(KakaoSignIn.IsGameToken("k4411.1762900000." + new string('a', 64)));  // 웹 쿠키
        Assert.False(KakaoSignIn.IsGameToken(Token.ToUpperInvariant()));
        Assert.False(KakaoSignIn.IsGameToken(Token + "|1234"));
        Assert.False(KakaoSignIn.IsGameToken(null));
    }

    [Fact]
    public void Saved_lines_round_trip()
    {
        KakaoAccount account = new(Token, "길동");

        Assert.Equal([Token, "길동"], account.ToLines());
        Assert.Equal(account, KakaoAccount.Parse(account.ToLines()[0], account.ToLines()[1]));
    }

    [Fact]
    public void A_broken_saved_token_is_no_account()
    {
        Assert.Null(KakaoAccount.Parse("", "길동"));
        Assert.Null(KakaoAccount.Parse("wren", "길동"));
    }

    [Fact]
    public void A_nickname_never_breaks_the_two_lines() =>
        Assert.Equal([Token, "길 동"], new KakaoAccount(Token, "길\n동").ToLines());

    [Fact]
    public void The_verbs_go_after_the_token()
    {
        Assert.Equal(Token + "|look", KakaoSignIn.Look(Token));
        Assert.Equal(Token + "|delete", KakaoSignIn.Delete(Token));
    }

    [Fact]
    public void The_characters_request_carries_the_game_token()
    {
        Assert.Equal("https://lodgame.duckdns.org/api/kakao/characters", KakaoSignIn.CharactersUrl);
        Assert.Equal($"{{\"token\":\"{Token}\"}}", KakaoSignIn.CharactersRequest(Token));
    }

    [Fact]
    public void My_characters_are_read_in_the_servers_order()
    {
        IReadOnlyList<KakaoCharacter>? mine = KakaoSignIn.ReadCharacters(200,
            "{\"characters\":[{\"name\":\"길동\",\"level\":41,\"path\":\"Monk\",\"gender\":\"Female\",\"hairStyle\":7,\"hairColor\":12},"
            + "{\"name\":\"새싹\",\"level\":1,\"path\":\"Peasant\",\"gender\":\"Male\",\"hairStyle\":1,\"hairColor\":0}]}");

        Assert.Equal([new KakaoCharacter("길동", 41, "Monk", "Female", 7, 12), new KakaoCharacter("새싹", 1, "Peasant", "Male", 1, 0)], mine);
        Assert.Equal(("무도가", (byte)5, (byte)2), (mine![0].Job, mine[0].PathNumber, mine[0].GenderNumber));
        Assert.Equal(("평민", (byte)0, (byte)1), (mine[1].Job, mine[1].PathNumber, mine[1].GenderNumber));
    }

    [Fact]
    public void What_a_character_wears_is_read_like_the_servers_0x33()
    {
        IReadOnlyList<KakaoCharacter>? mine = KakaoSignIn.ReadCharacters(200,
            "{\"characters\":[{\"name\":\"길동\",\"look\":{\"head\":285,\"body\":33,\"armor\":3,\"boots\":5,\"shield\":9,"
            + "\"weapon\":37,\"hairColor\":2,\"bootColor\":9,\"headAccessory1\":4,\"lantern\":1,\"headAccessory2\":6,\"overCoat\":7}},"
            + "{\"name\":\"옛줄\"},{\"name\":\"이상\",\"look\":7}]}");

        Assert.Equal(new Appearance(285, 33, 3, 5, 9, 37, 2, 9, 4, 1, 6, 0, 7), mine![0].Look);
        Assert.Null(mine[1].Look);
        Assert.Null(mine[2].Look);
    }

    [Fact]
    public void No_characters_is_an_empty_list_not_a_failure() =>
        Assert.Empty(KakaoSignIn.ReadCharacters(200, "{\"characters\":[]}")!);

    [Fact]
    public void A_broken_line_is_skipped_and_odd_fields_fall_back()
    {
        IReadOnlyList<KakaoCharacter>? mine = KakaoSignIn.ReadCharacters(200,
            "{\"characters\":[{\"level\":3},7,{\"name\":\"길\\n동\",\"level\":\"9\",\"path\":\"Bard\"}]}");

        Assert.Equal([new KakaoCharacter("길 동", 0, "Bard", "", 0, 0)], mine);
        Assert.Equal(("Bard", (byte)0, (byte)1), (mine![0].Job, mine[0].PathNumber, mine[0].GenderNumber));
    }

    [Theory]
    [InlineData(403, "{\"status\":\"pending\"}")]
    [InlineData(401, "{\"error\":\"bad\"}")]
    [InlineData(200, "{\"characters\":{}}")]
    [InlineData(200, "<html>")]
    [InlineData(0, "")]
    public void No_list_without_a_good_answer(long status, string body) =>
        Assert.Null(KakaoSignIn.ReadCharacters(status, body));

    [Theory]
    [InlineData("카카오 로그인이 끝났습니다. 앱에서 다시 로그인해 주십시오.", true)]
    [InlineData("다른 카카오 계정의 캐릭터입니다.", false)]
    [InlineData("아직 카카오에 이어지지 않은 캐릭터입니다. 관리자에게 이어 달라고 해 주십시오.", false)]
    public void Only_the_ended_refusal_signs_out(string refusal, bool signedOut) =>
        Assert.Equal(signedOut, KakaoSignIn.IsSignedOut(refusal));
}
