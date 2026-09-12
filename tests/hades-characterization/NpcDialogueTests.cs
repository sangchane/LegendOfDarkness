using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// The count of loaded NPC templates says nothing about whether the world has NPCs in it. An NPC that
/// loads, stands in the right place and answers when tapped is the only thing worth calling done — the
/// port's own plan says so — and that needs a client that walks in and taps one.
/// </summary>
public sealed class NpcDialogueTests : IDisposable
{
    /// <summary>A ported NPC with a line of its own, and where it stands.</summary>
    private const int MilethId = 20287;

    private const string Garen = "가렌@밀레스마을#52,43";
    private const string GarenSays = "가렌: 전사 사범담당 가렌입니다. 데마시아!";
    private const string Name = "npctalk";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Tapping_a_ported_npc_opens_what_the_pack_said_it_says()
    {
        // Standing beside Garen — the tile south of him holds another Garen, the pack puts four in a row.
        // The tap carries a serial, not a direction, but being next to
        // him is what a player would do and it keeps him inside the first creature list.
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MilethId, 53, 43));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry entry = await Settled(world, seen => seen is not null);
        Assert.Equal(MilethId, entry.Map.Id);

        Creature him = await Standing(world, new Tile(52, 43));

        await world.ClickAsync(him.Serial, _deadline.Token);

        Dialogue talk = await Answered(world);

        Assert.Equal(Garen, talk.Who);
        Assert.Equal(GarenSays, talk.What);
        Assert.Equal(him.Serial, talk.Serial);
    }

    private async Task<Creature> Standing(WorldClient world, Tile where)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            Creature? found = world.Creatures.FirstOrDefault(one => one.Where == where);

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(
            $"{where} 에 아무도 없습니다. 본 것: " +
            string.Join(", ", world.Creatures.Select(one => $"{one.Serial}@{one.Where}")));
    }

    private async Task<Dialogue> Answered(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Talking is { } talk)
            {
                return talk;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException("눌렀는데 대화창이 오지 않았습니다.");
    }

    private async Task<WorldEntry> Settled(WorldClient world, Func<WorldEntry?, bool> wanted)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (wanted(world.State))
            {
                return world.State!;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"세계에 들어가지 못했습니다. 마지막 상태: {world.State}");
    }
}
