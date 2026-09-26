using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사용자(2026-09-26) — "몬스터 리젠이 안 되는 것 같다. 젠 되는 숫자는 좀 줄이고, 리젠을 몬스터가 끊기지 않게".
/// 한 맵에서 쉬지 않고 잡아도 곁에 괴물이 다시 서는지 본다. 젠은 <c>MonolithComponent</c> 한 곳이 정한다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class RespawnTests : IDisposable
{
    /// <summary>노비스평원A — 50x50, 정의 다섯. 첫 사냥터라 사람이 가장 오래 머문다.</summary>
    private const int NovicePlainA = 20393;

    private static readonly Tile Middle = new(25, 25);

    /// <summary>얼마나 오래 쉬지 않고 잡나.</summary>
    private static readonly TimeSpan Hunting = TimeSpan.FromSeconds(120);

    /// <summary>
    /// 곁(시야 안)에 괴물이 하나도 없는 채로 이보다 오래 있으면 "끊겼다". 걸어서 다가가 한 번 치는 데 몇 초가 들므로
    /// 그보다 넉넉히 준다.
    /// </summary>
    private static readonly TimeSpan LongestEmpty = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));
    private readonly ITestOutputHelper _output;

    public RespawnTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// 가운데에서 가장 가까운 괴물을 2분 동안 쉬지 않고 잡는다(가장자리로 끌려가거나 곁이 비면 운영자 순간이동으로 가운데로). 이 맵 정의의 체력만 1 로 낮춰(젠 값 — 마릿수·간격 — 은
    /// 그대로) 한 방에 끝나게 한다 — 잡는 속도는 걸어가는 데 드는 시간이 정하고, 사람이 잡는 것보다 빠르다. 그래도 곁이 {LongestEmpty} 넘게 비지 않아야 하고, 잡은 수가 줄지 않아야 한다
    /// (뒤 1분에 잡은 수가 앞 1분의 절반 아래로 떨어지면 채워지는 것보다 빨리 비는 것이다).
    /// </summary>
    [Fact]
    public async Task Hunting_nonstop_on_the_novice_plain_never_leaves_the_hunter_alone_for_long()
    {
        const string name = "regenhunter";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlainA, Middle.X, Middle.Y));
        OneBlowEach(server);
        Waiting.MakeGameMaster(server, name);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == NovicePlainA && world.Vitals is not null,
            "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "노비스평원A 에 괴물이 서지 않았습니다.", _deadline.Token, TimeSpan.FromSeconds(60));

        Stopwatch clock = Stopwatch.StartNew();
        Stopwatch? empty = null;
        TimeSpan longest = TimeSpan.Zero;
        int firstHalf = 0, secondHalf = 0;
        long experience = world.Vitals!.Experience;

        while (clock.Elapsed < Hunting)
        {
            if (world.Vitals is { } now && now.Experience != experience)
            {
                experience = now.Experience;

                if (clock.Elapsed < Hunting / 2)
                {
                    firstHalf++;
                }
                else
                {
                    secondHalf++;
                }
            }

            Tile? goal = Nearest(world);

            if (goal is null)
            {
                if (empty is null)
                {
                    _output.WriteLine($"{clock.Elapsed.TotalSeconds:F0}초 빔: 나 {world.State?.Map.Id}@{world.State?.Where} 체력 {world.Vitals?.Health} · 보이는 괴물 {world.Creatures.Count(c => c.Kind == CreatureKind.Hostile)}");
                }

                empty ??= Stopwatch.StartNew();
                longest = empty.Elapsed > longest ? empty.Elapsed : longest;

                // 괴물이 없으면 가운데로 돌아가 기다린다(운영자 순간이동 — 걸어가다 벽에 막히면 그 막힘을 "빔" 으로 잴 것이다).
                if (world.State is { } idle && idle.Where != Middle)
                {
                    await world.SayAsync($"/tp \"노비스평원A\" {Middle.X} {Middle.Y}", _deadline.Token);
                }

                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(300, _deadline.Token);
                continue;
            }

            empty = null;

            if (world.State is not { } me)
            {
                await Task.Delay(100, _deadline.Token);
                continue;
            }

            // 쫓다가 가장자리로 끌려가면(워프가 있다) 가운데로 되돌린다.
            if (me.Where.X is < 8 or > 42 || me.Where.Y is < 8 or > 42)
            {
                await world.SayAsync($"/tp \"노비스평원A\" {Middle.X} {Middle.Y}", _deadline.Token);
                await Task.Delay(300, _deadline.Token);
                continue;
            }

            Direction step = Toward(me.Where, goal.Value);
            await world.WalkAsync(step, _deadline.Token);
            await Task.Delay(300, _deadline.Token);

            if (world.State is { } after && IsThere(world, Ahead(after.Where, step)))
            {
                await world.AttackAsync(_deadline.Token);
                await Task.Delay(600, _deadline.Token);
            }
        }

        _output.WriteLine($"잡은 수: 앞 1분 {firstHalf} · 뒤 1분 {secondHalf} · 곁이 가장 오래 빈 때 {longest.TotalSeconds:F1}초");

        Assert.True(firstHalf > 0, "앞 1분 동안 한 마리도 못 잡았습니다 — 사냥 자체가 안 됩니다.");
        Assert.True(longest <= LongestEmpty,
            $"곁에 괴물이 {longest.TotalSeconds:F1}초 동안 하나도 없었습니다({LongestEmpty.TotalSeconds}초 안에 다시 서야). " +
            $"잡은 수 앞 1분 {firstHalf} · 뒤 1분 {secondHalf}.");
        Assert.True(secondHalf * 2 >= firstHalf,
            $"잡는 속도가 떨어졌습니다 — 앞 1분 {firstHalf}마리, 뒤 1분 {secondHalf}마리(채워지는 것보다 빨리 빕니다).");
    }

    /// <summary>
    /// 잡은 자리가 몇 초 안에 다시 차나 — 노비스평원A 의 제 정의 하나(SpawnRate 30 그대로)를 코앞 한 칸에 세우고(정의 자리 ·
    /// 제자리 · 비선공 · 체력 1) 여섯 번 잡는다. 다시 서는 간격은 SpawnRate ÷ 넓이 배수(6) ÷ 3 = 1.7초에 순회 1초라
    /// {Refill}초 안이어야 한다. 3 으로 나누기 전(5초 + 1초)에는 여섯 번 중 한 번은 거의 늘 넘었다.
    /// </summary>
    [Fact]
    public async Task A_kill_on_the_novice_plain_is_replaced_within_a_few_seconds()
    {
        const string name = "regenwatch";
        Tile spot = new(Middle.X, Middle.Y - 1);

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlainA, Middle.X, Middle.Y));
        int rate = StandOneDefinitionAt(server, spot);
        Assert.Equal(30, rate);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == NovicePlainA, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);
        await world.TurnAsync(Direction.North, _deadline.Token);

        List<double> waits = [];

        for (int round = 0; round < 6; round++)
        {
            await Waiting.Until(() => IsThere(world, spot), $"{round + 1}번째: 코앞 {spot} 에 괴물이 서지 않았습니다.",
                _deadline.Token, TimeSpan.FromSeconds(30));

            // 그 칸이 빌 때까지 친다(두 마리가 겹쳐 섰을 수 있다).
            for (int swing = 0; swing < 20 && IsThere(world, spot); swing++)
            {
                await world.AttackAsync(_deadline.Token);
                await Task.Delay(600, _deadline.Token);
            }

            Assert.False(IsThere(world, spot), $"{round + 1}번째: 코앞 괴물을 끝내지 못했습니다.");

            Stopwatch gap = Stopwatch.StartNew();
            await Waiting.Until(() => IsThere(world, spot), $"{round + 1}번째: 잡은 뒤 30초가 지나도 다시 서지 않았습니다.",
                _deadline.Token, TimeSpan.FromSeconds(30));
            waits.Add(gap.Elapsed.TotalSeconds);
        }

        _output.WriteLine($"다시 서기까지: {string.Join(" · ", waits.Select(w => $"{w:F1}초"))}");

        Assert.True(waits.Max() <= Refill.TotalSeconds,
            $"잡은 자리가 {waits.Max():F1}초 만에야 다시 찼습니다({Refill.TotalSeconds}초 안이어야). 모두: {string.Join(", ", waits.Select(w => $"{w:F1}"))}");
    }

    /// <summary>한 마리가 다시 서기까지 기다려 줄 시간 — 간격 1.7초 + 순회 1초 + 화면에 알리는 틈.</summary>
    private static readonly TimeSpan Refill = TimeSpan.FromSeconds(4);

    /// <summary>
    /// 노비스평원A 정의를 모두 재우고(SpawnMax 0) 첫 정의 하나를 <paramref name="spot" /> 에 묶어 세운다. SpawnRate 는 정의 값
    /// 그대로 두고 돌려준다. SpawnMax 2 — 1 이면 희귀 단독개체로 보아 간격을 줄이지 않는다(<c>MonolithComponent</c>).
    /// </summary>
    private static int StandOneDefinitionAt(IsolatedHadesServer server, Tile spot)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex plain = new($"\\\"AreaID\\\"\\s*:\\s*{NovicePlainA}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonNode? model = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                     .Where(path => plain.IsMatch(File.ReadAllText(path))).Order(StringComparer.Ordinal))
        {
            JsonNode template = JsonNode.Parse(File.ReadAllText(path), documentOptions: options)!;
            model ??= JsonNode.Parse(template.ToJsonString());
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        Assert.NotNull(model);
        model!["Name"] = "리젠시험";
        model["BaseName"] = "리젠시험";
        model["SpawnMax"] = 2;
        model["SpawnType"] = 4;
        model["DefinedX"] = spot.X;
        model["DefinedY"] = spot.Y;
        model["MoodType"] = 1;
        model["PathQualifer"] = 2;
        model["MaximumHP"] = 1;
        model["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "respawn.json"), model.ToJsonString(indented));
        return (int?)model["SpawnRate"] ?? 0;
    }

    /// <summary>노비스평원A 정의의 체력만 1 로 — 마릿수·간격은 손대지 않는다.</summary>
    private static void OneBlowEach(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex plain = new($"\\\"AreaID\\\"\\s*:\\s*{NovicePlainA}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        int changed = 0;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (!plain.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: options)!;
            template["MaximumHP"] = 1;
            template["Grow"] = false;
            File.WriteAllText(path, template.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            changed++;
        }

        Assert.True(changed > 0, "노비스평원A 정의를 못 찾았습니다.");
    }

    private static Direction Toward(Tile from, Tile to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? Direction.East : Direction.West)
            : (dy >= 0 ? Direction.South : Direction.North);
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

    /// <summary>가운데 둘레(10~40) 안에서 가장 가까운 괴물 — 가장자리 워프 쪽으로 쫓아가지 않는다.</summary>
    private static Tile? Nearest(WorldClient world)
    {
        if (world.State is not { } me)
        {
            return null;
        }

        return world.Creatures
            .Where(c => c.Kind == CreatureKind.Hostile && c.Where.X is >= 10 and <= 40 && c.Where.Y is >= 10 and <= 40)
            .OrderBy(c => Math.Abs(c.Where.X - me.Where.X) + Math.Abs(c.Where.Y - me.Where.Y))
            .Select(c => (Tile?)c.Where)
            .FirstOrDefault();
    }
}
