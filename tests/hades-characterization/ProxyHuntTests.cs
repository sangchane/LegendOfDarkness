using System.Net;
using System.Text.Json.Nodes;
using Darkages.Types;
using Lod.HuntProxy;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 대신 사냥(<c>autopilot/proxy-hunt/</c>) — 서버의 열쇠 로그인(SC-003)과 넘김·되찾기(SC-004).
/// 열쇠는 같은 기계에서, 그 이름으로, 한 번, 2분 안에, 캐릭터가 접속해 있지 않을 때만 통한다.
/// </summary>
public sealed class ProxyHuntTests : IDisposable
{
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    private static readonly IPAddress Here = IPAddress.Loopback;
    private static readonly IPAddress Away = IPAddress.Parse("203.0.113.5");
    private const string Orders = """{"hours":2}""";

    public ProxyHuntTests() => ProxyHunt.FolderSource = () => "";

    [Fact]
    public void A_key_opens_once_for_its_name_from_this_machine_within_two_minutes()
    {
        DateTime now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        // ① 루프백 · 이름 일치 · 처음 · 시간 안 · 접속 안 함 → 끝 시각(2시간 뒤).
        string key = ProxyHunt.Issue("keyone", Orders, now);
        Assert.Equal(now.AddHours(2), ProxyHunt.Take("KeyOne", key, Here, online: false, now, 1));

        // ④ 두 번째는 안 된다.
        Assert.Null(ProxyHunt.Take("keyone", key, Here, online: false, now, 1));

        // ② 다른 기계에서는 안 된다 — 그 뒤에도 열쇠는 그대로 산다(남이 버리게 하지 못한다).
        key = ProxyHunt.Issue("keytwo", Orders, now);
        Assert.Null(ProxyHunt.Take("keytwo", key, Away, online: false, now, 1));
        Assert.Null(ProxyHunt.Take("keytwo", key, IPAddress.Parse("::ffff:203.0.113.5"), online: false, now, 1));
        Assert.NotNull(ProxyHunt.Take("keytwo", key, IPAddress.Parse("::ffff:127.0.0.1"), online: false, now, 1));

        // ③ 다른 이름의 열쇠로는 안 된다.
        string other = ProxyHunt.Issue("keythree", Orders, now);
        ProxyHunt.Issue("keyfour", Orders, now);
        Assert.Null(ProxyHunt.Take("keyfour", other, Here, online: false, now, 1));

        // ⑤ 2분이 지나면 안 된다.
        key = ProxyHunt.Issue("keyfive", Orders, now);
        Assert.Null(ProxyHunt.Take("keyfive", key, Here, online: false, now + ProxyHunt.TokenTtl + TimeSpan.FromSeconds(1), 1));

        // ⑥ 이미 접속해 있으면 안 되고(앱을 밀어내지 않는다) 열쇠도 버린다.
        key = ProxyHunt.Issue("keysix", Orders, now);
        Assert.Null(ProxyHunt.Take("keysix", key, Here, online: true, now, 1));
        Assert.Null(ProxyHunt.Take("keysix", key, Here, online: false, now, 1));

        // 비밀번호 같은 아무 글은 열쇠가 아니다.
        ProxyHunt.Issue("keyseven", Orders, now);
        Assert.Null(ProxyHunt.Take("keyseven", "not-a-real-secret", Here, online: false, now, 1));
        Assert.Null(ProxyHunt.Take("keyseven", new string('z', ProxyHunt.TokenBytes * 2), Here, online: false, now, 1));
    }

    [Fact]
    public void Only_the_connection_that_used_the_key_arrives_as_the_proxy_and_never_over_a_returned_app()
    {
        DateTime now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        // 열쇠를 쓴 로그인 접속(7)만 대리 — 같은 이름으로 오는 다른 접속(앱, 8)은 보통 손님.
        string key = ProxyHunt.Issue("arriver", Orders, now);
        Assert.NotNull(ProxyHunt.Take("arriver", key, Here, online: false, now, loginSerial: 7));
        Assert.Null(ProxyHunt.Arrive("arriver", 8, online: false, out bool refused));
        Assert.False(refused);
        Assert.Equal(now.AddHours(2), ProxyHunt.Arrive("Arriver", 7, online: false, out refused));
        Assert.False(refused);
        Assert.Null(ProxyHunt.Arrive("arriver", 7, online: false, out _));

        // 열쇠를 쓴 뒤 앱이 비밀번호로 먼저 들어왔다 — 뒤늦게 온 대리는 돌려보낸다(앱을 밀어내지 않는다).
        key = ProxyHunt.Issue("racer", Orders, now);
        Assert.NotNull(ProxyHunt.Take("racer", key, Here, online: false, now, loginSerial: 9));
        ProxyHunt.Cancel("racer");
        Assert.Null(ProxyHunt.Arrive("racer", 9, online: true, out refused));
        Assert.True(refused);
    }

    [Fact]
    public void Hours_are_clamped_to_what_the_server_allows()
    {
        DateTime now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        string key = ProxyHunt.Issue("longhours", """{"hours":10}""", now);
        Assert.Equal(now.AddHours(ProxyHunt.HoursMax), ProxyHunt.Take("longhours", key, Here, online: false, now, 1));
        Assert.Equal(ProxyHunt.HoursDefault, ProxyHunt.Hours("""{"radius":3}"""));
        Assert.Equal(1, ProxyHunt.Hours("""{"hours":0}"""));
    }

    [Fact]
    public async Task A_dropped_hunter_is_handed_over_and_taken_back_with_a_report()
    {
        const string who = "proxyman";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        string jobs = Path.Combine(server.RunRoot, "proxy-jobs");
        Configure(server, jobs);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);

        // 앱: 들어가서 맡기고, 끊긴다(소켓 닫힘).
        using (WorldSession app = await Login(server, who, LoginFlow.SyntheticSecret))
        {
            WorldClient world = Pump(app);
            await Waiting.Until(() => world.State is not null, "앱이 월드에 서지 못했습니다.", _deadline.Token);
            await world.ArmProxyAsync(new ProxyOrders { Hours = 1, Radius = 5 }, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        string job = Path.Combine(jobs, $"{who}.json");
        await Waiting.Until(() => File.Exists(job), $"끊겼는데 작업 파일이 생기지 않았습니다.{Tail(server)}", _deadline.Token);
        JsonNode handed = JsonNode.Parse(File.ReadAllText(job))!;
        Assert.Equal(5, (int)handed["settings"]!["radius"]!);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(jobs));

        // 대리: 비밀번호 대신 열쇠로 들어간다. 열쇠는 한 번이라 파일이 지워진다.
        WorldSession proxy = await Login(server, who, (string)handed["token"]!);
        WorldClient hunter = Pump(proxy);
        await Waiting.Until(() => hunter.State is not null, "대리가 월드에 서지 못했습니다.", _deadline.Token);
        Assert.False(File.Exists(job), "쓴 열쇠의 작업 파일이 남았습니다.");

        // 앱이 돌아온다 — 대리는 밀려나고, 앱에 결과 한 줄. 밀려난 대리는 다시 넘기지 않는다.
        WorldClient back = Pump(await Login(server, who, LoginFlow.SyntheticSecret));
        await Waiting.Until(() => back.State is not null, "앱이 다시 들어가지 못했습니다.", _deadline.Token);
        await Waiting.Until(() => hunter.Broke is not null || hunter.IsDisposed, "앱이 돌아왔는데 대리가 남았습니다.", _deadline.Token,
            within: TimeSpan.FromSeconds(20));

        List<string> told = [];
        await Waiting.Until(() =>
        {
            while (back.TakeTold(out _, out string text))
                told.Add(text);
            return told.Any(line => line.StartsWith("대신 사냥", StringComparison.Ordinal));
        }, "돌아온 앱에 대신 사냥 결과가 오지 않았습니다.", _deadline.Token, within: TimeSpan.FromSeconds(20));
        Assert.False(File.Exists(job), "밀려난 대리가 다시 넘김을 만들었습니다.");

        // 스스로 로그아웃하면 맡기지 않는다.
        await back.ArmProxyAsync(new ProxyOrders(), _deadline.Token);
        await back.LogOutAsync(_deadline.Token);
        await Task.Delay(TimeSpan.FromSeconds(2), _deadline.Token);
        Assert.False(File.Exists(job), "스스로 나갔는데 대리에게 넘겼습니다.");
    }

    [Fact]
    public async Task The_key_is_refused_from_another_name_and_a_password_login_cancels_it()
    {
        const string who = "proxycancel";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        string jobs = Path.Combine(server.RunRoot, "proxy-jobs");
        Configure(server, jobs);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);
        LoginFlow.TryCreateAccount(server, "proxyother");

        using (WorldSession app = await Login(server, who, LoginFlow.SyntheticSecret))
        {
            WorldClient world = Pump(app);
            await Waiting.Until(() => world.State is not null, "앱이 월드에 서지 못했습니다.", _deadline.Token);
            await world.ArmProxyAsync(new ProxyOrders(), _deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        string job = Path.Combine(jobs, $"{who}.json");
        await Waiting.Until(() => File.Exists(job), $"작업 파일이 생기지 않았습니다.{Tail(server)}", _deadline.Token);
        string token = (string)JsonNode.Parse(File.ReadAllText(job))!["token"]!;

        await Assert.ThrowsAnyAsync<Exception>(() => Login(server, "proxyother", token));

        // 앱이 대리보다 먼저 돌아오면 열쇠는 없던 일 — 대리는 들어오지 못한다.
        using WorldSession back = await Login(server, who, LoginFlow.SyntheticSecret);
        Assert.False(File.Exists(job), "앱이 돌아왔는데 작업 파일이 남았습니다.");
        await Assert.ThrowsAnyAsync<Exception>(() => Login(server, who, token));
    }

    [Fact]
    public async Task The_proxy_program_takes_the_job_hunts_and_hands_back_experience()
    {
        const string who = "proxyhunter";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (AutoHuntTests.WoodlandOneOne, AutoHuntTests.Start.X, AutoHuntTests.Start.Y));
        AutoHuntTests.StandOneWeakTarget(server);
        string jobs = Path.Combine(server.RunRoot, "proxy-jobs");
        Configure(server, jobs);
        server.Start(TimeSpan.FromMinutes(2));

        // 앱: 무도가로 들어가 자동 사냥을 맡기고 끊긴다.
        using (WorldSession app = await HadesLoginClient.CreateCharacterAsync(
                   IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret,
                   hairStyle: 12, gender: 1, hairColor: 40, path: 5, progress: null, _deadline.Token))
        {
            WorldClient world = Pump(app);
            await Waiting.Until(() => world.State is not null && world.Creatures.Any(one => one.Kind == CreatureKind.Hostile),
                "앱이 월드에 서지 못했거나 표적이 없습니다.", _deadline.Token);
            await world.ArmProxyAsync(new ProxyOrders
            {
                Hours = 1, Radius = 8, Map = world.State!.Map.Id, X = world.State.Where.X, Y = world.State.Where.Y,
            }, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        // 대리 프로그램: 작업을 집어 들어가 사냥한다.
        List<string> said = [];
        string assets = HadesWorkspace.MapLayoutFolder;
        ProxyHost host = new(new ProxyConfig { JobFolder = jobs, LoginPort = server.LoginPort, MapFolder = assets },
            new Lod.CompanionBot.MapWalls(assets), MapGuide.Empty, line => { lock (said) said.Add(line); });
        using CancellationTokenSource stopHost = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token);
        Task hosting = host.RunAsync(stopHost.Token);

        string Said() { lock (said) return string.Join(" | ", said); }
        await Waiting.Until(() => host.Running == 1, $"대리가 작업을 집지 않았습니다: {Said()}{Tail(server)}", _deadline.Token);

        // 대리가 사냥하는 동안 기다렸다가(표적 하나, 무도가 평타) 앱이 돌아온다.
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        long Exp() => (long?)JsonNode.Parse(File.ReadAllText(saved))!["ExpTotal"] ?? 0;
        long before = Exp();
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        WorldClient? back = null;
        List<string> told = [];

        while (DateTime.UtcNow < giveUp)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), _deadline.Token);
            back = Pump(await Login(server, who, LoginFlow.SyntheticSecret));
            await Waiting.Until(() => back.State is not null, "앱이 다시 들어가지 못했습니다.", _deadline.Token);
            await Waiting.Until(() =>
            {
                while (back.TakeTold(out _, out string text))
                    told.Add(text);
                return told.Any(line => line.StartsWith("대신 사냥", StringComparison.Ordinal));
            }, $"결과 한 줄이 없습니다: {Said()}", _deadline.Token, within: TimeSpan.FromSeconds(20));

            if (back.Vitals?.Experience > before)
                break;

            // 아직 못 잡았다 — 다시 맡기고 끊어 대리에게 돌려준다.
            await back.ArmProxyAsync(new ProxyOrders { Hours = 1, Radius = 8 }, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
            back.Dispose();
            await Waiting.Until(() => host.Running == 1, $"대리가 다시 집지 않았습니다: {Said()}", _deadline.Token);
        }

        string report = told.LastOrDefault(line => line.StartsWith("대신 사냥", StringComparison.Ordinal)) ?? "";
        Assert.True(back?.Vitals?.Experience > before, $"대리가 경험치를 얻지 못했습니다: {report} · {Said()}");
        await Waiting.Until(() => host.Running == 0, $"앱이 돌아왔는데 대리가 남았습니다: {Said()}", _deadline.Token);
        Assert.Contains("대리 접속", Said());
        stopHost.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hosting);
    }

    private static string Tail(IsolatedHadesServer server) =>
        Environment.NewLine + server.ConsoleOutput[^Math.Min(3000, server.ConsoleOutput.Length)..];

    private static void Configure(IsolatedHadesServer server, string jobs)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["ProxyJobFolder"] = jobs;
        File.WriteAllText(path, config.ToJsonString());
    }

    private Task<WorldSession> Login(IsolatedHadesServer server, string who, string secret) =>
        HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, secret, progress: null, _deadline.Token);

    private WorldClient Pump(WorldSession session)
    {
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        return world;
    }
}
