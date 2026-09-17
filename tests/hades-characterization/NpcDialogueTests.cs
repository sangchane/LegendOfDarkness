using System.Net;
using System.Text.Json.Nodes;
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

    /// <summary>A ported shop (<c>shop1</c>) in the novice town's diner, and one of its goods the server has a template for.</summary>
    private const int NoviceDinerId = 20374;
    private const string Diner = "카르마@노비스마을식당#3,10";
    private const string SnakeMeat = "뱀고기";
    private const uint SnakeMeatPrice = 350;
    private const string Shopper = "npcshop";

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

    /// <summary>
    /// A shop is three windows deep: its menu, the goods, and word of the sale. The goods have Korean names and the
    /// shop finds what to sell by the name that comes back, so this is also the check that the name survives the
    /// way back (0x3A, the server's code page — not 0x39, which reads ASCII).
    /// </summary>
    [Fact]
    public async Task Buying_from_a_shop_goes_through_its_windows_and_puts_the_thing_in_the_pack()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceDinerId, 3, 11));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Shopper);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Shopper}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["GoldPoints"] = 1000;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Shopper, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen?.Map.Id == NoviceDinerId);
        Creature keeper = await Standing(world, new Tile(3, 10));

        await world.ClickAsync(keeper.Serial, _deadline.Token);
        Dialogue menu = await Window(world, 0, talk => talk.Options.Count > 0);

        Assert.Equal((Diner, DialogueKind.Options), (menu.Who, menu.Kind));
        DialogueOption buy = Assert.Single(menu.Options, option => option.Text == "Buy");

        int opened = world.TalkCount;
        await world.AnswerAsync(keeper.Serial, buy.Step, _deadline.Token);
        Dialogue goods = await Window(world, opened, talk => talk.Kind == DialogueKind.Goods);

        DialogueGoods meat = Assert.Single(goods.Goods, one => one.Name == SnakeMeat);
        Assert.Equal(SnakeMeatPrice, meat.Price);

        await world.AnswerAsync(keeper.Serial, goods.Step, meat.Name, _deadline.Token);
        await Until(() => world.Pack.Any(item => item.Name == SnakeMeat) && world.Vitals?.Gold == 1000 - SnakeMeatPrice,
            $"{SnakeMeat}을 사지 못했습니다. 소지품: {string.Join(", ", world.Pack.Select(item => item.Name))} · 금화 {world.Vitals?.Gold} · 창: {world.Talking?.What}");

        await world.ShutDialogueAsync(_deadline.Token);
        await Until(() => world.Talking is null, $"창을 닫았는데 서버가 닫지 않았습니다. 마지막 창: {world.Talking?.What}");
    }

    /// <summary>Waits for a window opened after the <paramref name="seen" />-th one that is the one wanted.</summary>
    private async Task<Dialogue> Window(WorldClient world, int seen, Func<Dialogue, bool> wanted)
    {
        await Until(() => world.TalkCount > seen && world.Talking is { } talk && wanted(talk),
            $"기다린 창이 오지 않았습니다. 마지막 창: {world.Talking?.Kind} {world.Talking?.What}");

        return world.Talking!;
    }

    private async Task Until(Func<bool> condition, string failure)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(failure);
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
