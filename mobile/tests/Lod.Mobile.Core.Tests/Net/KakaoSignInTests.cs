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
    public void The_token_request_carries_the_code_only_once_typed()
    {
        Assert.Equal("{\"ticket\":\"abc\"}", KakaoSignIn.TokenRequest("abc"));
        Assert.Equal("{\"ticket\":\"abc\"}", KakaoSignIn.TokenRequest("abc", "12345"));
        Assert.Equal("{\"ticket\":\"abc\",\"code\":\"012345\"}", KakaoSignIn.TokenRequest("abc", "012345"));
    }

    [Theory]
    [InlineData("012345", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12a456", false)]
    [InlineData("١٢٣٤٥٦", false)]   // 다른 문자의 숫자
    [InlineData(null, false)]
    public void A_code_is_six_digits(string? code, bool expected) =>
        Assert.Equal(expected, KakaoSignIn.IsCode(code));

    [Fact]
    public void The_browser_finishing_asks_for_the_code() =>
        Assert.Equal(KakaoWait.NeedsCode, KakaoSignIn.ReadToken(202, "{\"code\":true}").State);

    [Fact]
    public void A_wrong_code_says_how_many_chances_are_left()
    {
        Assert.Equal(
            new KakaoReply(KakaoWait.WrongCode, Error: "번호가 맞지 않습니다. 남은 기회 3번.", Left: 3),
            KakaoSignIn.ReadToken(403, "{\"error\":\"번호가 맞지 않습니다. 남은 기회 3번.\",\"left\":3}"));
        Assert.Equal(0, KakaoSignIn.ReadToken(403, "{\"left\":0}").Left);
    }

    [Theory]
    [InlineData(202, "{\"waiting\":true}", KakaoWait.Waiting)]
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
    public void Linking_puts_the_old_password_after_the_token() =>
        Assert.Equal(Token + "|옛비번|1", KakaoSignIn.Link(Token, "옛비번|1"));

    [Theory]
    [InlineData("카카오 로그인이 끝났습니다. 앱에서 다시 로그인해 주십시오.", true)]
    [InlineData("다른 카카오 계정의 캐릭터입니다.", false)]
    [InlineData("아직 카카오에 이어지지 않은 캐릭터입니다. 「옛 캐릭터 잇기」로 옛 비밀번호를 한 번 넣어 주십시오.", false)]
    public void Only_the_ended_refusal_signs_out(string refusal, bool signedOut) =>
        Assert.Equal(signedOut, KakaoSignIn.IsSignedOut(refusal));
}
