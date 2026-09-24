using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 뮤레칸의방(20138)은 사람을 가두면 안 된다. 유령이 10,9 에서 깨어나 모바일 클라이언트가 보내는 그대로의 말로
/// [지도](0xF0)를 누르고, 뮤레칸(12,5)을 눌러(0x43) "다음"을 넘기면 살아나 레벨에 맞는 마을로 간다.
/// 저장 모습은 live monk4(2026-09-24)와 같다 — 뮤레칸의방 10,9 · 유령(Flags 1) · 체력 1.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class MurekansRoomTests : IDisposable
{
    private const int MurekansRoom = 20138;
    private const int NoviceVillage = 20373;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_ghost_in_murekans_room_can_open_the_world_map_and_murekan_revives_it_into_the_novice_village()
    {
        const string who = "roomghost";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);
        SaveAsGhostInTheRoom(server, who);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == MurekansRoom && state.Where == new Tile(10, 9),
            $"유령이 뮤레칸의방 10,9 에서 깨어나지 않았습니다. 마지막: {world.State}", _deadline.Token);

        // [지도] — 방이 사람을 가두지 않게 여기서도 열린다. 닫으면 손이 풀려야 한다.
        await OpenAndCloseTheWorldMap(world);
        await TalkToMurekan(world);
    }

    [Fact]
    public async Task Killed_alone_the_ghost_can_open_the_world_map_and_murekan_revives_it_in_the_same_session()
    {
        const string who = "freshghost";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, 2, 35));
        ComaTests.StandOneKillerAhead(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);
        Save(server, who, saved =>
        {
            saved["_MaximumHp"] = 100;
            saved["CurrentHp"] = 100;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await ArriveBeforeKiller(world);
        int armor = world.Self!.Wearing!.Armor;

        await world.TurnAsync(Direction.North, _deadline.Token);
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (world.State?.Map.Id != MurekansRoom && DateTime.UtcNow < giveUp)
        {
            if (world.State?.Map.Id == WoodlandOneOne)
                await world.AttackAsync(_deadline.Token);
            await Task.Delay(300, _deadline.Token);
        }

        await Waiting.Until(() => world.State is { } state && state.Map.Id == MurekansRoom && state.Where == new Tile(10, 9),
            $"쓰러진 유령이 뮤레칸의방 10,9 에 서지 않았습니다. 마지막: {world.State}", _deadline.Token);

        await OpenAndCloseTheWorldMap(world);
        await TalkToMurekan(world, armor);
    }

    private async Task ArriveBeforeKiller(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);
        while (!(world.State?.Where == new Tile(2, 35) && world.Creatures.Any(c => c.Where == new Tile(2, 34))))
        {
            Assert.True(DateTime.UtcNow < giveUp, "우드랜드1-1 입구 앞칸에 괴물이 서지 않았습니다.");
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
        }
    }

    private const int WoodlandOneOne = 20015;

    private async Task OpenAndCloseTheWorldMap(WorldClient world)
    {
        await world.OpenFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is not null,
            $"뮤레칸의방에서 [지도]가 열리지 않았습니다. 서버가 한 말: {world.Said}", _deadline.Token, TimeSpan.FromSeconds(10));
        await world.CloseFieldAsync(_deadline.Token);
        await Waiting.Until(() => world.Field is null, "월드맵을 닫았는데 서버가 거두지 않았습니다.", _deadline.Token, TimeSpan.FromSeconds(10));
    }

    private async Task TalkToMurekan(WorldClient world, int? wornArmor = null)
    {
        Creature murekan = null!;
        await Waiting.Until(() => (murekan = world.Creatures.FirstOrDefault(c => c.Where == new Tile(12, 5))!) is not null,
            "뮤레칸의방 12,5 에 뮤레칸이 없습니다.", _deadline.Token);
        Assert.Equal(CreatureKind.Merchant, murekan.Kind);

        // 유령은 유령 몸(ServerFormat33 — 남 0x30)으로, 입은 것 없이 그려진다.
        await Waiting.Until(() => world.Self?.Wearing is { } ghost && ghost.Body >> 4 == GhostMan && ghost.Armor == 0,
            $"유령이 유령 몸(0x30)으로 오지 않았습니다: {world.Self?.Wearing}", _deadline.Token);
        await world.ClickAsync(murekan.Serial, _deadline.Token);

        int answered = 0;
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (world.State?.Map.Id != NoviceVillage && DateTime.UtcNow < giveUp)
        {
            if (world.TalkCount > answered && world.Talking is { } talk && talk.Options.FirstOrDefault(option => option.Text == "다음") is { } next)
            {
                answered = world.TalkCount;
                await world.AnswerAsync(murekan.Serial, next.Step, _deadline.Token);
            }

            await Task.Delay(100, _deadline.Token);
        }

        Assert.True(world.State is { } now && now.Map.Id == NoviceVillage && now.Where == new Tile(37, 29),
            $"뮤레칸이 살려 노비스마을 37,29 로 보내지 않았습니다. 마지막: {world.State} · 창: {world.Talking?.What} · 서버가 한 말: {world.Said}");

        // 살아나면 산 몸(남 0x10)에 입은 것을 다시 입고 그려진다.
        await Waiting.Until(() => world.Self?.Wearing is { } alive && alive.Body >> 4 == LivingMan && alive.Armor == (wornArmor ?? alive.Armor),
            $"살아났는데 산 몸·입은 갑옷({wornArmor})으로 그려지지 않았습니다: {world.Self?.Wearing}", _deadline.Token);
    }

    private const int LivingMan = 1;
    private const int GhostMan = 3;

    private static void Save(IsolatedHadesServer server, string who, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void SaveAsGhostInTheRoom(IsolatedHadesServer server, string who)
    {
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["CurrentMapId"] = MurekansRoom;
        character["X"] = 10;
        character["Y"] = 9;
        character["Flags"] = 1;
        character["CurrentHp"] = 1;
        File.WriteAllText(saved, character.ToJsonString());
    }
}
