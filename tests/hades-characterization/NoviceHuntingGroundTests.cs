using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 노비스 사냥터 — 노비스마을 밖 평원 A·B 와 그 아래 지하던전 3x3. 세 팩(5.99 · 혼든 · Novaonline)이 모두 이 모양이고,
/// 5.99 `mob/Novice/Novice_Spawn.txt` 가 평원마다 녹색벌·팜팻·브라운맨티스·노비스풀뱀 51마리, 지하던전 A1 에
/// 멜로더·카디·브라운맨티스·노비스풀뱀을 둔다. 괴물 템플릿이 들어와 있다는 것과 **실제로 서서 보이는 것**은 다른
/// 일이라(우드랜드1-1 은 템플릿이 있어도 한동안 비어 있었다 — <see cref="WoodlandHuntTests" />) 서서 센다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class NoviceHuntingGroundTests : IDisposable
{
    /// <summary>서로 다른 괴물이 이만큼 시야에 들어오면 사냥터가 채워진 것으로 본다(우드랜드 시험과 같은 기준).</summary>
    private const int Several = 2;

    /// <summary>0.5초 간격으로 이만큼 — 1분.</summary>
    private const int StandingTicks = 120;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));

    public void Dispose() => _deadline.Dispose();

    [Theory]
    [InlineData(20393, 25, 25, "노비스평원A")]
    [InlineData(20380, 20, 20, "노비스지하던전A1")]
    public async Task Standing_in_the_novice_ground_puts_monsters_in_sight(int map, int x, int y, string name)
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (map, x, y));
        server.Start(TimeSpan.FromMinutes(2));

        string who = $"novice{map % 100}";
        LoginFlow.TryCreateAccount(server, who);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        HashSet<uint> met = [];

        for (int tick = 0; tick < StandingTicks && met.Count < Several; tick++)
        {
            foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
            {
                met.Add(mob.Serial);
            }

            if (world.State is not null)
            {
                await world.RefreshAsync(_deadline.Token);
            }

            await Task.Delay(500, _deadline.Token);
        }

        Assert.True(world.State?.Map.Id == map, $"{name}({map}) 에 들어가지 못했습니다. 마지막: {world.State}");
        Assert.True(met.Count >= Several,
            $"{name} ({x},{y}) 에 {StandingTicks / 2}초 서 있는 동안 시야에 들어온 괴물이 {met.Count}마리입니다 ({Several}마리를 기대).");
    }
}
