using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 포테의숲 오솔길 — 5.99 의 개인 던전. 5존 26,0 을 밟으면 `포테의숲오솔길입장` 스크립트가 묻고, "입장한다" 면 그 캐릭터 전용으로
/// 오솔길 → 대기실 → 보스존 세 맵을 새로 짓는다(`map_create` · `warp_create`, 서버 `Systems/Instances`). 대기실에는 사나운은빛늑대 6마리가
/// 서고, 그 늑대를 다 잡기 전에는 보스방으로 못 간다(`warp_create` 끝 칸 1). 보스방의 늑대 8마리와 자이언트맨티스를 다 잡으면 던전 스크립트
/// (`Dungeon__Script`, 1초마다)가 5초를 센 뒤 경험치 20만을 주고 5존 16,16 으로 내보낸다. 운영자는 한 방이 200배라 금방 잡는다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class PoteDungeonTests : IDisposable
{
    private const int FifthZone = 20267;

    /// <summary>사본은 원래 맵 번호를 알린다 — 클라이언트가 그 맵 그림을 쓴다.</summary>
    private const int Trail = 20194;
    private const int WaitingRoom = 20195;
    private const int BossRoom = 20269;

    private const string Name = "potetrail";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(15));

    private IsolatedHadesServer? _server;

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Entering_the_trail_builds_a_private_copy_whose_waiting_room_holds_the_way_until_the_wolves_are_gone()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (FifthZone, 26, 1));
        _server = server;
        Waiting.MakeGameMaster(server, Name);

        // 5존 사냥터 괴물은 치운다 — 엔트자이언트의 나르콜리에 잠들면 서버가 대화 대답을 받지 않는다(자는 동안은 아무것도 못 한다).
        foreach (string hunter in Directory.GetFiles(Path.Combine(server.ContentLocation, "templates", "monsters", "5.99"), "*@포테의숲5존.json"))
        {
            File.Delete(hunter);
        }

        // 던전 괴물 체력만 1 로 — 마릿수(대기실 늑대 6 · 보스방 늑대 8 · 자이언트맨티스 1)와 흐름은 그대로 두고 잡는 시간만 줄인다.
        string entry = Path.Combine(server.ContentLocation, "scripts", "Pack599", "Npcs", "포테의숲오솔길입장.cs");
        File.WriteAllText(entry, File.ReadAllText(entry).Replace("(V)4500L", "(V)1L").Replace("(V)15000L", "(V)1L"));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        // 입장은 레벨 21~52. 대기실 늑대에 쓰러지지 않게 체력을 넉넉히.
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 30;
        character["_MaximumHp"] = 20000;
        character["CurrentHp"] = 20000;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == FifthZone, "포테의숲5존에 들어가지 못했습니다.", _deadline.Token);

        Creature host = null!;
        await Waiting.Until(() => (host = world.Creatures.FirstOrDefault(c => c.Where == new Tile(28, 0))!) is not null,
            "5존 28,0 에 입장 스크립트 NPC 가 없습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.North, () => world.Talking is not null, _deadline.Token);
        await Waiting.Until(() => world.Talking?.What.Contains("포테의숲 오솔길로 입장") == true,
            $"26,0 을 밟았는데 입장을 묻지 않았습니다. 마지막 창: {world.Talking?.What} · 서버가 한 말: {world.Said}", _deadline.Token);

        DialogueOption enter = world.Talking!.Options.First(option => option.Text.StartsWith("입장한다"));
        await world.AnswerAsync(world.Talking.Serial, enter.Step, _deadline.Token);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == Trail && state.Map.Name == "포테의숲오솔길" && state.Where == new Tile(9, 39),
            $"오솔길 사본 9,39 로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 서버 기록: {PackLog(server)}", _deadline.Token);

        // 오솔길은 걸을 때마다 1/5 로 마비된다 — 엔트자이언트의날개가 있으면 대신 하나 쓴다(Dungeon__Script).
        await world.SayAsync("/give \"엔트자이언트의날개\" 30", _deadline.Token);
        await Waiting.Until(() => world.Pack.Any(item => item.Name.StartsWith("엔트자이언트의날개")), "날개가 오지 않았습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Map.Id == WaitingRoom, _deadline.Token, attempts: 80);
        await Waiting.Until(() => world.State is { } state && state.Map.Id == WaitingRoom && state.Where == new Tile(4, 9),
            $"오솔길 끝 9,0 에서 대기실 4,9 로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "대기실에 늑대가 없습니다.", _deadline.Token);

        // 서버가 센 수를 문구로 알려 준다 — 스크립트가 세운 6마리가 다 서 있어야 한다.
        await StepTo(world, new Tile(4, 1));
        await Waiting.WalkUntil(world, Direction.North, () => world.Said.Contains("남아있는 몬스터수"), _deadline.Token, attempts: 10);
        Assert.Contains("남아있는 몬스터수 : 6]", world.Said);
        Assert.Equal(WaitingRoom, world.State?.Map.Id);

        await KillEverything(world, WaitingRoom);

        await ThroughDoor(world, WaitingRoom, BossRoom);
        await Waiting.Until(() => world.State is { } state && state.Map.Id == BossRoom && state.Map.Name == "포테의숲오솔길보스존",
            $"늑대를 다 잡았는데 보스방으로 가지 않았습니다. 서버 기록: {PackLog(server)} · 마지막: {world.State} · 서버가 한 말: {world.Said} · 보이는 것: {string.Join(", ", world.Creatures.Select(c => $"{c.Kind}:{c.Name}#{c.Sprite}@{c.Where}"))}", _deadline.Token);

        long before = world.Vitals?.Experience ?? 0;
        await KillEverything(world, BossRoom);

        await Waiting.Until(() => world.State is { } state && state.Map.Id == FifthZone && state.Where == new Tile(16, 16),
            $"보스방을 비웠는데 5존 16,16 으로 나가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 서버 기록: {PackLog(server)}",
            _deadline.Token, TimeSpan.FromSeconds(40));
        Assert.True((world.Vitals?.Experience ?? 0) - before >= 200000,
            $"클리어 경험치 20만이 들어오지 않았습니다: {before} → {world.Vitals?.Experience}");
    }

    /// <summary>
    /// 문(4,0)을 지난다. 마지막 괴물이 죽은 것이 서버에서 처리되기 전에 밟으면 한 번 거절되고, 운영자는 맵 끝을 지나 걸으므로 문 칸에서 더
    /// 올라가 버린다 — 그래서 문 앞(4,1)으로 돌아가 다시 밟는다. 그 사이 남은 괴물이 보이면 잡는다.
    /// </summary>
    private async Task ThroughDoor(WorldClient world, int room, int next)
    {
        for (int attempt = 0; attempt < 8 && world.State?.Map.Id != next; attempt++)
        {
            await StepTo(world, new Tile(4, 1));
            string before = world.Said;
            // 서버가 화면을 다시 보내는 동안은 걸음을 버리므로 될 때까지 걷는다.
            await Waiting.WalkUntil(world, Direction.North,
                () => world.State?.Map.Id == next || (world.Said != before && world.Said.Contains("남아있는 몬스터수")), _deadline.Token, attempts: 4);

            if (world.State?.Map.Id != next && world.Said.Contains("남아있는 몬스터수"))
                await KillEverything(world, room);
        }
    }

    private static Queue<Tile> Lookouts(MapInfo map) => new(new[]
    {
        new Tile(map.Columns / 4, map.Rows / 4), new Tile(map.Columns * 3 / 4, map.Rows / 4),
        new Tile(map.Columns * 3 / 4, map.Rows * 3 / 4), new Tile(map.Columns / 4, map.Rows * 3 / 4)
    });

    /// <summary>한 칸씩 걸어 그 칸에 선다. 걸을 때마다 서버에 자리를 다시 물어 본다 — 걸음 뒤 자리는 서버가 말해 줘야 안다.</summary>
    private async Task StepTo(WorldClient world, Tile goal)
    {
        int? map = world.State?.Map.Id;

        for (int step = 0; step < 40; step++)
        {
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);

            // 가는 사이 맵이 바뀌었으면(던전이 내보냄) 그 맵에서 계속 걷지 않는다.
            if (world.State is not { } me || me.Where == goal || me.Map.Id != map)
                return;

            Direction toward = me.Where.X != goal.X
                ? (goal.X > me.Where.X ? Direction.East : Direction.West)
                : (goal.Y > me.Where.Y ? Direction.South : Direction.North);
            await world.WalkAsync(toward, _deadline.Token);
            await Task.Delay(300, _deadline.Token);
        }

        Assert.Fail($"{goal} 에 서지 못했습니다. 마지막: {world.State}");
    }

    /// <summary>그 방의 괴물이 다 없어질 때까지 가장 가까운 것 쪽으로 한 칸 가서 휘두른다.</summary>
    private async Task KillEverything(WorldClient world, int room)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(6);
        Queue<Tile> lookouts = world.State is { } start ? Lookouts(start.Map) : new Queue<Tile>();

        while (DateTime.UtcNow < giveUp && world.State?.Map.Id == room)
        {
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(300, _deadline.Token);

            if (world.State is not { } me)
                continue;

            Creature? target = world.Creatures.Where(c => c.Kind == CreatureKind.Hostile)
                .OrderBy(c => Math.Abs(c.Where.X - me.Where.X) + Math.Abs(c.Where.Y - me.Where.Y)).FirstOrDefault();

            if (target is null)
            {
                // 괴물은 제자리에 서 있고(PathQualifer 1) 시야는 열두 칸이라, 20x20 방은 한 자리에서 다 안 보인다.
                // 네 구역을 돌며 보고, 어디서도 안 보이면 끝.
                if (lookouts.Count == 0)
                    return;
                await StepTo(world, lookouts.Dequeue());
                continue;
            }

            lookouts = Lookouts(me.Map);

            int dx = target.Where.X - me.Where.X, dy = target.Where.Y - me.Where.Y;
            Direction toward = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            // 붙어 있으면 돌아서 휘두르고, 아니면 한 칸 다가간다. 그 칸이 막혀 못 가면 다른 축으로 돌아간다.
            if (Math.Abs(dx) + Math.Abs(dy) == 1)
            {
                await world.TurnAsync(toward, _deadline.Token);
                await world.AttackAsync(_deadline.Token);
                await Task.Delay(600, _deadline.Token);
                continue;
            }

            Tile from = me.Where;
            await world.WalkAsync(toward, _deadline.Token);
            await Task.Delay(400, _deadline.Token);

            if (world.State?.Where == from && dx != 0 && dy != 0)
            {
                Direction other = Math.Abs(dx) >= Math.Abs(dy)
                    ? (dy >= 0 ? Direction.South : Direction.North)
                    : (dx >= 0 ? Direction.East : Direction.West);
                await world.WalkAsync(other, _deadline.Token);
            }
        }

        // 방을 벗어났으면(보스방을 비워 던전 스크립트가 내보낸 경우) 다 잡은 것이다.
        if (world.State?.Map.Id != room)
            return;

        Assert.Fail($"{room} 방의 괴물을 6분 안에 다 잡지 못했습니다: {world.Creatures.Count(c => c.Kind == CreatureKind.Hostile)}마리 남음 · 내 자리 {world.State?.Where} · 서버 기록: {PackLog(_server!)}");
    }

    /// <summary>격리 서버가 남긴 5.99 명령·스크립트 기록 — 실패했을 때 무엇이 비었는지 보이려고.</summary>
    private static string PackLog(IsolatedHadesServer server) =>
        string.Join(" / ", server.ConsoleOutput.Split('\n').Where(line => line.Contains("멈췄") || line.Contains("옮기지 않은") || line.Contains("rror")).TakeLast(4));
}
