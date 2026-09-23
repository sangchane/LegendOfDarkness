using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 죽은 사람은 뮤레칸의방(20138) 10,9 로 간다 — 5.99·Novaonline `__SCRIPT_DEAD__`. 전에는 하데스 자기 죽음 맵(99999 "Hades", NPC 없음)으로
/// 가서 되살아날 길이 없었다(monk2). 그렇게 저장된 유령이 다시 들어오면 뮤레칸의방에서 깨어나고, 뮤레칸(12,5)을 누르면 살아나
/// 레벨에 맞는 마을로 간다(5.99 `NPC_뮤레칸` — 20레벨 아래는 노비스마을 37,29).
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class MurekanTests : IDisposable
{
    private const int OldDeathMap = 99999;
    private const int MurekansRoom = 20138;
    private const int NoviceVillage = 20373;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_ghost_saved_on_the_old_death_map_wakes_at_murekan_who_revives_it_in_the_novice_village()
    {
        const string who = "oldghost";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);

        // monk2 가 저장된 모습 그대로 — 옛 죽음 맵 9,15 · 유령 · 체력 1.
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["CurrentMapId"] = OldDeathMap;
        character["X"] = 9;
        character["Y"] = 15;
        character["Flags"] = 1;
        character["CurrentHp"] = 1;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == MurekansRoom && state.Where == new Tile(10, 9),
            $"옛 죽음 맵에 저장된 유령이 뮤레칸의방 10,9 에서 깨어나지 않았습니다. 마지막: {world.State}", _deadline.Token);

        Creature murekan = null!;
        await Waiting.Until(() => (murekan = world.Creatures.FirstOrDefault(c => c.Where == new Tile(12, 5))!) is not null,
            "뮤레칸의방 12,5 에 뮤레칸이 없습니다.", _deadline.Token);
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
    }
}
