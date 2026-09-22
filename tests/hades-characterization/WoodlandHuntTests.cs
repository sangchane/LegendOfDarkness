using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Hunting has to pay. Spawning and taking damage are each proved elsewhere; what is unproved is the end of
/// the loop — that a woodland monster can actually be finished off, and that finishing it hands over the
/// experience and the gold its definition promises.
/// </summary>
/// <remarks>
/// <para>
/// Woodland is one ground that runs the whole way from novice to the level cap, so its zones are the ladder.
/// The entrance zone is the only one a fresh character survives: five definitions between 115 and 240 health
/// that hit for one to seven. The zones above it hold the same creatures grown — 1,150 health at zone
/// <c>20023</c>, 9,000 at the goblins — and a new character dies there before it can finish anything.
/// </para>
/// <para>
/// Experience is the signal that something died, not a health report reading zero. Experience arrives from
/// <c>Formulas/monsterexp.cs GenerateRewards</c>, which the server runs at the moment a monster is removed
/// and at no other time, so a rise in it cannot mean anything else. A health report, by contrast, is sent for
/// every blow that lands and the last one before a body disappears is not reliably zero.
/// </para>
/// <para>
/// Gold is a separate claim because it travels a different road, and not the road the code first suggests.
/// <c>GenerateGold</c> does lay it on the floor where the body stood, and picking a thing up is otherwise
/// something a player has to ask for by naming the exact tile. But gold never waits to be asked: every
/// player carries an <c>AUTO LOOT GOLD</c> game setting, it is on from the start, and
/// <c>ObjectComponent</c> hands the pile over the moment it comes into view. So a kill pays gold straight
/// into the purse, and this watches the purse rather than the floor.
/// </para>
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class WoodlandHuntTests : IDisposable
{
    private const string Hunter = "woodhunt";

    private const string Looter = "woodloot";

    private const string Watcher = "woodwatch";

    /// <summary>
    /// How many different monsters have to come into sight before a ground counts as a hunting ground
    /// rather than an empty field.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Counted as distinct bodies met over the two minutes rather than bodies standing there at one
    /// instant, because they wander and cluster and an instant is a thin sample.
    /// </para>
    /// <para>
    /// Two, because that is what the measurements support. The spawner as it was put <b>nothing</b> in
    /// sight in a minute at the middle of this zone; with the pacing fixed the same spot yields two to
    /// four over two minutes. So this separates an empty ground from a stocked one, which is the claim —
    /// it does not say the ground is busy, and the numbers would not support saying so.
    /// </para>
    /// </remarks>
    private const int Several = 2;

    /// <summary><c>LootQualifer.Table</c>·<c>Gold</c> — 정의가 적은 목록에서 뽑고, 골드도 함께 낸다.</summary>
    private const int LootTable = 4;

    private const int LootGold = 32;

    /// <summary>
    /// 우드랜드1-1 — 니에1(115)·뱀1(130)·완두콩1(130)·녹색말벌1(200)·브라운맨티스1(240), 전부 한 방에 1~7.
    /// <c>LootType 32</c> 은 <c>Gold</c> 하나뿐이라 이 존은 골드만 떨어뜨린다. 물건이 떨어지는 첫 존은
    /// 20023 이고 거기 괴물은 체력이 1,150 이라 1수준으로는 끝내지 못한다.
    /// </summary>
    private const int WoodlandOneOne = 20015;

    private static readonly Tile Start = new(2, 35);

    /// <summary>
    /// The middle of the sixty-by-sixty ground. Standing here puts the twelve tiles we are told about
    /// around the centre of where the spawner scatters things, which is the fairest place to ask whether
    /// the zone fills at all.
    /// </summary>
    private static readonly Tile Middle = new(30, 30);

    /// <summary>The tile the fixture monster stands on — the one the entrance faces.</summary>
    private static readonly Tile TargetTile = new(2, 34);

    /// <summary><c>SpawnQualifer.Defined</c> — stand where the template says, not on a random tile.</summary>
    private const int SpawnDefined = 4;

    /// <summary><c>PathQualifer.Fixed</c> — 서 있는 자리에서 움직이지 않는다.</summary>
    private const int PathFixed = 2;

    /// <summary><c>MoodQualifer.Idle</c> — 먼저 덤비지 않는다.</summary>
    private const int MoodIdle = 1;

    /// <summary>
    /// Enough swings to finish the largest of the five definitions several times over — a fresh character's
    /// Assail takes off something like twenty a blow — while still giving up inside a few minutes if none
    /// land.
    /// </summary>
    /// <remarks>
    /// 120 이었다. 금화가 셋에 하나꼴로만 떨어지게 된 뒤로(5.99 `골드 50 30`) 한 마리로는 모자라
    /// 올렸다 — 스무 마리쯤 잡으면 빈손으로 끝날 확률이 천에 하나 아래로 내려간다.
    /// </remarks>
    private const int MostSwings = 240;

    /// <summary>
    /// Steps spent looking for something to fight. At roughly a step every 150ms this is about a minute of
    /// walking, which crosses a sixty-tile ground several times over.
    /// </summary>
    private const int RoamingSteps = 400;

    /// <summary>
    /// Half-second looks the density test spends standing still — two minutes. A minute was enough on a
    /// quiet machine and not enough beside seventy other tests, each of which is a game server of its
    /// own: the spawner sweeps on the server's clock, so a server starved of processor stands fewer
    /// things up in the same wall-clock minute.
    /// </summary>
    private const int StandingTicks = 240;

    /// <summary>
    /// Half-second looks before the fight goes looking. Short, because walking finds something sooner
    /// than waiting does and the fight has a whole zone to cross afterwards.
    /// </summary>
    private const int GlanceTicks = 40;

    /// <summary>
    /// 1서클 물건. 하데스가 싣는 영문 표의 목걸이 넷이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 처음에는 같은 물건의 한글 쪽(<c>대지/바다/바람/화염의목걸이</c>, <c>Group: 팩드롭</c>, 그림번호가
    /// 197·199·198 로 같다)을 실었는데 <b>여섯 마리를 잡아도 하나도 떨어지지 않았다.</b> 자리 문제가
    /// 아니었다 — 표적을 제자리에 못 박고 죽은 칸과 그 둘레를 집어도 마찬가지였고, 이름만 영문으로
    /// 바꾸자 같은 자리에서 곧바로 들어왔다.
    /// </para>
    /// <para>
    /// 두 정의가 다른 곳: 한글 쪽은 <c>DropRate</c> 가 없고(0 이 된다) <c>Flags</c> 가 65
    /// (<c>Equipable|Repairable</c>) 뿐이며 <c>DisplayImage</c> 가 0 이다. 영문 쪽은 <c>DropRate 0.5</c> ·
    /// <c>Flags 5241</c>(여기에 <c>Dropable</c>·<c>Sellable</c>·<c>Bankable</c>·<c>Upgradeable</c> 이 있다) ·
    /// <c>DisplayImage 32965</c> 다. 어느 칸이 막는지는 아직 못 짚었다 — 사람이 물건을 버릴 때 보는
    /// <c>Dropable</c> 검사(<c>GameServerHandlers</c>)는 괴물이 떨어뜨리는 길과 상관이 없다.
    /// </para>
    /// <para>
    /// <b>드롭 기능이 고장난 것은 아니다.</b> 팩드롭 쪽은 한글 이름을 붙이는 작업이 아직 진행 중이라
    /// 정의가 덜 채워진 상태였다(2026-09-15 확인). 여기서 영문을 쓰는 것은 그 편이 맞기 때문이기도
    /// 하다 — 아이템은 하데스 표를 기준으로 가기로 되어 있다.
    /// </para>
    /// </remarks>
    private static readonly string[] FirstCircleDrops =
        ["Earth Necklace", "Sea Necklace", "Wind Necklace", "Fire Necklace"];

    /// <summary>
    /// How many bodies before giving up. One kill is not enough to conclude anything: the table is asked
    /// for <c>Random.Next(LootTableStackSize)</c> things and <c>LootTableStackSize</c> is three, so one
    /// kill in three is asked for nothing at all and drops nothing however full its table is.
    /// </summary>
    private const int MostKills = 6;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(9));
    private readonly List<IsolatedHadesServer> _servers = [];

    /// <summary>
    /// What the fixture monster's own definition says a kill is worth. Read from that definition rather
    /// than written down here, so the two cannot drift apart.
    /// </summary>
    private long _promised;

    /// <summary>
    /// 이 존의 정의가 적어 둔 금화 가운데 가장 큰 것. 어느 놈이 죽었는지 고를 수 없으므로 위쪽만 조인다.
    /// </summary>
    private long _mostGold;

    /// <summary>How many of the zone's definitions were given something to drop.</summary>
    private int _patched;

    /// <summary>
    /// The tile we last swung into. Gold comes to us on its own; a thing on the ground does not, and the
    /// ask for it names one exact tile — <c>Format07Handler</c> matches <c>XPos</c> and <c>YPos</c> and
    /// only then checks we are within <c>ClickLootDistance</c> — so the fight has to remember where the
    /// body fell.
    /// </summary>
    private Tile? _struck;

    public void Dispose()
    {
        _deadline.Dispose();

        foreach (IsolatedHadesServer server in _servers)
        {
            server.Dispose();
        }
    }

    [Fact]
    public async Task A_woodland_kill_pays_the_experience_and_the_gold_its_definition_promises()
    {
        WorldClient world = await Enter(Hunter);

        await AnyMonster(world);

        // The server drops anything sent while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        Vitals before = Mine(world);

        // 금화까지 기다린다. 정의가 적은 확률이 30% 라(5.99 `골드 50 30`) 한 마리로는 열에 일곱이
        // 빈손이다 — 경험치에서 멈추면 시험이 동전 던지기가 된다.
        await SwingUntil(world, enough: () => Mine(world).Gold > before.Gold);

        Vitals after = Mine(world);
        long paid = after.Experience - before.Experience;

        Assert.True(
            paid > 0,
            $"{MostSwings}번 휘둘렀는데 경험치가 {before.Experience} 그대로입니다 — 우드랜드1-1 에서 " +
            $"괴물을 한 마리도 끝내지 못했습니다. 체력 보고: {Reports(world)}");

        // 존에 다섯 종이 서고 값이 3,300~4,200 이라 어느 놈이 죽었는지 우리가 고를 수 없다. 그래서
        // 가장 싼 정의를 하한으로 잡는다. 위쪽을 조이지 않는 까닭이 하나 더 있다 — 천 단위로 잘라
        // 넘기면서 남는 것을 천으로 올려 주므로 4,200 짜리가 5,000 을 준다.
        Assert.True(
            paid >= _promised,
            $"이 존에서 가장 싼 정의가 경험치 {_promised} 를 약속하는데 {paid} 만 들어왔습니다.");

        long coins = after.Gold - before.Gold;

        Assert.True(
            coins > 0,
            $"괴물을 잡아 경험치 {paid} 를 받았는데 골드가 {before.Gold} 그대로입니다 — " +
            $"LootType 32 는 Gold 한 가지이므로 이 존이 내놓는 것은 골드뿐입니다. " +
            $"서버가 마지막으로 한 말: \"{world.Said}\" ({world.SaidCount}번).");

        // 한 무더기가 들어오는 순간 멈추므로 들어온 것은 정의가 적은 한 마리 몫이다. 이 존이 적는 것은
        // 20전과 50전 — 레벨 식(Level × 500~1000)으로 되돌아가면 500 이상이 들어와 여기서 걸린다.
        Assert.True(
            coins <= _mostGold,
            $"이 존이 적어 둔 가장 큰 금화가 {_mostGold} 전인데 한 마리에 {coins} 전이 들어왔습니다 — " +
            $"정의를 안 읽고 레벨로 만든 값입니다.");
    }

    /// <summary>
    /// A ground this size has to have something on it within the time somebody will stand there waiting.
    /// </summary>
    /// <remarks>
    /// Nothing else measures this. The other two go and find a fight and are content with one monster, so
    /// they pass on a zone that holds a single creature in an hour. What a hunting ground owes a player is
    /// that walking into it puts things in sight, and the spawner's pacing is what decides that: each
    /// definition stands one up every <c>SpawnRate</c> seconds, and Woodland 1-1 asks for fifty while being
    /// nine times the area of the rooms that number was written for.
    /// </remarks>
    [Fact]
    public async Task The_zone_puts_several_monsters_in_sight_of_someone_standing_in_it()
    {
        WorldClient world = await Enter(Watcher);

        HashSet<uint> met = [];

        for (int tick = 0; tick < StandingTicks && met.Count < Several; tick++)
        {
            foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
            {
                met.Add(mob.Serial);
            }

            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        Assert.True(
            met.Count >= Several,
            $"우드랜드1-1 한가운데에 {StandingTicks / 2}초를 서 있는 동안 시야에 들어온 괴물이 " +
            $"{met.Count}마리입니다 ({Several}마리를 기대). 이 존은 60x60=3,600칸이고 사람이 보는 것은 " +
            $"열두 칸 안입니다 — 정의 다섯이 각각 SpawnRate 50초에 하나씩 세우던 때에는 그 안이 비어 " +
            $"있었습니다.");
    }

    [Fact]
    public async Task A_first_circle_monster_hands_over_a_first_circle_item()
    {
        WorldClient world = await Enter(Looter, FirstCircleDrops);

        await AnyMonster(world);
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        int kills = 0;

        for (; kills < MostKills && Carried(world) is null; kills++)
        {
            long before = Mine(world).Experience;

            await SwingUntil(world, enough: () => Mine(world).Experience > before);

            if (Mine(world).Experience == before)
            {
                break;
            }

            await PickUpAroundTheBody(world);
        }

        string? carried = Carried(world);

        Assert.True(
            carried is not null,
            $"1서클 물건 {string.Join(", ", FirstCircleDrops)} 를 이 존의 정의 {_patched}개에 싣고 " +
            $"{kills}마리를 잡았는데 소지품에 " +
            $"하나도 들어오지 않았습니다. 죽은 자리 {_struck}, 내 자리 {world.State?.Where}, 소지품 " +
            $"[{string.Join(", ", world.Pack.Select(i => i.Name))}]. " +
            $"서버가 보여 주는 것 [{string.Join(", ", world.Creatures.Select(c => $"{c.Kind}:{c.Name}@{c.Where}"))}]. " +
            $"서버가 마지막으로 한 말: \"{world.Said}\".");
    }

    /// <summary>The first of the carried things that reached the pack, if any did.</summary>
    private static string? Carried(WorldClient world) =>
        world.Pack.Select(item => item.Name)
            .FirstOrDefault(name => FirstCircleDrops.Any(
                drop => name.Contains(drop, StringComparison.Ordinal)));

    /// <summary>
    /// Asks for what is lying on and around the tile the body fell on, and for whatever else the server is
    /// showing nearby that we cannot walk into.
    /// </summary>
    /// <remarks>
    /// The ask names one exact tile — <c>Format07Handler</c> matches <c>XPos</c> and <c>YPos</c> and only
    /// then checks we are within <c>ClickLootDistance</c> — and a thing on the floor is announced to us the
    /// same way a monster is, as something we can walk through. So this asks about the body's tile and its
    /// neighbours, and then about every walk-through thing in sight, which is where a dropped object shows
    /// up when the body took a step as it fell.
    /// </remarks>
    private async Task PickUpAroundTheBody(WorldClient world)
    {
        Tile centre = _struck ?? world.State?.Where
            ?? throw new InvalidOperationException("휘두른 자리도 내 자리도 모릅니다.");

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                await world.PickUpAsync(new Tile(centre.X + dx, centre.Y + dy), _deadline.Token);
                await Task.Delay(60, _deadline.Token);
            }
        }

        await Task.Delay(400, _deadline.Token);
    }

    /// <summary>
    /// Walks at the nearest monster and swings only when one is standing in the tile we face. A step is also
    /// a turn, and a step into an occupied tile is refused while the turn stands — so walking into the thing
    /// is how we come to be facing it.
    /// </summary>
    private async Task SwingUntil(WorldClient world, Func<bool> enough)
    {
        for (int swings = 0; swings < MostSwings && !enough();)
        {
            if (Nearest(world) is not { } goal || world.State is not { } before)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(200, _deadline.Token);
                continue;
            }

            int dx = goal.X - before.Where.X, dy = goal.Y - before.Where.Y;
            Direction step = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            await world.WalkAsync(step, _deadline.Token);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(120, _deadline.Token);

            if (world.State is not { } after || !IsThere(world, Ahead(after.Where, step)))
            {
                continue;
            }

            _struck = Ahead(after.Where, step);
            await world.AttackAsync(_deadline.Token);
            swings++;

            // GlobalBaseSkillDelay 는 500ms 다. 그보다 빨리 휘두르면 서버가 그냥 버린다.
            await Task.Delay(600, _deadline.Token);
        }
    }

    private static Tile Ahead(Tile from, Direction facing) => facing switch
    {
        Direction.North => new Tile(from.X, from.Y - 1),
        Direction.South => new Tile(from.X, from.Y + 1),
        Direction.East => new Tile(from.X + 1, from.Y),
        _ => new Tile(from.X - 1, from.Y)
    };

    private static bool IsThere(WorldClient world, Tile tile) =>
        world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == tile);

    private static Tile? Nearest(WorldClient world)
    {
        if (world.State is not { } me)
        {
            return null;
        }

        return world.Creatures
            .Where(c => c.Kind == CreatureKind.Hostile)
            .OrderBy(c => Math.Abs(c.Where.X - me.Where.X) + Math.Abs(c.Where.Y - me.Where.Y))
            .Select(c => (Tile?)c.Where)
            .FirstOrDefault();
    }

    private static Vitals Mine(WorldClient world) =>
        world.Vitals ?? throw new InvalidOperationException("서버가 내 수치를 말하지 않았습니다 (0x08 을 못 읽었습니다).");

    private static string Reports(WorldClient world) =>
        string.Join(", ", world.Hurts.Select(hurt => $"{hurt.Serial}={hurt.Left}%"));

    /// <summary>
    /// Stands one of the zone's own monsters next to the entrance, keeping every number that this test is
    /// about and fixing only where and when it appears.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Left alone the zone is too big and too slow to test against. Each of the five definitions gets one
    /// attempt every fifty seconds, picks a tile at random out of thirty-six hundred, spends the attempt
    /// even when that tile is wall, and we are told only about the twelve tiles around us — so whether
    /// anything is in sight inside a minute is a coin toss, and a run that finds nothing proves nothing.
    /// </para>
    /// <para>
    /// So this copies 니에1 — the smallest of the five at 115 health — and changes <c>SpawnType</c> to
    /// <c>Defined</c> at the tile ahead of the entrance, with <c>SpawnRate</c> at one second. Health,
    /// experience, armour, damage and above all <c>LootType</c> are left exactly as the pack wrote them,
    /// because those are the claims being checked. The name is changed so the spawner counts this one
    /// separately from the 니에1 that goes on standing up around the zone on its own.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The least any of the zone's five definitions pays. Which of them walks into us is the spawner's
    /// business, so the claim has to hold for whichever it was.
    /// </summary>
    private (long Cheapest, long MostGold) CheapestKillInTheZone(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        JsonDocumentOptions lenient = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        long cheapest = long.MaxValue;
        long mostGold = 0;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (!text.Contains($"\"AreaID\": {WoodlandOneOne}", StringComparison.Ordinal))
            {
                continue;
            }

            JsonNode node = JsonNode.Parse(text, documentOptions: lenient)!;

            if ((long?)node["Exp"] is { } exp && exp < cheapest)
            {
                cheapest = exp;
            }

            if ((long?)node["Gold"] is { } gold && gold > mostGold)
            {
                mostGold = gold;
            }
        }

        Assert.NotEqual(long.MaxValue, cheapest);
        Assert.True(mostGold > 0,
            "이 존의 정의에 Gold 가 없습니다 — 그러면 서버는 레벨로 금화를 만듭니다(Level × 500~1000).");

        return (cheapest, mostGold);
    }

    /// <summary>
    /// Gives every monster in the zone something of its own circle to drop, and stands one of them next to
    /// where we come in so the fight starts at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every one of the zone's five definitions is <c>LootType 32</c> — <c>Gold</c> and nothing else — so
    /// left alone the ground never puts an object down and there is no drop to watch. Turning
    /// <c>Table</c> on (36 = <c>Table|Gold</c>) and naming what to drop is exactly the shape the zones that
    /// do drop things already use, so nothing here is a mechanism this test invented.
    /// </para>
    /// <para>
    /// All five are given the same list rather than one of them, because the zone fills: whichever monster
    /// walks into us first is the one we fight, and marking a single definition would make the test pass or
    /// fail on which one the spawner happened to stand up nearest.
    /// </para>
    /// </remarks>
    private void GiveTheZoneSomethingToDrop(IsolatedHadesServer server, string[] carrying)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        JsonDocumentOptions lenient = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };
        JsonSerializerOptions indented = new() { WriteIndented = true };

        JsonNode Carrying(JsonNode node)
        {
            // 이 존의 제 몬스터들은 세우지 않는다. 젠이 고쳐진 뒤로 우드랜드1-1 은 스스로 차고, 그러면
            // 여기서 마주치는 것이 우리가 물건을 실어 둔 놈인지 아닌지 알 수 없다 — 물건이 안 나왔을 때
            // 그것이 드롭이 고장난 것인지 엉뚱한 놈을 잡은 것인지 갈리지 않는다.
            node["SpawnMax"] = 0;
            node["LootType"] = LootTable | LootGold;
            node["Drops"] = new JsonObject
            {
                ["$values"] = new JsonArray([.. carrying.Select(name => JsonValue.Create(name))]),
            };
            return node;
        }

        JsonNode? entrance = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (!text.Contains($"\"AreaID\": {WoodlandOneOne}", StringComparison.Ordinal))
            {
                continue;
            }

            JsonNode node = Carrying(JsonNode.Parse(text, documentOptions: lenient)!);
            File.WriteAllText(path, node.ToJsonString(indented));
            _patched++;

            entrance ??= JsonNode.Parse(node.ToJsonString(), documentOptions: lenient);
        }

        Assert.NotNull(entrance);

        // 하나를 입구 앞칸에 세워 둔다. 존이 스스로 차기를 기다리면 1분이 더 드는데, 여기서 보려는 것은
        // 젠이 아니라 떨어진 물건이 소지품까지 오는가다.
        entrance["Name"] = "우드랜드드롭시험";
        entrance["SpawnType"] = SpawnDefined;
        entrance["SpawnRate"] = 1;
        entrance["SpawnMax"] = 1;
        entrance["DefinedX"] = TargetTile.X;
        entrance["DefinedY"] = TargetTile.Y;
        entrance["Grow"] = false;

        // 제자리에 세우고 쫓아오지 않게 한다. 돌아다니면 죽는 자리가 매번 달라지고, 물건은 골드와
        // 달리 자동으로 들어오지 않아 **그 칸을 정확히 집어야** 한다 — 자리가 흔들리면 못 줍는다.
        entrance["PathQualifer"] = PathFixed;
        entrance["MoodType"] = MoodIdle;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(
            Path.Combine(testFolder, "woodland-drop-target.json"),
            entrance.ToJsonString(indented));
    }

    private async Task<WorldClient> Enter(string who, string[]? carrying = null)
    {
        Tile from = carrying is null ? Middle : Start;

        IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, from.X, from.Y));
        _servers.Add(server);
        if (carrying is null)
        {
            // 표적을 심지 않는다. 우드랜드1-1 이 스스로 채워지는지가 이 시험이 볼 것의 하나다.
            (_promised, _mostGold) = CheapestKillInTheZone(server);
        }
        else
        {
            GiveTheZoneSomethingToDrop(server, carrying);
        }
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is not null, "세계에 들어가지 못했습니다.");
        return world;
    }

    /// <summary>
    /// Walks the zone until something hostile comes into sight, which on a ground this size means walking
    /// rather than waiting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// We are told about creatures within <c>WithinRangeProximity</c>, twelve tiles, and Woodland 1-1 is
    /// sixty by sixty — so standing at the entrance leaves nine tenths of the ground unseen. The spawner
    /// makes it worse: each of the five definitions gets one attempt every fifty seconds
    /// (<c>SpawnRate</c>), spends the attempt whether or not the tile it picked turns out to be wall, and
    /// scatters what does stand up across the whole map. Waiting at the entrance is waiting for one of ten
    /// monsters to wander into a tenth of the map.
    /// </para>
    /// <para>
    /// This is also what a player does, which is the point — the zone is not broken, it is large.
    /// </para>
    /// </remarks>
    private async Task AnyMonster(WorldClient world)
    {
        // 먼저 걷지 않고 잠깐 기다린다 — 서 있는 자리로 걸어오는 놈이 있으면 그것으로 족하다.
        // 길게 기다리는 것은 밀도를 재는 시험의 일이고, 여기서는 걸어 나가는 편이 빠르다.
        for (int tick = 0; tick < GlanceTicks; tick++)
        {
            if (world.Creatures.Any(c => c.Kind == CreatureKind.Hostile))
            {
                return;
            }

            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        Direction heading = Direction.East;
        int turns = 0;

        for (int step = 0; step < RoamingSteps; step++)
        {
            if (world.Creatures.Any(c => c.Kind == CreatureKind.Hostile))
            {
                return;
            }

            Tile? was = world.State?.Where;

            await world.WalkAsync(heading, _deadline.Token);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(150, _deadline.Token);

            // 벽에 부딪히면 자리가 그대로다. 그러면 방향을 돌려 다른 쪽을 훑는다.
            if (world.State?.Where is { } now && now == was)
            {
                heading = (Direction)(((int)heading + 1) % 4);
                turns++;
            }
            else if (step % 15 == 14)
            {
                // 한 방향으로만 가면 맵 가장자리를 따라 돈다. 가끔 꺾어 안쪽을 지난다.
                heading = (Direction)(((int)heading + 1) % 4);
            }
        }

        throw new TimeoutException(
            $"우드랜드1-1 에서 {GlanceTicks / 2}초를 기다린 뒤 {RoamingSteps}걸음을 돌았는데" +
            $"(벽에 막혀 방향을 {turns}번 꺾음) 괴물이 시야에 한 마리도 들어오지 않았습니다. " +
            $"지금 자리 {world.State?.Where}, 보이는 것 {world.Creatures.Count}개 " +
            $"[{string.Join(", ", world.Creatures.Select(c => $"{c.Kind}@{c.Where}"))}].");
    }

    private async Task Until(Func<bool> wanted, string complaint, int seconds = 100, WorldClient? asking = null)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);

        while (DateTime.UtcNow < giveUp)
        {
            if (wanted())
            {
                return;
            }

            if (asking is not null)
            {
                await asking.RefreshAsync(_deadline.Token);
            }

            await Task.Delay(300, _deadline.Token);
        }

        throw new TimeoutException(complaint);
    }
}
