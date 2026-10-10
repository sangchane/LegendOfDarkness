using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Darkages.Types;
using Lod.Mobile.Core.Net;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 게임 로그인을 카카오로(사용자 2026-10-11 「비밀번호 대신 카카오」, 명세 <c>autopilot/game-kakao-login/SPEC.md</c> SC-3·SC-4).
/// 앱은 비밀번호 칸에 게임 표를, 옛 캐릭터를 처음 이을 때는 「표|옛비밀번호」를 싣는다. 표 확인은 홈페이지 서비스 — 여기서는 가짜 HTTP.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class KakaoLoginTests : IDisposable
{
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    /// <summary>게임 표 꼴(g번호.만료.서명 64자) — 서명이 맞는지는 홈페이지 서비스가 본다.</summary>
    private static string Token(string id) => $"g{id}.9999999999.{new string('a', 64)}";

    private static KakaoLogin.Check Says(string token) =>
        token == Token("1") ? new("1", null)
        : token == Token("2") ? new("2", null)
        : token == Token("9") ? new(null, KakaoLogin.Pending)
        : new(null, KakaoLogin.Expired);

    [Fact]
    public void Login_lets_the_owner_in_keeps_others_out_and_asks_an_old_character_to_link_once()
    {
        Assert.Equal((KakaoLogin.Verdict.Enter, (string?)null, "1"), KakaoLogin.Decide("1", Token("1"), loopback: false, Says));
        Assert.Equal((KakaoLogin.Verdict.Enter, (string?)null, "1"), KakaoLogin.Decide("1", Token("1") + "|아무거나", false, Says));
        Assert.Equal((KakaoLogin.Verdict.Refuse, KakaoLogin.OtherOwner, (string?)null), KakaoLogin.Decide("1", Token("2"), false, Says));
        Assert.Equal((KakaoLogin.Verdict.Refuse, KakaoLogin.NotLinked, (string?)null), KakaoLogin.Decide("", Token("1"), false, Says));
        Assert.Equal((KakaoLogin.Verdict.Link, (string?)null, "1"), KakaoLogin.Decide(null, Token("1") + "|옛비밀번호", false, Says));
        Assert.Equal((KakaoLogin.Verdict.Refuse, KakaoLogin.Pending, (string?)null), KakaoLogin.Decide("", Token("9"), false, Says));
        Assert.Equal((KakaoLogin.Verdict.Refuse, KakaoLogin.Expired, (string?)null), KakaoLogin.Decide("1", Token("5"), false, Says));
        // 평문 비밀번호 — 밖(옛 앱)은 거절, 같은 기계(봇 셋·시험)는 옛 방식 그대로.
        Assert.Equal((KakaoLogin.Verdict.Refuse, KakaoLogin.OldApp, (string?)null), KakaoLogin.Decide("1", "secret", false, Says));
        Assert.Equal((KakaoLogin.Verdict.PasswordAsBefore, (string?)null, (string?)null), KakaoLogin.Decide("", "secret", true, Says));
        // 표 꼴이 아니면 확인 서버에 묻지도 않는다.
        Assert.Equal((KakaoLogin.Verdict.Refuse, KakaoLogin.OldApp, (string?)null),
            KakaoLogin.Decide("", "g1.2.short", false, _ => throw new InvalidOperationException("묻지 말아야 한다")));
        Assert.Equal(("abc", "x|y"), KakaoLogin.Split("abc|x|y"));
    }

    [Fact]
    public void Creating_from_outside_needs_a_game_token()
    {
        Assert.Equal((true, "1", (string?)null), KakaoLogin.ForCreate(Token("1"), loopback: false, Says));
        Assert.Equal((false, (string?)null, KakaoLogin.OldApp), KakaoLogin.ForCreate("secret", false, Says));
        Assert.Equal((true, (string?)null, (string?)null), KakaoLogin.ForCreate("secret", true, Says));
        Assert.Equal((false, (string?)null, KakaoLogin.Pending), KakaoLogin.ForCreate(Token("9"), false, Says));
    }

    [Fact]
    public async Task A_game_token_makes_its_own_character_and_links_an_old_one_with_its_old_password_once()
    {
        using var check = new FakeCheck();
        using var server = IsolatedHadesServer.Prepare();
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["KakaoCheckUrl"] = check.Url;
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        server.Start(TimeSpan.FromMinutes(2));
        CancellationToken token = _deadline.Token;
        string? Owner(string name) =>
            JsonNode.Parse(File.ReadAllText(Path.Combine(server.ContentLocation, "aislings", name + ".json")))!["KakaoId"]?.GetValue<string>();
        Task<WorldSession> Login(IPAddress where, string name, string field) =>
            HadesLoginClient.LoginAsync(where, server.LoginPort, name, field, null, token);
        async Task<string> Refused(IPAddress where, string name, string field) =>
            (await Assert.ThrowsAnyAsync<Exception>(() => Login(where, name, field))).Message;

        // 표로 만들면 그 카카오 계정이 주인 — 비밀번호 칸의 표는 비밀번호로 남지 않는다.
        using (await HadesLoginClient.CreateCharacterAsync(IPAddress.Loopback, server.LoginPort, "kakaonew", Token("1"),
                   hairStyle: 1, gender: 1, hairColor: 1, path: 1, cancellationToken: token)) { }
        Assert.Equal("1", Owner("kakaonew"));
        using (await Login(IPAddress.Loopback, "kakaonew", Token("1"))) { }
        Assert.Contains(KakaoLogin.OtherOwner, await Refused(IPAddress.Loopback, "kakaonew", Token("2")));
        Assert.Contains(KakaoLogin.Pending, await Refused(IPAddress.Loopback, "kakaonew", Token("9")));
        Assert.Contains(KakaoLogin.Expired, await Refused(IPAddress.Loopback, "kakaonew", Token("5")));

        // 옛 캐릭터(같은 기계에서 평문으로 만든 것) — 표만으로는 잇기를 요구, 틀린 옛 비밀번호는 거절, 맞으면 주인을 묶는다.
        LoginFlow.TryCreateAccount(server, "oldone");
        Assert.Contains(KakaoLogin.NotLinked, await Refused(IPAddress.Loopback, "oldone", Token("1")));
        Assert.Contains("비밀번호가 틀렸습니다", await Refused(IPAddress.Loopback, "oldone", Token("1") + "|wrong"));
        Assert.Null(Owner("oldone"));
        using (await Login(IPAddress.Loopback, "oldone", Token("1") + "|" + LoginFlow.SyntheticSecret)) { }
        Assert.Equal("1", Owner("oldone"));
        using (await Login(IPAddress.Loopback, "oldone", Token("1"))) { }
        Assert.Contains(KakaoLogin.OtherOwner, await Refused(IPAddress.Loopback, "oldone", Token("2") + "|" + LoginFlow.SyntheticSecret));

        // 같은 기계의 평문(봇 셋)은 그대로, 밖의 평문(옛 앱)은 거절.
        LoginFlow.TryCreateAccount(server, "botlike");
        using (await Login(IPAddress.Loopback, "botlike", LoginFlow.SyntheticSecret)) { }
        IPAddress? lan = EcoBotServerTests.OtherAddress();
        if (lan != null)
            Assert.Contains(KakaoLogin.OldApp, await Refused(lan, "botlike", LoginFlow.SyntheticSecret));
    }

    /// <summary>홈페이지 서비스의 <c>/api/kakao/game-check</c> 대신 — 표 셋만 안다.</summary>
    private sealed class FakeCheck : IDisposable
    {
        private readonly HttpListener _listener = new();

        public string Url { get; }

        public FakeCheck()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://127.0.0.1:{port}/api/kakao/game-check";
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _ = Task.Run(Serve);
        }

        private async Task Serve()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception) { return; }
                using var reader = new StreamReader(context.Request.InputStream);
                using var body = JsonDocument.Parse(await reader.ReadToEndAsync());
                string token = body.RootElement.GetProperty("token").GetString() ?? "";
                (int status, string json) = token == Token("1") ? (200, "{\"id\":\"1\"}")
                    : token == Token("2") ? (200, "{\"id\":\"2\"}")
                    : token == Token("9") ? (403, "{\"status\":\"pending\"}")
                    : (401, "{}");
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
