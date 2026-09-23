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
        (IsolatedHadesServer server, WorldSession session, WorldClient world) = await EnterTrail(Name, gameMaster: true);
        using IsolatedHadesServer ownedServer = server;
        using WorldSession ownedSession = session;

        // 오솔길은 걸을 때마다 1/5 로 마비된다 — 엔트자이언트의날개가 있으면 대신 하나 쓴다(Dungeon__Script).
        await world.SayAsync("/give \"엔트자이언트의날개\" 30", _deadline.Token);
        await Waiting.Until(() => world.Pack.Any(item => item.Name.StartsWith("엔트자이언트의날개")), "날개가 오지 않았습니다.", _deadline.Token);

        await Waiting.WalkUntil(world, Direction.North, () => world.State?.Map.Id == WaitingRoom, _deadline.Token, attempts: 80);
        // 5.99 는 4,9 에 내려 준다. 늑대 여섯이 방 안 아무 데나 서므로(mob_spawn3) 4,9 에 늑대가 서 있으면 서버가 가장 가까운 빈칸에
        // 내려 준다(Area 의 빈칸 찾기, 세 칸 안) — 2026-09-19 부터 누구도 남이 선 칸에 겹쳐 서지 않는다.
        await Waiting.Until(() => world.State is { } state && state.Map.Id == WaitingRoom
                && Math.Max(Math.Abs(state.Where.X - 4), Math.Abs(state.Where.Y - 9)) <= 3,
            $"오솔길 끝 9,0 에서 대기실 4,9 로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "대기실에 늑대가 없습니다.", _deadline.Token);

        // 서버가 센 수를 문구로 알려 준다 — 스크립트가 세운 6마리가 다 서 있어야 한다.
        Direction intoDoor = await BesideDoor(world);
        await Waiting.WalkUntil(world, intoDoor, () => world.Said.Contains("남아있는 몬스터수"), _deadline.Token, attempts: 10);
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
        await Waiting.Until(() => (world.Vitals?.Experience ?? 0) - before >= 200000,
            $"클리어 경험치 20만이 들어오지 않았습니다: {before} → {world.Vitals?.Experience}", _deadline.Token, TimeSpan.FromSeconds(5));
    }

    /// <summary>5존 26,1 에서 들어가 오솔길 사본 9,39 에 선다. 5존 사냥터 괴물은 치운다(나르콜리에 잠들면 대화 대답이 무시된다).</summary>
    private async Task<(IsolatedHadesServer Server, WorldSession Session, WorldClient World)> EnterTrail(
        string who, bool gameMaster, (string Name, int Stacks)[]? pack = null, long gold = 0)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (FifthZone, 26, 1));
        _server = server;
        if (gameMaster)
        {
            Waiting.MakeGameMaster(server, who);
        }

        // 5존 사냥터 괴물은 치운다 — 엔트자이언트의 나르콜리에 잠들면 서버가 대화 대답을 받지 않는다(자는 동안은 아무것도 못 한다).
        foreach (string hunter in Directory.GetFiles(Path.Combine(server.ContentLocation, "templates", "monsters", "5.99"), "*@포테의숲5존.json"))
        {
            File.Delete(hunter);
        }

        // 던전 괴물 체력만 1 로 — 마릿수(대기실 늑대 6 · 보스방 늑대 8 · 자이언트맨티스 1)와 흐름은 그대로 두고 잡는 시간만 줄인다.
        string entry = Path.Combine(server.ContentLocation, "scripts", "Pack599", "Npcs", "포테의숲오솔길입장.cs");
        File.WriteAllText(entry, File.ReadAllText(entry).Replace("(V)4500L", "(V)1L").Replace("(V)15000L", "(V)1L"));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);

        // 입장은 레벨 21~52. 대기실 늑대에 쓰러지지 않게 체력을 넉넉히.
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 30;
        character["_MaximumHp"] = 20000;
        character["CurrentHp"] = 20000;
        character["GoldPoints"] = gold;

        // 운영자가 아니면 /give 를 못 하므로 저장 파일에 넣어 둔다 — 서버가 불러올 때 이름으로 템플릿을 채운다.
        int slot = 1;
        foreach ((string item, int stacks) in pack ?? [])
        {
            character["Inventory"]!["Items"]![slot.ToString()] = new JsonObject
            {
                ["Template"] = new JsonObject { ["Name"] = item },
                ["Slot"] = slot,
                ["Stacks"] = stacks,
                ["Durability"] = 100,
            };
            slot++;
        }

        File.WriteAllText(saved, character.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

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

        return (server, session, world);
    }

    /// <summary>
    /// 오솔길의 혼수 함정 — 5.99 `Dungeon__Script` 가 오솔길에서 움직인 사람을 1초마다 1/5 로 혼수에 빠뜨린다(`set_coma` · `set_state 1,1` ·
    /// `coma_delay 12`). 엔트자이언트의날개가 없으면 그대로 빠지고, 12초 안에 누가 살리지 않으면 죽는다(`__COMA_END__`). 5.99 서버 역어셈블로
    /// 확인한 혼수는 하데스 빈사(아이콘 89 · 그림 24)와 같아 그것을 건다. 운영자는 빈사에 걸리지 않으므로 보통 캐릭터로.
    /// 죽으면 5.99·Novaonline `__SCRIPT_DEAD__` 대로 뮤레칸의방 10,9 로 가고, 뮤레칸(12,5)을 누르면 살아나 레벨에 맞는 마을로 간다
    /// (30레벨 → 수오미마을 39,19). 죽음 벌칙은 꺼 두었으니 소지품·골드는 그대로다.
    /// </summary>
    [Fact]
    public async Task Walking_the_trail_without_a_wing_drops_you_into_a_coma_that_ends_in_death_at_murekan_who_revives_you_in_a_village()
    {
        (IsolatedHadesServer server, WorldSession session, WorldClient world) =
            await EnterTrail("potecoma", gameMaster: false, pack: [("팜팻의정수", 3)], gold: 1000);
        using IsolatedHadesServer ownedServer = server;
        using WorldSession ownedSession = session;

        await Waiting.Until(() => world.Pack.Any(item => item.Name.StartsWith("팜팻의정수")), "넣어 둔 팜팻의정수가 가방에 없습니다.", _deadline.Token);
        string[] before = Belongings(world);

        DateTime comaAt = await WalkUntilComa(world, server);

        // 혼수 그림 24 가 내 몸 위에 그려지라고 온다(0x29 — 첫 그림이 맞는 쪽, 맞는 쪽이 나).
        List<Effect> flashes = [];
        await Waiting.Until(() =>
        {
            while (world.TakeEffect(out Effect? flash))
                flashes.Add(flash);
            return flashes.Any(flash => flash.TargetAnimation == 24 && flash.Target == world.Serial && flash.At is null);
        }, $"혼수 그림 24 가 내 몸에 오지 않았습니다. 온 것: {string.Join(", ", flashes)}", _deadline.Token, TimeSpan.FromSeconds(5));

        const int MurekansRoom = 20138;
        await Waiting.Until(() => world.State?.Map.Id == MurekansRoom,
            $"혼수가 끝나도 뮤레칸의방으로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}", _deadline.Token, TimeSpan.FromSeconds(30));
        double comaSeconds = (DateTime.UtcNow - comaAt).TotalSeconds;
        Assert.True(comaSeconds < 17, $"혼수 12초가 {comaSeconds:0.0}초 걸렸습니다.");
        await Waiting.Until(() => world.State?.Where == new Tile(10, 9), $"뮤레칸의방 10,9 에 서지 않았습니다: {world.State}", _deadline.Token);

        Assert.Equal(before, Belongings(world));
        Assert.Equal(1000, world.Vitals?.Gold);

        // 회귀 시험 — debuff_reeping.OnEnded 가 CastDeath() 보다 먼저 유령 깃발을 세우면, CastDeath() 가
        // 「아직 유령이 아닐 때」만 부르는 AislingToGhostForm()(체력 자연회복 타이머를 끄는 곳)이 불리지
        // 않아 유령인 채로 체력이 계속 찬다. 뮤레칸이 살리기 전까지(기본 RegenRate 21초를 한 번은 넘겨서)
        // 체력이 죽었을 때 값에서 오르지 않는지 본다.
        int deadHealth = world.Vitals?.Health ?? 0;
        int worstGhostHealth = deadHealth;
        DateTime regenWatch = DateTime.UtcNow + TimeSpan.FromSeconds(24);
        while (DateTime.UtcNow < regenWatch)
        {
            if (world.Vitals is { } vitals)
                worstGhostHealth = Math.Max(worstGhostHealth, vitals.Health);
            await Task.Delay(500, _deadline.Token);
        }

        Assert.True(worstGhostHealth <= deadHealth,
            $"유령인 동안 체력이 죽었을 때 값({deadHealth})을 넘어 {worstGhostHealth} 로 올랐습니다 — " +
            $"CastDeath() 가 AislingToGhostForm() 을 부르지 못해 자연회복 타이머가 안 꺼진 것입니다.");

        Creature murekan = null!;
        await Waiting.Until(() => (murekan = world.Creatures.FirstOrDefault(c => c.Where == new Tile(12, 5))!) is not null,
            "뮤레칸의방 12,5 에 뮤레칸이 없습니다.", _deadline.Token);
        await world.ClickAsync(murekan.Serial, _deadline.Token);

        const int SuomiVillage = 20355;
        int answered = 0;
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (world.State?.Map.Id != SuomiVillage && DateTime.UtcNow < giveUp)
        {
            if (world.TalkCount > answered && world.Talking is { } talk && talk.Options.FirstOrDefault(option => option.Text == "다음") is { } next)
            {
                answered = world.TalkCount;
                await world.AnswerAsync(murekan.Serial, next.Step, _deadline.Token);
            }

            await Task.Delay(100, _deadline.Token);
        }

        Assert.True(world.State is { } now && now.Map.Id == SuomiVillage && now.Where == new Tile(39, 19),
            $"뮤레칸이 살려 수오미마을 39,19 로 보내지 않았습니다. 마지막: {world.State} · 창: {world.Talking?.What} · 서버가 한 말: {world.Said}");

        // 살아났다 — 유령이면 하데스가 걷기 말고는 막는다. 저장 파일의 Flags 가 0(보통)이 되고 소지품은 그대로다.
        // 되살림은 곧바로 저장하지 않는다 — 이 시험 손님은 0x45·0x75 를 보내지 않아 SaveRate 저장이 안 돌고, SaveComponent 가
        // 45초마다 한 번 저장할 뿐이다. 위의 24초 지켜보기로 되살림이 그 저장 바로 뒤에 걸리면 기본 30초로는 다음 저장을 못 본다.
        await Waiting.Until(() => Flags(server, "potecoma") == 0, "살아났는데 저장된 캐릭터가 아직 유령입니다.", _deadline.Token,
            TimeSpan.FromSeconds(60));
        Assert.Equal(before, Belongings(world));
        Assert.Equal(1000, world.Vitals?.Gold);
    }

    /// <summary>
    /// 엔트자이언트의날개가 있으면 함정이 터져도 혼수 대신 날개 하나를 쓴다("엔트자이언트의 날개를 소모하였다.") — 5.99 Dungeon.txt.
    /// </summary>
    [Fact]
    public async Task Walking_the_trail_with_a_wing_spends_a_wing_instead_of_falling_into_a_coma()
    {
        (IsolatedHadesServer server, WorldSession session, WorldClient world) =
            await EnterTrail("potewing", gameMaster: false, pack: [("엔트자이언트의날개", 5)]);
        using IsolatedHadesServer ownedServer = server;
        using WorldSession ownedSession = session;

        await Waiting.Until(() => Wings(world) == 5, $"날개 5개가 가방에 없습니다: {string.Join(", ", Belongings(world))}", _deadline.Token);

        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        Direction way = Direction.North;
        bool dying = false;

        while (DateTime.UtcNow < giveUp && Wings(world) == 5 && !dying)
        {
            await world.WalkAsync(way, _deadline.Token);
            await Task.Delay(700, _deadline.Token);
            way = way == Direction.North ? Direction.South : Direction.North;
            dying = Dying.Any(line => world.Said.Contains(line));
        }

        Assert.False(dying, $"날개가 있는데 혼수에 빠졌습니다. 서버가 한 말: {world.Said}");
        await Waiting.Until(() => Wings(world) == 4, $"오솔길을 2분 걸었는데 날개를 쓰지 않았습니다. 남은 날개: {Wings(world)} · 서버가 한 말: {world.Said} · 서버 기록: {PackLog(server)}",
            _deadline.Token, TimeSpan.FromSeconds(5));
        Assert.Contains("엔트자이언트의 날개를 소모하였다.", world.Said);

        // 날개를 쓴 뒤 몇 초 더 서 있어도 혼수가 오지 않는다.
        await Task.Delay(3000, _deadline.Token);
        Assert.DoesNotContain(Dying, line => world.Said.Contains(line));
        Assert.Equal(Trail, world.State?.Map.Id);
    }

    /// <summary>하데스 빈사 문구(LoruleConfig ReapMessage) — 이 가운데 하나가 뜨면 혼수다.</summary>
    private static readonly string[] Dying =
        ["You are dying.", "You cannot move nor raise your arms.", "Barron is going to take your soul.", "All things eventually come to an end."];

    /// <summary>오르내리며 자리를 바꿔야 함정이 돈다. 혼수 문구가 처음 뜬 때를 돌려준다.</summary>
    private async Task<DateTime> WalkUntilComa(WorldClient world, IsolatedHadesServer server)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        Direction way = Direction.North;

        while (DateTime.UtcNow < giveUp && !Dying.Any(line => world.Said.Contains(line)))
        {
            await world.WalkAsync(way, _deadline.Token);
            await Task.Delay(700, _deadline.Token);
            way = way == Direction.North ? Direction.South : Direction.North;
        }

        Assert.True(Dying.Any(line => world.Said.Contains(line)), $"오솔길을 2분 걸었는데 혼수에 빠지지 않았습니다. 서버가 한 말: {world.Said} · 서버 기록: {PackLog(server)}");
        return DateTime.UtcNow;
    }

    private static string[] Belongings(WorldClient world) => [.. world.Pack.Select(item => $"{item.Slot}:{item.Name}x{item.Stacks}")];

    private static int Wings(WorldClient world) =>
        world.Pack.Where(item => item.Name.StartsWith("엔트자이언트의날개")).Sum(item => Math.Max(1, item.Stacks));

    /// <summary>저장 파일의 AislingFlags(0 보통 · 1 유령). 서버가 쓰는 도중이면 -1.</summary>
    private static int Flags(IsolatedHadesServer server, string name)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(Path.Combine(server.ContentLocation, "aislings", $"{name}.json")))?["Flags"]?.GetValue<int>() ?? -1;
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return -1;
        }
    }

    /// <summary>
    /// 문(4,0)을 지난다. 마지막 괴물이 죽은 것이 서버에서 처리되기 전에 밟으면 한 번 거절되므로 문 옆으로 돌아가 다시 밟는다.
    /// 그 사이 남은 괴물이 보이면 잡는다.
    /// </summary>
    private async Task ThroughDoor(WorldClient world, int room, int next)
    {
        for (int attempt = 0; attempt < 8 && world.State?.Map.Id != next; attempt++)
        {
            Direction into = await BesideDoor(world);
            string before = world.Said;
            // 서버가 화면을 다시 보내는 동안은 걸음을 버리므로 될 때까지 걷는다.
            await Waiting.WalkUntil(world, into,
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

    /// <summary>
    /// 문(4,0) 옆 빈칸에 서서, 문으로 들어갈 방향을 돌려준다. 늑대가 문 앞 4,1 에 서 있을 수 있어 3,0 · 5,0 도 본다.
    /// </summary>
    private async Task<Direction> BesideDoor(WorldClient world)
    {
        (Tile Stand, Direction Into)[] ways = [(new Tile(4, 1), Direction.North), (new Tile(3, 0), Direction.East), (new Tile(5, 0), Direction.West)];

        foreach ((Tile stand, Direction into) in ways)
        {
            if (await StepTo(world, stand))
                return into;
        }

        Assert.Fail($"문 4,0 옆 어디에도 서지 못했습니다. 마지막: {world.State} · 서버가 한 말: {world.Said} · 보이는 것: {string.Join(", ", world.Creatures.Select(c => $"{c.Kind}:{c.Name}@{c.Where}"))}");
        return Direction.North;
    }

    /// <summary>
    /// 그 칸까지 걸어가 선다. 남이 선 칸에는 운영자도 못 들어가므로(2026-09-19) 보이는 괴물·NPC 칸을 비켜 길을 찾는다 — 운영자는 벽을
    /// 지나 걸으므로 맵 안이면 된다. 걸을 때마다 서버에 자리를 다시 물어 본다. 그 칸에 누가 서 있거나 길이 없으면 false.
    /// </summary>
    private async Task<bool> StepTo(WorldClient world, Tile goal)
    {
        int? map = world.State?.Map.Id;

        for (int step = 0; step < 40; step++)
        {
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);

            // 가는 사이 맵이 바뀌었으면(던전이 내보냄) 그 맵에서 계속 걷지 않는다.
            if (world.State is not { } me || me.Map.Id != map)
                return true;
            if (me.Where == goal)
                return true;
            if (FirstStep(world, me, goal) is not { } toward)
                return false;

            await world.WalkAsync(toward, _deadline.Token);
            await Task.Delay(300, _deadline.Token);
        }

        return false;
    }

    /// <summary>보이는 괴물·NPC 칸을 피해 맵 안에서 가장 짧은 길의 첫 걸음(너비 우선). 길이 없으면 null.</summary>
    private static Direction? FirstStep(WorldClient world, WorldEntry me, Tile goal)
    {
        int columns = me.Map.Columns, rows = me.Map.Rows;
        HashSet<Tile> taken = [.. world.Creatures.Select(c => c.Where)];
        if (taken.Contains(goal))
            return null;

        (int X, int Y, Direction Way)[] ways = [(0, -1, Direction.North), (1, 0, Direction.East), (0, 1, Direction.South), (-1, 0, Direction.West)];
        Dictionary<Tile, Direction> first = [];
        Queue<Tile> frontier = new([me.Where]);

        while (frontier.Count > 0)
        {
            Tile at = frontier.Dequeue();
            foreach ((int dx, int dy, Direction way) in ways)
            {
                Tile next = new(at.X + dx, at.Y + dy);
                if (next.X < 0 || next.Y < 0 || next.X >= columns || next.Y >= rows || next == me.Where || taken.Contains(next) || first.ContainsKey(next))
                    continue;

                first[next] = at == me.Where ? way : first[at];
                if (next == goal)
                    return first[next];
                frontier.Enqueue(next);
            }
        }

        return null;
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
