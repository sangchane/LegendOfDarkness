using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 포테의숲 — 5.99 `warp/Suomi_Warp.txt` 의 워프로 들어간다(5.99 워프 목록이 그 파일을 안 실어 5.99 에서도 막혀 있던 곳,
/// `docs/pote-forest.md`). 수오미마을 동쪽 끝(99,24~27)을 밟으면 1존 33,47 이고, 1존에는 팜팻 다섯 색과 사슴이 있다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class PoteForestTests : IDisposable
{
    private const int SuomiTown = 20355;
    private const int ForestOne = 20263;

    private const string Name = "potewalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Walking_off_the_east_edge_of_suomi_enters_the_first_zone_where_monsters_are()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (SuomiTown, 97, 25));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown, "수오미마을에 들어가지 못했습니다.", _deadline.Token);

        // 97 → 98 → 99(입구). 서버가 걸음 간격을 재므로 한 걸음씩 띄운다.
        for (int step = 0; step < 4 && world.State?.Map.Id == SuomiTown; step++)
        {
            await world.WalkAsync(Direction.East, _deadline.Token);
            await Task.Delay(600, _deadline.Token);
        }

        await Waiting.Until(() => world.State is { } state && state.Map.Id == ForestOne && state.Where == new Tile(33, 47),
            $"수오미마을 동쪽 입구를 밟았는데 포테의숲1존 33,47 로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        HashSet<uint> met = [];

        for (int tick = 0; tick < 120 && met.Count < 2; tick++)
        {
            foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
            {
                met.Add(mob.Serial);
            }

            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        Assert.True(met.Count >= 2, $"포테의숲1존 입구에 1분 서 있는 동안 괴물이 {met.Count}마리만 보였습니다.");
    }
}
