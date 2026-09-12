using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// The port's own tenth step: walk in and use the world. Every count can be right while the world is
/// still unusable — a shop that loads is not a shop you can buy from, and a warp that loads is not a
/// door you can walk through. These go in as a player would.
/// </summary>
public sealed class SmokeTests : IDisposable
{
    /// <summary>노비스잡화상점 — a shop NPC and a way out of the room, both in reach of the spawn.</summary>
    private const int ShopRoom = 20378;

    private static readonly Tile Keeper = new(3, 14);
    private static readonly Tile DoorOut = new(10, 19);
    private const int NoviceTown = 20373;   // 노비스마을 — 잡화상점 (10,19)·(9,19) 이 여기로 난다

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_shop_keeper_opens_a_shop_when_tapped()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (ShopRoom, Keeper.X + 1, Keeper.Y));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, "smokeshop");
        WorldClient world = await Enter(server, "smokeshop");

        Creature keeper = await Standing(world, Keeper);
        await world.ClickAsync(keeper.Serial, _deadline.Token);

        Dialogue talk = await Until(() => world.Talking, "눌렀는데 상점이 열리지 않았습니다");

        // shop1.cs answers with its own question, so this proves the script bound and ran — not just
        // that a template loaded.
        Assert.Contains("베이가", talk.Who, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(talk.What),
            $"상점이 아무 말도 하지 않았습니다. 온 것: {talk.Who} / {talk.What}");
    }

    [Fact]
    public async Task Walking_onto_a_ported_warp_moves_you_to_the_other_map()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (ShopRoom, DoorOut.X, DoorOut.Y - 1));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, "smokewarp");
        WorldClient world = await Enter(server, "smokewarp");

        Assert.Equal(ShopRoom, world.State!.Map.Id);

        // The server drops a walk while the client is still settling into the map, and it answers a
        // good step with silence — it tells the people nearby, not the walker. So wait, step, and ask.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);
        await world.WalkAsync(Direction.South, _deadline.Token);
        await world.RefreshAsync(_deadline.Token);

        WorldEntry moved = await Until(
            () => world.State is { } s && s.Map.Id != ShopRoom ? s : null,
            $"워프 칸을 밟았는데 맵이 바뀌지 않았습니다. 지금 {world.State?.Map.Id} {world.State?.Where}");

        // 걸음이 먹혔는지부터 본다 — 자리에 그대로 있으면 워프가 아니라 걸음이 문제다.

        Assert.Equal(NoviceTown, moved.Map.Id);
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string name)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State, "세계에 들어가지 못했습니다");
        return world;
    }

    private async Task<Creature> Standing(WorldClient world, Tile where) =>
        await Until(() => world.Creatures.FirstOrDefault(one => one.Where == where),
            $"{where} 에 아무도 없습니다");

    private async Task<T> Until<T>(Func<T?> wanted, string complaint) where T : class
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (wanted() is { } got)
            {
                return got;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(complaint);
    }
}
