using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 뤼케시온해안 에리얼 퀘스트(사용자 2026-10-11, <c>autopilot/aerial-quest/SPEC.md</c>). 3-D 동쪽 에리얼입구(57,25)에게 말하면 그룹 전용
/// 게길 → 대기실 → 에리얼의방 사본을 짓는다(<c>Pack599/Quests/에리얼입구.cs</c>). 게길의 게(<c>Monsters/AerialCrab.cs</c>)는 옆 칸에 선 사람의
/// 제물 한 벌(블루하콘무기·레드하콘다리)을 빼앗고, 없으면 3-D 로 쫓아낸다 — 킹아크퍼스의팬던트가 있으면 건드리지 않는다. 에리얼을 잡고
/// 마레(<c>Pack599/Quests/마레.cs</c>)를 누르면 파파야방패를 받고 3-D 로 나온다.
/// 시험은 게를 시작 칸(1,6) 옆(2,5)에 한 마리만, 움직이지 않게 세우고 에리얼 체력을 1 로 한다 — 흐름만 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class AerialQuestTests : IDisposable
{
    private const int Shore = 20881;
    private const int Trail = 20882;          // 사본은 원본 번호를 알린다 — 대기실·에리얼의방은 3-D(Shore) 번호
    private const string Who = "aerialtest";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(12));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Offerings_get_you_through_the_crab_trail_and_saving_mare_after_aerial_gives_the_papaya_shield()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (Shore, 55, 25));
        Waiting.MakeGameMaster(server, Who);

        // 3-D 사냥 괴물은 치운다(흐름만 본다).
        foreach (string hunter in Directory.GetFiles(Path.Combine(server.ContentLocation, "templates", "monsters", "뤼케시온해안"), "*@뤼케시온해안3-D.json"))
            File.Delete(hunter);
        string crab = Path.Combine(server.ContentLocation, "templates", "monsters", "뤼케시온해안", "게@뤼케시온해안게길.json");
        JsonNode crabTemplate = JsonNode.Parse(File.ReadAllText(crab))!;
        crabTemplate["MovementSpeed"] = 600000;
        File.WriteAllText(crab, crabTemplate.ToJsonString());
        string entry = Path.Combine(server.ContentLocation, "scripts", "Pack599", "Quests", "에리얼입구.cs");
        string script = File.ReadAllText(entry);
        const string crabs = "{ (8, 2), (12, 5), (16, 7), (20, 3), (24, 6), (28, 1), (32, 4), (36, 2), (40, 5), (44, 7), (48, 3), (52, 6), (56, 1), (60, 4), (64, 2) }";
        Assert.Contains(crabs, script);
        Assert.Contains("(V)\"에리얼\", room, (V)0L, (V)0L, (V)0L, (V)1L", script);
        File.WriteAllText(entry, script
            .Replace(crabs, "{ (2, 5) }")
            .Replace("(V)\"에리얼\", room, (V)0L, (V)0L, (V)0L, (V)1L", "(V)\"에리얼\", room, (V)0L, (V)0L, (V)1L, (V)1L"));

        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Who);
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 80;
        character["_MaximumHp"] = 50000;
        character["CurrentHp"] = 50000;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == Shore, "뤼케시온해안3-D 에 들어가지 못했습니다.", _deadline.Token);

        // ① 제물이 없으면 못 들어간다.
        string refused = await Talk(world, server);
        Assert.Contains("한 벌도 없습니다", refused);
        Assert.Equal(Shore, world.State?.Map.Id);

        // ② 제물 한 벌로 들어가면 게 옆(시작 칸)에서 한 벌을 빼앗기고, 3초 뒤 또 집히면 없으니 3-D 로 쫓겨난다.
        await Give(world, "블루하콘무기", 1);
        await Give(world, "레드하콘다리", 1);
        await Talk(world, server);
        await Waiting.Until(() => world.Said.Contains("남은 제물 0벌"),
            $"게 옆에 섰는데 제물을 빼앗지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 서버 기록: {Log(server)}", _deadline.Token, TimeSpan.FromSeconds(20));
        await Waiting.Until(() => world.State?.Map.Id == Shore && world.Said.Contains("쫓겨났습니다"),
            $"제물이 없는데 3-D 로 쫓겨나지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}", _deadline.Token, TimeSpan.FromSeconds(20));
        Assert.DoesNotContain(world.Pack, item => item.Name.StartsWith("블루하콘무기") || item.Name.StartsWith("레드하콘다리"));

        // ③ 킹아크퍼스의팬던트가 있으면 제물 없이 들어가고 게가 건드리지 않는다.
        await Give(world, "킹아크퍼스의팬던트", 1);
        await Talk(world, server);
        await Waiting.Until(() => world.State is { } s && s.Map.Id == Trail && s.Where == new Tile(1, 6),
            $"팬던트로 게길 1,6 에 들어가지 못했습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}", _deadline.Token);
        await Task.Delay(4000, _deadline.Token);
        Assert.Equal(Trail, world.State?.Map.Id);

        // ④ 게길 동쪽 끝(69,5~8) → 대기실 20,30 → 동쪽 문(45,28~31) → 에리얼의방 20,30.
        await Waiting.WalkUntil(world, Direction.East, () => world.State?.Map.Id != Trail, _deadline.Token, attempts: 90);
        await Waiting.Until(() => world.State is { } s && s.Map.Id == Shore && s.Map.Name == "에리얼대기실" && s.Where == new Tile(20, 30),
            $"게길 끝에서 대기실 20,30 으로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);
        await world.WalkAsync(Direction.North, _deadline.Token);
        await Task.Delay(600, _deadline.Token);
        await Waiting.WalkUntil(world, Direction.East, () => world.State?.Map.Name != "에리얼대기실", _deadline.Token, attempts: 40);
        await Waiting.Until(() => world.State is { } s && s.Map.Name == "에리얼의방" && s.Where == new Tile(20, 30),
            $"대기실 문에서 에리얼의방 20,30 으로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        // 에리얼(45,30)·마레(48,27)는 도착 칸에서 시야 밖이다 — 29줄로 올라 동쪽으로 다가간다(43,30 은 막힘).
        await world.WalkAsync(Direction.North, _deadline.Token);
        await Task.Delay(600, _deadline.Token);
        await Waiting.WalkUntil(world, Direction.East,
            () => world.Creatures.Any(c => c.Where == new Tile(48, 27)) || world.State?.Where.X >= 38, _deadline.Token, attempts: 25);

        // 에리얼이 살아 있으면 마레는 「아직」.
        Creature mare = await Find(world, new Tile(48, 27), "에리얼의방 48,27 에 마레가 없습니다.");
        string early = await Click(world, mare);
        Assert.Contains("아직 날뛰고", early);

        Creature aerial = await Find(world, new Tile(45, 30), "에리얼의방 45,30 에 에리얼이 없습니다.");
        long before = world.Vitals?.Experience ?? 0;
        await KillAt(world, aerial);
        await Waiting.Until(() => (world.Vitals?.Experience ?? 0) > before,
            $"에리얼을 쳤는데 경험치가 오르지 않았습니다(안 죽음). 보이는 것: {string.Join(", ", world.Creatures.Select(c => $"{c.Kind}:{c.Name}@{c.Where}"))} · 나: {world.State}",
            _deadline.Token, TimeSpan.FromSeconds(10));

        // 잡은 뒤 마레를 누르면 다음 → 파파야방패 · 3-D 55,25. 알림은 60줄까지만 쌓이므로 앞의 것은 비운다.
        while (world.TakeTold(out _, out _))
        {
        }

        string thanks = await Click(world, mare);
        Assert.Contains("살았다", thanks);
        DialogueOption next = world.Talking!.Options.First();
        await world.AnswerAsync(world.Talking.Serial, next.Step, _deadline.Token);
        await Waiting.Until(() => world.State is { } s && s.Map.Id == Shore && s.Map.Name == "뤼케시온해안3-D" && s.Where == new Tile(55, 25),
            $"마레를 구했는데 3-D 55,25 로 나오지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 서버 기록: {Log(server)}", _deadline.Token);
        await Waiting.Until(() => world.Pack.Any(item => item.Name.StartsWith("파파야방패")), "파파야방패를 받지 못했습니다.", _deadline.Token);
        // Said 는 마지막 한 줄 — 나갈 때 에리얼이 떨군 금화를 챙긴 알림(Instances.HandOver)이 뒤에 온다. 쌓인 것을 다 본다.
        List<string> told = [];
        while (world.TakeTold(out _, out string line))
            told.Add(line);
        Assert.Contains(told, line => line.Contains("파파야방패를 받았습니다"));
    }

    private async Task Give(WorldClient world, string item, int count)
    {
        await world.SayAsync($"/give \"{item}\" {count}", _deadline.Token);
        await Waiting.Until(() => world.Pack.Any(i => i.Name.StartsWith(item)), $"{item} 이 오지 않았습니다.", _deadline.Token);
    }

    /// <summary>3-D 57,25 의 에리얼입구에게 말을 걸어 「입장한다」를 고른다. 들어가지 못했으면 그 말(창 글)을 돌려준다.</summary>
    private async Task<string> Talk(WorldClient world, IsolatedHadesServer server)
    {
        Creature host = await Find(world, new Tile(57, 25), "3-D 57,25 에 에리얼입구가 없습니다.");
        string asked = await Click(world, host);
        Assert.Contains("에리얼", asked);
        DialogueOption pick = world.Talking!.Options.First(o => o.Text.StartsWith("입장한다"));
        await world.AnswerAsync(world.Talking.Serial, pick.Step, _deadline.Token);
        await Waiting.Until(() => world.Talking?.What != asked || world.State?.Map.Id != Shore,
            $"입장 대답 뒤 아무 일도 없었습니다. 서버 기록: {Log(server)}", _deadline.Token);
        return world.Talking?.What ?? "";
    }

    private async Task<string> Click(WorldClient world, Creature who)
    {
        Dialogue? before = world.Talking;
        await world.ClickAsync(who.Serial, _deadline.Token);
        await Waiting.Until(() => world.Talking is not null && world.Talking != before, $"{who.Name} 이 말하지 않았습니다.", _deadline.Token);
        return world.Talking!.What;
    }

    private async Task<Creature> Find(WorldClient world, Tile at, string failure)
    {
        Creature found = null!;
        await Waiting.Until(() => (found = world.Creatures.FirstOrDefault(c => c.Where == at)!) is not null, failure, _deadline.Token);
        return found;
    }

    /// <summary>그 괴물 옆으로 걸어가 쓰러질 때까지 친다(체력 1).</summary>
    private async Task KillAt(WorldClient world, Creature target)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < giveUp && world.Creatures.Any(c => c.Serial == target.Serial))
        {
            // 서버가 알려 준 내 자리는 다시 받아야 새로워진다(PoteDungeonTests.KillEverything 와 같이).
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(300, _deadline.Token);
            if (world.State is not { } me || world.Creatures.FirstOrDefault(c => c.Serial == target.Serial) is not { } now)
                continue;
            int dx = now.Where.X - me.Where.X, dy = now.Where.Y - me.Where.Y;
            Direction toward = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);
            if (Math.Abs(dx) + Math.Abs(dy) == 1)
            {
                await world.TurnAsync(toward, _deadline.Token);
                await world.AttackAsync(_deadline.Token);
            }
            else
            {
                await world.WalkAsync(toward, _deadline.Token);
            }

            await Task.Delay(500, _deadline.Token);
        }

        Assert.DoesNotContain(world.Creatures, c => c.Serial == target.Serial);
    }

    private static string Log(IsolatedHadesServer server) =>
        string.Join(" / ", server.ConsoleOutput.Split('\n').Where(line => line.Contains("멈췄") || line.Contains("옮기지 않은") || line.Contains("rror")).TakeLast(4));
}
