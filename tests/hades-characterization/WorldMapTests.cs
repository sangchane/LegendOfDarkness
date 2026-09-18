using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 월드맵 — 우드랜드입구 아래 가장자리(9~11,23)를 밟으면 서버가 창을 띄우고(ServerFormat2E),
/// 그 창에서 한 곳을 고르기 전까지는 다른 패킷을 모두 버린다(`NetworkServer.cs:141`).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class WorldMapTests : IDisposable
{
    private const int WoodlandGate = 20028;
    private const int SuomiTown = 20355;

    private const string Name = "mapwalker";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Stepping_onto_the_woodland_edge_opens_the_world_map_with_suomi_on_it()
    {
        // 20028 은 40x24 — (10,23) 이 아래 가장자리이고 월드맵을 여는 칸이다. (10,22) 도 (10,23) 도 길이다.
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, _deadline.Token);

        WorldMapInfo field = world.Field!;

        Assert.Equal("field001", field.Field);
        Assert.Equal(24, field.Nodes.Count);

        WorldMapNode suomi = Assert.Single(field.Nodes, node => node.Name == "수오미");

        Assert.Equal(SuomiTown, suomi.AreaId);
        Assert.Equal(40, suomi.X);
        Assert.Equal(11, suomi.Y);
    }

    [Fact]
    public async Task Choosing_suomi_puts_the_character_down_in_suomi()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandGate, 10, 22));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == WoodlandGate,
            "우드랜드입구에 들어가지 못했습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.South, () => world.Field is not null, _deadline.Token);

        await world.ChooseFieldAsync(SuomiTown, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == SuomiTown,
            $"수오미를 골랐는데 수오미마을로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        // 창은 맵이 바뀌면 닫힌다 — 안 닫히면 그 뒤로 걸음이 서버에 닿지 않는다.
        Assert.Null(world.Field);
    }
}
