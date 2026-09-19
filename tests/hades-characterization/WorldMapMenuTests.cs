using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 월드맵을 숨은 칸이 아니라 **말 한 마디로** 연다. 마을에서만 열리고(사냥터에서 열면 싸우다 갇힌다),
/// 열었다가 그냥 닫을 수도 있다(사용자 결정, 2026-09-19).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class WorldMapMenuTests : IDisposable
{
    private const int NoviceTown = 20373;
    private const int NovicePlain = 20393;
    private const int SuomiTown = 20355;

    private const string Name = "menuwalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Asking_for_the_world_map_in_town_opens_it()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 37, 29));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NoviceTown, "노비스마을에 들어가지 못했습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);

        await Waiting.Until(() => world.Field is not null, "마을에서 월드맵을 달라고 했는데 오지 않았습니다.", _deadline.Token);
        Assert.Contains(world.Field!.Nodes, node => node.Name == "수오미");
    }

    [Fact]
    public async Task Asking_for_it_where_monsters_are_is_refused()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlain, 25, 25));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NovicePlain, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);

        // 괴물이 젠될 때까지 기다린다 — 젠 관리자가 세우기 전에 물으면 마을처럼 보인다.
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "노비스평원A 에 괴물이 나오지 않았습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        Assert.Null(world.Field);

        // 거절당했어도 갇히면 안 된다 — 걸음이 그대로 닿는다.
        // 원작 걸음은 성공해도 자기에게는 아무 말도 오지 않는다(ServerFormat0C 는
        // Scope.NearbyAislingsExludingSelf) — 그래서 걷고 나서 RefreshAsync 로 서버에게
        // 있는 자리를 다시 물어야 world.State.Where 가 실제로 바뀐 값을 받는다.
        Tile before = world.State!.Where;
        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Where != before, _deadline.Token);
        await world.RefreshAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Where != before, "걸었는데 서버가 다른 자리를 말하지 않습니다.", _deadline.Token);
        Assert.NotEqual(before, world.State!.Where);
    }

    [Fact]
    public async Task Closing_the_world_map_gives_movement_back()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 37, 29));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == NoviceTown, "노비스마을에 들어가지 못했습니다.", _deadline.Token);

        await world.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is not null, "월드맵이 오지 않았습니다.", _deadline.Token);

        await world.CloseFieldAsync(_deadline.Token);

        await Waiting.Until(() => world.Field is null, "닫았는데 월드맵이 그대로입니다.", _deadline.Token);

        // 여기가 요점이다 — 닫은 뒤 걸음이 서버에 닿아야 한다.
        // 원작 걸음은 성공해도 자기에게는 아무 말도 오지 않는다(ServerFormat0C 는
        // Scope.NearbyAislingsExludingSelf) — 그래서 걷고 나서 RefreshAsync 로 서버에게
        // 있는 자리를 다시 물어야 world.State.Where 가 실제로 바뀐 값을 받는다.
        Tile before = world.State!.Where;
        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Where != before, _deadline.Token);
        await world.RefreshAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Where != before, "걸었는데 서버가 다른 자리를 말하지 않습니다.", _deadline.Token);
        Assert.NotEqual(before, world.State!.Where);
        Assert.Equal(NoviceTown, world.State!.Map.Id);
    }
}
