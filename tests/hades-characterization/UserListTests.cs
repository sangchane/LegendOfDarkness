using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 접속자 창(2026-10-04) — 0x18 을 보내면 서버가 접속자 목록(0x36)을 보낸다. 레벨 높은 순, 같으면 최대 체력 + 최대 마력×2 큰 순
/// (사용자). 알맹이는 직업과 이름만 읽는다(<see cref="UserList" />).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class UserListTests : IDisposable
{
    private static readonly (int Map, int X, int Y) Town = (20373, 37, 29);

    // 레벨 50 둘은 체력 + 마력×2 로 갈린다(100 + 400 = 500 > 400 + 0) — 체력만 큰 쪽이 뒤.
    private static readonly (string Name, int Level, int Health, int Mana)[] People =
        [("ulow", 10, 9000, 9000), ("utie", 50, 400, 0), ("uhigh", 50, 100, 200)];

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Lists_who_is_on_by_level_then_health_and_twice_mana()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        server.Start(TimeSpan.FromMinutes(2));

        List<WorldClient> worlds = [];

        foreach ((string name, int level, int health, int mana) in People)
        {
            worlds.Add(await Enter(server, name, level, health, mana));
        }

        // 서버는 새로고침(0x38) 뒤 0.3초 안의 0x18 을 버린다 — 앱처럼 답이 올 때까지 1초마다 다시 묻는다.
        IReadOnlyList<OnlineUser>? users = null;

        for (int tries = 0; users is null && tries < 10; tries++)
        {
            await worlds[0].AskUsersAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
            users = worlds[0].TakeUsers();
        }

        Assert.NotNull(users);
        Assert.Equal(["uhigh", "utie", "ulow"], users!.Select(one => one.Name.ToLowerInvariant()));
        Assert.Equal(["어둠", "", ""], users!.Select(one => one.Guild));
    }

    /// <summary>
    /// 확인 사진 — 격리 서버에 알맹이 셋과 실제 앱 하나. 앱이 들어가 [접속자] 창을 연다(<c>--users</c>).
    /// <c>LOD_USERS_SHOT</c> 에 png 경로를 줄 때만 돈다(<c>LOD_USERS_ORIENT</c> portrait|landscape).
    /// </summary>
    [Fact]
    public async Task Photograph_the_user_list()
    {
        if (Environment.GetEnvironmentVariable("LOD_USERS_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: Town);
        server.Start(TimeSpan.FromMinutes(2));

        foreach ((string name, int level, int health, int mana) in People)
        {
            await Enter(server, name, level, health, mana);
        }

        LoginFlow.TryCreateAccount(server, "usee");
        File.Delete(shot);
        string orient = Environment.GetEnvironmentVariable("LOD_USERS_ORIENT") ?? "portrait";
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"usee:{LoginFlow.SyntheticSecret}",
                     "--orient", orient, "--size", orient == "portrait" ? "360x780" : "852x393",
                     "--users", "--shot", shot, "--shot-after", "12"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
        string output = await said;
        Assert.True(output.Contains("GREYBOX_USERS ", StringComparison.Ordinal), output);
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string name, int level, int health, int mana)
    {
        LoginFlow.TryCreateAccount(server, name);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = level;
        character["_MaximumHp"] = health;
        character["_MaximumMp"] = mana;
        character["Clan"] = name == "uhigh" ? "어둠" : "";
        File.WriteAllText(saved, character.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null, $"{name} 가 들어가지 못했습니다.", _deadline.Token);

        return world;
    }
}
