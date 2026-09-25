using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사냥이 얼마를 버는가. 무엇이 떨어지는가는 <see cref="WoodlandHuntTests" /> 가 보고, 여기서 보는 것은
/// **액수**다 — 한 마리가 옷 한 벌 값을 내놓으면 상점도 드롭도 아무 뜻이 없어진다.
/// </summary>
/// <remarks>
/// <para>
/// <b>2026-09-24, 두 번째 결정 — 골드는 경험치에 비례한다.</b> 원작에도 하데스에도 "몬스터 레벨"이라는
/// 값이 없어(서버팩 3개·원작 아카이브·참고저장소 16개를 다 뒤져 확인) 레벨 대신 경험치를 쓴다 —
/// <c>Formulas/monsterexp.cs</c> 의 <c>GoldPerExp</c>(0.02) · <c>GoldVariance</c>(±20%). 노비스
/// 괴물 11마리의 경험치(1,068~1,849)와 지금 금화(20~30)에서 역산한 값이다.
/// </para>
/// <para>
/// 그래서 여기서는 (1) 금화가 경험치×0.02 의 ±20% 안에 드는지, (2) 확률·골드플래그가 없어도 항상
/// 나오는지, (3) 경험치가 큰 괴물이 더 많이 주는지, (4) **경험치 지급 자체는 이 작업으로 안 바뀌었는지**
/// (서버가 보내는 "경험치가 N 올랐습니다" 메시지의 N이 정의값과 정확히 같은지)를 본다.
/// </para>
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class MonsterGoldTests : IDisposable
{
    /// <summary>
    /// 우드랜드1-1 — 1수준이 살아남는 유일한 존이고, 다섯 정의가 <c>LootType 32</c>(<c>Gold</c> 하나뿐)라
    /// 금화 말고는 아무것도 떨어지지 않는다. <see cref="CombatSmokeTests" /> 와 같은 방·같은 문 앞칸이다.
    /// </summary>
    private const int MonsterRoom = 20015;

    private static readonly Tile Start = new(2, 35);

    private static readonly Tile TargetTile = new(2, 34);

    /// <summary><c>SpawnQualifer.Defined</c> — 정의가 적은 자리에 선다.</summary>
    private const int SpawnDefined = 4;

    /// <summary><c>PathQualifer.Fixed</c> — 서 있는 자리에서 움직이지 않는다.</summary>
    private const int PathFixed = 2;

    /// <summary><c>MoodQualifer.Idle</c> — 먼저 덤비지 않는다.</summary>
    private const int MoodIdle = 1;

    /// <summary>한 마리를 여러 번 끝내고도 남을 만큼. 115 체력에 한 방이 스물 남짓이다.</summary>
    private const int MostSwings = 120;

    /// <summary>
    /// 다음 한 마리까지 몇 초. 한 마리를 끝내고 지갑을 보는 데 십몇 초면 되므로, 그 사이에 둘째가
    /// 서지 않을 만큼만 길면 된다.
    /// </summary>
    private const int OneAtATime = 60;

    private const string Name = "goldkill";

    /// <summary>
    /// <c>LootQualifer.None</c>(256) — Random·Table·Gold 어느 플래그도 없는 값. 사슴 등은 실제로는
    /// Random(2)만 켜져 있었지만, 여기서는 빈 <c>Drops</c> 목록에서 <c>DetermineRandomDrop</c> 이
    /// 인덱스 예외를 던지지 않도록 아무 갈래도 없는 값으로 "골드 플래그 없음"만 따로 본다.
    /// </summary>
    private const int LootTypeWithoutGoldFlag = 256;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(9));
    private readonly List<IsolatedHadesServer> _servers = [];

    public void Dispose()
    {
        _deadline.Dispose();

        foreach (IsolatedHadesServer server in _servers)
        {
            server.Dispose();
        }
    }

    /// <summary><c>Formulas/monsterexp.cs</c> 의 <c>GoldPerExp</c> 을 그대로 되풀이한다.</summary>
    private const double GoldPerExp = 0.02;

    /// <summary><c>Formulas/monsterexp.cs</c> 의 <c>GoldVariance</c> 을 그대로 되풀이한다(±20%).</summary>
    private const double GoldVariance = 0.2;

    private static (long Low, long High) GoldRangeFor(int exp) =>
        ((long)Math.Floor(exp * GoldPerExp * (1 - GoldVariance)),
         (long)Math.Ceiling(exp * GoldPerExp * (1 + GoldVariance)));

    [Fact]
    public async Task A_kill_pays_gold_proportional_to_its_experience()
    {
        const int exp = 2000;

        (WorldClient world, IsolatedHadesServer server) = await Enter(target => target["Exp"] = exp);

        await Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "문 앞에 괴물이 서지 않았습니다.");

        // 들어서는 동안 보낸 것은 서버가 버린다.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        long before = Mine(world).Gold;

        await SwingUntil(world, enough: () => Mine(world).Gold > before);

        long paid = Mine(world).Gold - before;
        (long low, long high) = GoldRangeFor(exp);

        Assert.True(paid >= low && paid <= high,
            $"경험치 {exp}짜리가 금화 {paid}를 냈습니다 — {low}~{high} 안이어야 합니다. {Said(world)}");
    }

    /// <summary>
    /// 사용자 결정(2026-09-24) — 금화는 무조건 떨어진다. <c>GoldChance</c> 를 1(1%)로 낮춰도,
    /// <c>LootType</c> 에서 Gold 플래그(32)를 빼도(사슴 등이 그랬다) 여전히 준다.
    /// </summary>
    [Fact]
    public async Task A_kill_always_pays_gold_no_matter_the_chance_or_the_loot_flag()
    {
        const int exp = 5000;

        (WorldClient world, IsolatedHadesServer server) = await Enter(target =>
        {
            target["Exp"] = exp;
            target["GoldChance"] = 1;
            target["LootType"] = LootTypeWithoutGoldFlag; // 사슴 등이 실제로 쓰던 값 — Gold(32) 플래그가 없다.
        });

        await Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "문 앞에 괴물이 서지 않았습니다.");
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        long before = Mine(world).Gold;
        await SwingUntil(world, enough: () => Mine(world).Gold > before);

        long paid = Mine(world).Gold - before;
        (long low, long high) = GoldRangeFor(exp);

        Assert.True(paid >= low && paid <= high,
            $"확률 1%·골드플래그 없음인데 금화 {paid}를 냈습니다 — {low}~{high} 안이어야 합니다. {Said(world)}");
    }

    /// <summary>사용자 결정(2026-09-24) — 경험치가 큰 괴물이 금화도 더 많이 준다(레벨 최저금액 분기의 대체).</summary>
    [Fact]
    public async Task A_bigger_experience_kill_pays_more_gold()
    {
        (WorldClient smallWorld, _) = await Enter(target => target["Exp"] = 1000);
        await Until(() => smallWorld.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "문 앞에 괴물이 서지 않았습니다(작은 쪽).");
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        long beforeSmall = Mine(smallWorld).Gold;
        await SwingUntil(smallWorld, enough: () => Mine(smallWorld).Gold > beforeSmall);
        long paidSmall = Mine(smallWorld).Gold - beforeSmall;

        (WorldClient bigWorld, _) = await Enter(target => target["Exp"] = 40000);
        await Until(() => bigWorld.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "문 앞에 괴물이 서지 않았습니다(큰 쪽).");
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        long beforeBig = Mine(bigWorld).Gold;
        await SwingUntil(bigWorld, enough: () => Mine(bigWorld).Gold > beforeBig);
        long paidBig = Mine(bigWorld).Gold - beforeBig;

        Assert.True(paidBig > paidSmall,
            $"경험치 40,000짜리({paidBig}전)가 경험치 1,000짜리({paidSmall}전)보다 적게 냈습니다.");
    }

    /// <summary>
    /// 사용자 결정(2026-09-24) — 골드 식을 바꿔도 **경험치 지급 자체는 그대로**여야 한다. 서버가 보내는
    /// "경험치가 N 올랐습니다"(<c>GenerateExperience</c>, <c>Formulas/monsterexp.cs</c>)의 N이 정의에
    /// 적은 값과 정확히 같은지 본다 — 다르면 <c>GenerateGold</c> 를 고치다 <c>GenerateExperience</c> 를
    /// 건드린 것이다.
    /// </summary>
    [Fact]
    public async Task A_kill_still_grants_exactly_the_experience_its_definition_states()
    {
        const int exp = 3333;

        (WorldClient world, IsolatedHadesServer server) = await Enter(target => target["Exp"] = exp);

        await Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "문 앞에 괴물이 서지 않았습니다.");
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        long before = Mine(world).Gold;
        await SwingUntil(world, enough: () => Mine(world).Gold > before);

        Assert.True(TryReadExperienceGain(world, out int gained),
            $"\"경험치가 N 올랐습니다\" 알림을 못 받았습니다. {Said(world)}");
        Assert.Equal(exp, gained);
    }

    /// <summary>서버가 보낸 0x0A 줄 중 "경험치가 N 올랐습니다" 를 찾아 N을 읽는다. 없으면 false.</summary>
    private static bool TryReadExperienceGain(WorldClient world, out int amount)
    {
        while (world.TakeTold(out _, out string text))
        {
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(text, @"경험치가 (\d+) 올랐습니다");

            if (match.Success)
            {
                amount = int.Parse(match.Groups[1].Value);
                return true;
            }
        }

        amount = 0;
        return false;
    }

    /// <summary>
    /// 존의 제 정의 하나를 문 앞칸에 세우고, 나머지는 세우지 않는다. 액수 말고는 아무것도 바꾸지 않는다 —
    /// <c>Gold</c> 는 정의가 적은 그대로 두고 <c>GoldChance</c> 만 100 으로 올린다. 30% 를 기다리면 몇
    /// 마리를 잡아야 하는가가 운에 달려 시험이 흔들린다. <see cref="CombatSmokeTests" /> 와 같은 모양이다.
    /// </summary>
    private static void StandOneAtTheDoor(IsolatedHadesServer server, Action<JsonNode>? customize = null)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        (string Path, JsonNode Template)[] room = [.. DefinitionsInTheRoom(server)];

        Assert.NotEmpty(room);

        JsonNode target = room.MinBy(definition => (int?)definition.Template["MaximumHP"] ?? 0).Template.DeepClone();

        foreach ((string path, JsonNode template) in room)
        {
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        target["Name"] = "금화시험표적";
        target["SpawnType"] = SpawnDefined;

        // 한 마리만 잡히게 한다. 1초에 한 번 세우면 둘째가 같은 칸에서 죽고, `Money.Create` 는 같은 칸의
        // 금화를 **한 무더기로 합친다** — 두 마리 몫이 한 번에 들어와 액수를 잴 수 없다. 첫 놈은
        // SpawnRate 와 상관없이 첫 순회에 선다(NextAvailableSpawn 이 비어 있다).
        target["SpawnRate"] = OneAtATime;
        target["SpawnMax"] = 1;
        target["DefinedX"] = TargetTile.X;
        target["DefinedY"] = TargetTile.Y;
        target["PathQualifer"] = PathFixed;
        target["MoodType"] = MoodIdle;
        target["Grow"] = false;
        target["GoldChance"] = 100;
        customize?.Invoke(target);

        string testFolder = Path.Combine(server.ContentLocation, "templates", "monsters", "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "monster-gold-target.json"), target.ToJsonString(indented));
    }

    private static IEnumerable<(string Path, JsonNode Template)> DefinitionsInTheRoom(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");

        // 하데스가 싣는 정의 하나는 JSON 이 아니고(minions/minion.json 이 그림을 0x40C5 로 적는다)
        // 서버 제 손으로 쓴 것에는 꼬리 쉼표가 남는다. 그래서 글자로 먼저 고르고 너그럽게 읽는다.
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (text.Contains($"\"AreaID\": {MonsterRoom}", StringComparison.Ordinal))
            {
                yield return (path, JsonNode.Parse(text, documentOptions: lenient)!);
            }
        }
    }

    private async Task<(WorldClient World, IsolatedHadesServer Server)> Enter(Action<JsonNode>? customize = null)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        _servers.Add(server);
        StandOneAtTheDoor(server, customize);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State is not null && world.Vitals is not null, "세계에 들어가지 못했습니다.");
        return (world, server);
    }

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

            await world.AttackAsync(_deadline.Token);
            swings++;

            // GlobalBaseSkillDelay 가 500ms 다. 그보다 빨리 휘두르면 서버가 버린다.
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

    private static string Said(WorldClient world) =>
        $"서버가 마지막으로 한 말: \"{world.Said}\" ({world.SaidCount}번). " +
        $"체력 보고: {string.Join(", ", world.Hurts.Select(hurt => $"{hurt.Serial}={hurt.Left}%"))}";

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
