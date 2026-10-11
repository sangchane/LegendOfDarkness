using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 아벨해안 킹아크퍼스 퀘스트(사용자 2026-10-11, <c>autopilot/king-arcfus-quest/SPEC.md</c>). 대장장이(아벨무기점 5,4)가 껍질 셋 2개씩으로 대지의인장,
/// 아벨해안3-C 숨은 칸(20,37) → 외딴곳 브라토가 해저의팬던트. 둘을 들고 4-C 바위 틈(10,39)을 밟으면 외딴길(코마밭) → 숨겨진길A·B(잡몹을 다 잡아야
/// 지나감) → 거대문어의본거지(가짜, 문어다리). 문어다리를 든 채 문어를 잡으면 문어의원한, 밀레스신전 제단(9,4)에 문어다리 + 문어의원한 7 →
/// 원한의에센스. 에센스로 다시 들어가면 킹아크퍼스의본거지 — 잡고 미레를 누르면 세줄금반지를 받고 대기실로 나온다(<c>Pack599/Quests/킹아크퍼스*.cs</c>).
/// 시험은 잡몹을 문어 둘씩(체력 1)으로, 두 보스도 체력 1 로 한다 — 흐름만 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class KingArcfusQuestTests : IDisposable
{
    private const int Smithy = 20031, Coast3C = 20591, Coast4C = 20594, Lobby = 20595, Nook = 20883, Shrine = 20291;
    private const string Who = "arcfustest";
    private const string Trail = "킹아크퍼스" + Who + "외딴길", RoadA = "킹아크퍼스" + Who + "숨겨진길A", RoadB = "킹아크퍼스" + Who + "숨겨진길B";
    private const string FakeLair = "킹아크퍼스" + Who + "거대문어의본거지", Lair = "킹아크퍼스" + Who + "킹아크퍼스의본거지";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(14));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Seal_and_pendant_open_the_reef_and_the_essence_from_the_fake_lair_leads_to_the_real_king_arcfus_and_mire()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (Smithy, 5, 6));
        Waiting.MakeGameMaster(server, Who);
        _server = server;

        // 아벨해안 3-C·4-C 사냥 괴물은 치운다(흐름만 본다).
        foreach (string hunter in Directory.GetFiles(Path.Combine(server.ContentLocation, "templates", "monsters", "5.99"), "*@아벨해안?-c.json"))
            File.Delete(hunter);
        string entry = Path.Combine(server.ContentLocation, "scripts", "Pack599", "Quests", "킹아크퍼스암초.cs");
        string script = File.ReadAllText(entry);
        string[] cuts =
        [
            "{ \"문어\", \"애스코모이드\", \"슬러그2\", \"일2\" }",
            "(V)0L, (V)0L, (V)0L, (V)MinionsEach",
            "(V)\"킹아크퍼스\", lair, (V)0L, (V)0L, (V)0L, (V)1L",
            "(V)\"거대문어\", lair, (V)0L, (V)0L, (V)0L, (V)1L",
        ];
        foreach (string cut in cuts)
            Assert.Contains(cut, script);
        File.WriteAllText(entry, script
            .Replace(cuts[0], "{ \"문어\" }")
            .Replace(cuts[1], "(V)0L, (V)0L, (V)1L, (V)MinionsEach")
            .Replace(cuts[2], "(V)\"킹아크퍼스\", lair, (V)0L, (V)0L, (V)1L, (V)1L")
            .Replace(cuts[3], "(V)\"거대문어\", lair, (V)0L, (V)0L, (V)1L, (V)1L"));

        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Who);
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 60;
        character["_MaximumHp"] = 50000;
        character["CurrentHp"] = 50000;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == Smithy, "아벨무기점에 들어가지 못했습니다.", _deadline.Token);

        // ① 대장장이 — 껍질이 없으면 말만, 셋 2개씩이면 대지의인장.
        Creature smith = await Find(world, new Tile(5, 4), "아벨무기점 5,4 에 대장장이가 없습니다.");
        Assert.Contains("2개씩", await Click(world, smith));
        foreach (string shell in new[] { "터틀껍질", "게껍질", "퐁퐁이조각" })
            await Give(world, shell, 2);
        Assert.Contains("재료가 다 모였군", await Click(world, smith));
        await Next(world);
        await Waiting.Until(() => Count(world, "대지의인장") == 1 && Count(world, "터틀껍질") == 0,
            $"대지의인장을 받지 못했습니다. 가방: {Pack(world)} · 서버 기록: {Log(server)}", _deadline.Token);

        // ② 인장만으로는 바위 틈이 열리지 않는다.
        await Tp(world, "아벨해안4-C", 10, 38, Coast4C);
        await Waiting.WalkUntil(world, Direction.South, () => world.Said.Contains("바위 틈이 희미하게"), _deadline.Token, attempts: 4);
        await Waiting.Until(() => world.Said.Contains("바위 틈이 희미하게"), $"팬던트 없이 바위 틈을 밟았는데 알림이 없습니다. 서버가 한 말: {world.Said} · 서버 기록: {Log(server)}", _deadline.Token);
        Assert.Equal(Coast4C, world.State?.Map.Id);

        // ③ 3-C 숨은 칸(대지의인장이 있어야) → 외딴곳 5,8 → 브라토 → 해저의팬던트 · 대기실 20,17.
        await Tp(world, "아벨해안3-C", 19, 37, Coast3C);
        await Waiting.WalkUntil(world, Direction.East, () => world.State?.Map.Id == Nook, _deadline.Token, attempts: 4);
        await Waiting.Until(() => world.State is { } s && s.Map.Id == Nook && s.Where == new Tile(5, 8),
            $"숨은 칸에서 외딴곳 5,8 로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);
        Creature brato = await Find(world, new Tile(6, 5), "외딴곳 6,5 에 브라토가 없습니다.");
        Assert.Contains("해저의팬던트", await Click(world, brato));
        await Next(world);
        await Next(world);
        await Waiting.Until(() => world.State is { } s && s.Map.Id == Lobby && s.Where == new Tile(20, 17) && Count(world, "해저의팬던트") == 1,
            $"브라토가 팬던트를 주고 대기실 20,17 로 보내지 않았습니다. 마지막: {world.State} · 가방: {Pack(world)}", _deadline.Token);

        // ④ 둘 다 있으면 외딴길 21,57(코마 칸은 맨 끝 ⑩에서 — 체력 1로 문어 방에 들지 않게).
        await EnterReef(world, server, "아벨신해안외딴길");
        await Waiting.Until(() => world.State?.Where == new Tile(21, 57), $"외딴길 21,57 에 서지 않았습니다. 마지막: {world.State}", _deadline.Token);

        // ⑤ 외딴길 북쪽 끝 → 숨겨진길A 1,1. 문어가 남아 있으면 나가는 칸(12,13)이 막힌다. 문어다리 없이 잡은 문어는 문어의원한을 안 떨군다.
        await Tp(world, Trail, 11, 1, null);
        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Map.Name == "신해안의숨겨진길A", _deadline.Token, attempts: 4);
        await Waiting.Until(() => world.State is { } s && s.Map.Name == "신해안의숨겨진길A" && s.Where == new Tile(1, 1),
            $"외딴길 끝에서 숨겨진길A 1,1 로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);
        await Tp(world, RoadA, 12, 12, null);
        await Waiting.WalkUntil(world, Direction.South, () => world.Said.Contains("남아있는 몬스터수"), _deadline.Token, attempts: 4);
        await Waiting.Until(() => world.Said.Contains("남아있는 몬스터수"), $"문어가 남았는데 나가는 칸이 열려 있습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}", _deadline.Token);
        await KillAll(world, RoadA, "신해안의숨겨진길A", new Tile(6, 7));
        await Tp(world, RoadA, 12, 12, null);
        await Waiting.WalkUntil(world, Direction.South, () => world.State?.Map.Name == "신해안의숨겨진길B", _deadline.Token, attempts: 4);
        await Waiting.Until(() => world.State?.Map.Name == "신해안의숨겨진길B",
            $"문어를 다 잡았는데 숨겨진길B 로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}", _deadline.Token);
        Assert.Equal(0, Count(world, "문어의원한"));

        // ⑥ 문어다리를 들고 잡으면 문어 한 마리에 문어의원한 하나(방을 나갈 때 바닥 것을 챙긴다 — Instances.HandOver).
        await Give(world, "문어다리", 1);
        await KillAll(world, RoadB, "신해안의숨겨진길B", new Tile(6, 11));
        await Tp(world, RoadB, 12, 10, null);
        await Waiting.WalkUntil(world, Direction.South, () => world.State?.Map.Name == "거대문어의본거지", _deadline.Token, attempts: 3);
        await Waiting.Until(() => world.State is { } s && s.Map.Name == "거대문어의본거지" && s.Where == new Tile(5, 5),
            $"숨겨진길B 끝에서 거대문어의본거지 5,5 로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 보이는 것: {Seen(world)}", _deadline.Token);
        Assert.Equal(2, Count(world, "문어의원한"));

        // ⑦ 거대문어(가짜)를 잡고 나가는 칸(18,18) → 대기실, 문어다리 하나 더.
        await KillAll(world, FakeLair, "거대문어의본거지", new Tile(10, 10));
        await Tp(world, FakeLair, 17, 18, null);
        await Waiting.WalkUntil(world, Direction.East, () => world.State?.Map.Id == Lobby, _deadline.Token, attempts: 4);
        await Waiting.Until(() => world.State is { } s && s.Map.Id == Lobby && Count(world, "문어다리") == 2,
            $"거대문어를 잡고 대기실로 나오며 문어다리를 챙기지 못했습니다. 마지막: {world.State} · 가방: {Pack(world)}", _deadline.Token);

        // ⑧ 밀레스신전 제단 — 문어의원한이 모자라면 말만, 7개면 원한의에센스.
        await Tp(world, "밀레스신전", 9, 5, Shrine);
        Creature altar = await Find(world, new Tile(9, 4), "밀레스신전 9,4 에 제단이 없습니다.");
        Assert.Contains("(2/7)", await Click(world, altar));
        await Give(world, "문어의원한", 5);
        Assert.Contains("붉게 빛나며", await Click(world, altar));
        await Next(world);
        await Waiting.Until(() => Count(world, "원한의에센스") == 1 && Count(world, "문어의원한") == 0 && Count(world, "문어다리") == 1,
            $"제단이 원한의에센스를 주지 않았습니다. 가방: {Pack(world)}", _deadline.Token);

        // ⑨ 에센스로 들어가면 진짜 킹아크퍼스의본거지 — 에센스는 미레를 누를 때 쓴다. 킹아크퍼스가 살아 있으면 미레는 「아직」.
        await EnterReef(world, server, "아벨신해안외딴길");
        Assert.Equal(1, Count(world, "원한의에센스"));
        await Tp(world, Lair, 1, 9, null);
        await Waiting.Until(() => world.State?.Map.Name == "킹아크퍼스의본거지", $"킹아크퍼스의본거지에 들어가지 못했습니다. 마지막: {world.State}", _deadline.Token);
        Creature mire = await Find(world, new Tile(10, 10), "본거지 10,10 에 미레가 없습니다.");
        Assert.Contains("아직 저기", await Click(world, mire));
        await KillAll(world, Lair, "킹아크퍼스의본거지", new Tile(5, 5));
        while (world.TakeTold(out _, out _))
        {
        }

        Assert.Contains("우앙", await Click(world, mire));
        await Next(world);
        await Waiting.Until(() => world.State is { } s && s.Map.Id == Lobby && s.Where == new Tile(20, 17) && Count(world, "세줄금반지") == 1 && Count(world, "원한의에센스") == 0,
            $"미레를 구했는데 세줄금반지를 들고 대기실 20,17 로 나오지 않았습니다. 마지막: {world.State} · 가방: {Pack(world)} · 서버 기록: {Log(server)}", _deadline.Token);
        List<string> told = [];
        while (world.TakeTold(out _, out string line))
            told.Add(line);
        Assert.Contains(told, line => line.Contains("세줄금반지를 받았습니다"));

        // ⑩ 인장·팬던트는 남는다 — 다시 들어가 북쪽으로 걸으면 코마 칸(21,51)에서 체력 1.
        Assert.Equal(1, Count(world, "대지의인장"));
        Assert.Equal(1, Count(world, "해저의팬던트"));
        await EnterReef(world, server, "아벨신해안외딴길");
        await Waiting.Until(() => world.State?.Where == new Tile(21, 57), $"외딴길 21,57 에 서지 않았습니다. 마지막: {world.State}", _deadline.Token);
        await Waiting.WalkUntil(world, Direction.North, () => world.Said.Contains("그림자가 드리웁니다"), _deadline.Token, attempts: 10);
        List<string> seenHp = [];
        await Waiting.Until(() => { seenHp.Add($"{world.Vitals?.Health}/{world.Vitals?.MaximumHealth}"); return world.Vitals?.Health == 1; },
            $"코마 칸을 밟았는데 체력이 1이 아닙니다: {world.Vitals?.Health} · 본 값: {string.Join(",", seenHp.Distinct())} · 자리: {world.State} · 서버 기록: {Log(_server!)}", _deadline.Token, TimeSpan.FromSeconds(8));
    }


    /// <summary>4-C 바위 틈(10,39) 북쪽 칸(10,38)으로 옮겨 남쪽으로 한 걸음 — 외딴길 사본에 들어설 때까지.</summary>
    private async Task EnterReef(WorldClient world, IsolatedHadesServer server, string shown)
    {
        await Tp(world, "아벨해안4-C", 10, 38, Coast4C);
        await Waiting.WalkUntil(world, Direction.South, () => world.State?.Map.Name == shown, _deadline.Token, attempts: 4);
        await Waiting.Until(() => world.State?.Map.Name == shown,
            $"바위 틈을 밟았는데 {shown} 에 들어가지 못했습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 서버 기록: {Log(server)}", _deadline.Token);
    }

    private IsolatedHadesServer? _server;

    private async Task Tp(WorldClient world, string map, int x, int y, int? id)
    {
        await world.SayAsync($"/tp \"{map}\" {x} {y}", _deadline.Token);
        await Waiting.Until(() => world.State is { } s && s.Where == new Tile(x, y) && (id is null || s.Map.Id == id),
            $"{map} {x},{y} 로 옮겨지지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 대화: {world.Talking?.What} · 서버 끝줄: {string.Join(" / ", _server?.ConsoleOutput.Split('\n').TakeLast(8) ?? [])}", _deadline.Token);
        await Task.Delay(400, _deadline.Token);
    }

    private async Task Give(WorldClient world, string item, int count)
    {
        int before = Count(world, item);
        await world.SayAsync($"/give \"{item}\" {count}", _deadline.Token);
        await Waiting.Until(() => Count(world, item) >= before + count, $"{item} {count}개가 오지 않았습니다. 가방: {Pack(world)}", _deadline.Token);
    }

    private static int Count(WorldClient world, string item) =>
        world.Pack.Where(i => i.Name.StartsWith(item)).Sum(i => Math.Max(1, i.Stacks));

    private static string Seen(WorldClient world) => string.Join(", ", world.Creatures.Select(c => $"{c.Kind}:{c.Name}@{c.Where}"));

    private static string Pack(WorldClient world) => string.Join(", ", world.Pack.Select(i => $"{i.Name}×{i.Stacks}"));

    private async Task Next(WorldClient world)
    {
        Dialogue talking = world.Talking!;
        await world.AnswerAsync(talking.Serial, talking.Options.First().Step, _deadline.Token);
        // 대본의 마지막 말에 대답하면 새 창이 오지 않는다 — 다음 말이 오면 그때까지만 기다린다.
        DateTime until = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (world.Talking == talking && DateTime.UtcNow < until)
            await Task.Delay(100, _deadline.Token);
        if (world.Talking == talking)
            await world.ShutDialogueAsync(_deadline.Token);
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

    /// <summary>이 방의 괴물을 다 잡는다(체력 1). 가장 가까운 것 곁으로 운영자 순간이동해 돌아서 친다 — 괴물이 돌아다녀 걸어서 쫓으면 놓친다.</summary>
    private async Task KillAll(WorldClient world, string copy, string room, Tile middle)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        bool looked = false;
        while (DateTime.UtcNow < giveUp && world.State?.Map.Name == room)
        {
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
            if (world.State is not { } me)
                continue;
            Creature? target = world.Creatures.Where(c => c.Kind == CreatureKind.Hostile)
                .OrderBy(c => Math.Abs(c.Where.X - me.Where.X) + Math.Abs(c.Where.Y - me.Where.Y)).FirstOrDefault();
            if (target is null)
            {
                // 방 끝에서는 반대쪽이 시야 밖이다 — 가운데에서 한 번 더 보고 그래도 없으면 끝.
                if (looked)
                    return;
                looked = true;
                await world.SayAsync($"/tp \"{copy}\" {middle.X} {middle.Y}", _deadline.Token);   // 괴물이 서 있으면 곁 칸에 내린다
                await Task.Delay(1500, _deadline.Token);
                continue;
            }

            looked = false;
            int dx = target.Where.X - me.Where.X, dy = target.Where.Y - me.Where.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1)
            {
                await world.SayAsync($"/tp \"{copy}\" {target.Where.X} {target.Where.Y + 1}", _deadline.Token);
                await Task.Delay(1200, _deadline.Token);
                continue;
            }

            Direction facing = dx != 0 ? (dx > 0 ? Direction.East : Direction.West) : (dy > 0 ? Direction.South : Direction.North);
            await world.TurnAsync(facing, _deadline.Token);
            await world.AttackAsync(_deadline.Token);
            await Task.Delay(600, _deadline.Token);
        }

        Assert.DoesNotContain(world.Creatures, c => c.Kind == CreatureKind.Hostile);
    }

    private static string Log(IsolatedHadesServer server) =>
        string.Join(" / ", server.ConsoleOutput.Split('\n').Where(line => line.Contains("멈췄") || line.Contains("옮기지 않은") || line.Contains("rror")).TakeLast(4));
}
