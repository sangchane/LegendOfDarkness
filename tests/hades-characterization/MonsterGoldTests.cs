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
/// 하데스가 싣고 있던 식은 <c>Random(Level * 500, Level * 1000)</c> 였고(<c>Formulas/monsterexp.cs</c>),
/// 하데스의 괴물 정의 568개는 **하나도 빠짐없이 <c>Level 1</c>** 이라 세상의 모든 괴물이 한 마리에
/// 500~999 전을 냈다. 레더튜닉이 300전이다. 5.99 팩은 같은 노비스 괴물에게 `골드 20 30` — 스무 전을
/// 셋에 하나꼴로 — 을 적어 두었다.
/// </para>
/// <para>
/// 그래서 정의에 적힌 값을 서버가 그대로 내는지를 본다. 확률은 이 시험이 100 으로 고정한다(아래
/// <see cref="StandOneAtTheDoor" />): 셋에 하나를 기다리면 몇 마리를 잡아야 하는지가 운에 달리고, 여기서
/// 보려는 것은 확률이 아니라 액수다.
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

    [Fact]
    public async Task A_kill_pays_the_gold_its_definition_states()
    {
        (WorldClient world, IsolatedHadesServer server) = await Enter();

        int stated = StatedGold(server);

        await Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "문 앞에 괴물이 서지 않았습니다.");

        // 들어서는 동안 보낸 것은 서버가 버린다.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        long before = Mine(world).Gold;

        await SwingUntil(world, enough: () => Mine(world).Gold > before);

        long paid = Mine(world).Gold - before;

        Assert.True(paid > 0,
            $"{MostSwings}번 휘둘렀는데 금화가 {before} 그대로입니다. {Said(world)}");

        // 금화는 부탁하지 않아도 들어온다(AUTO LOOT GOLD) — 그래서 지갑을 본다.
        Assert.Equal(stated, paid);
    }

    /// <summary>
    /// 정의가 적어 둔 액수. 여기 적지 않고 정의에서 읽는다 — 둘이 어긋날 수 없게.
    /// </summary>
    private static int StatedGold(IsolatedHadesServer server)
    {
        JsonNode only = TheOnlyDefinitionInTheRoom(server);

        int? gold = (int?)only["Gold"];

        Assert.True(gold is not null,
            "정의에 Gold 가 없습니다 — 그러면 서버는 레벨로 금화를 만듭니다(Level × 500~1000). " +
            "정의 568개가 모두 Level 1 이라 그 길은 어느 괴물이든 500~999 전입니다.");

        return gold.Value;
    }

    private static JsonNode TheOnlyDefinitionInTheRoom(IsolatedHadesServer server) =>
        Assert.Single(
            DefinitionsInTheRoom(server).Select(definition => definition.Template),
            template => ((int?)template["SpawnMax"] ?? 0) > 0);

    /// <summary>
    /// 존의 제 정의 하나를 문 앞칸에 세우고, 나머지는 세우지 않는다. 액수 말고는 아무것도 바꾸지 않는다 —
    /// <c>Gold</c> 는 정의가 적은 그대로 두고 <c>GoldChance</c> 만 100 으로 올린다. 30% 를 기다리면 몇
    /// 마리를 잡아야 하는가가 운에 달려 시험이 흔들린다. <see cref="CombatSmokeTests" /> 와 같은 모양이다.
    /// </summary>
    private static void StandOneAtTheDoor(IsolatedHadesServer server)
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

    private async Task<(WorldClient World, IsolatedHadesServer Server)> Enter()
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        _servers.Add(server);
        StandOneAtTheDoor(server);
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
