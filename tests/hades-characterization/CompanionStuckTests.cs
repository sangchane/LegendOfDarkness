using System.Net;
using Lod.CompanionBot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// "봇이 지형에 걸리면 잘 못 쫓아온다"(사용자, 2026-09-26). 노비스마을 건물(20~31, 22~25)을 사이에 두고 주인이 반대편으로 옮겨 가면,
/// 봇은 벽을 돌아 오거나 — 벽 파일이 없는 맵처럼 벽을 모르고 막히면 — 서버가 몇 초 안에 주인 옆으로 옮겨 준다.
/// 봇에게는 벽 파일을 주지 않는다(클라우드에 없는 맵과 같은 조건).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionStuckTests : IDisposable
{
    private const string OwnerName = "stuckowner";
    private const int NoviceVillage = 20373;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_bot_that_cannot_get_round_a_building_is_brought_beside_its_owner()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceVillage, 26, 21));
        CompanionCallTests.Configure(server);
        Waiting.MakeGameMaster(server, OwnerName);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);

        WorldClient owner = await Enter(server, OwnerName);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);
        List<string> did = [];
        string noWalls = Path.Combine(server.RunRoot, "no-walls");
        _ = new CompanionRunner(bot, new MapWalls(noWalls), new CompanionSettings(), line =>
        {
            lock (did)
            {
                did.Add(line);
            }
        }).RunAsync(_deadline.Token);

        for (int tries = 0; tries < 10 && bot.Master is null; tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.NotNull(bot.Master);

        // 주인이 건물 아래쪽(26,27)으로 — 봇(위쪽)과는 7칸, 서버가 옮겨 주는 12칸보다 가깝다.
        await owner.SayAsync("/tp \"노비스마을\" 26 27", _deadline.Token);
        await Waiting.Until(() => bot.Others.FirstOrDefault(o => o.Serial == owner.Serial)?.Where == new Tile(26, 27),
            "주인이 건물 아래로 옮겨 가지 않았습니다.", _deadline.Token);

        await Waiting.Until(() =>
                owner.Others.FirstOrDefault(o => o.Serial == bot.Serial)?.Where is { } b &&
                Math.Abs(b.X - 26) + Math.Abs(b.Y - 27) <= 3,
            $"봇이 건물을 넘어 오지 못했습니다: 봇 {owner.Others.FirstOrDefault(o => o.Serial == bot.Serial)?.Where} · 한 일 {string.Join(" | ", did)}",
            _deadline.Token, TimeSpan.FromSeconds(12));
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == NoviceVillage && world.Serial != 0, $"{who} 가 서지 못했습니다.", _deadline.Token);
        return world;
    }
}
