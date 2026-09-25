using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 젠 마릿수와 선 자리, 그리고 맵을 옮길 때 지난 맵의 괴물이 남지 않는지.
/// 마릿수는 <c>MonolithComponent</c> 한 곳이 정한다 — <c>SpawnMax × √(칸수/400) × 0.7</c>(0.7 은 2026-09-24 사용자 결정,
/// "30% 줄여 달라").
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class SpawnCountTests : IDisposable
{
    /// <summary>노비스평원A — 50x50 이라 넓이 배수가 6(2,500 ÷ 400)이다.</summary>
    private const int NovicePlainA = 20393;

    private const int PlainSide = 50;

    /// <summary>노비스마을 — 70x70.</summary>
    private const string NoviceTown = "노비스마을";

    private const int NoviceTownId = 20373;

    /// <summary>시험 정의 하나가 적는 최대 마릿수.</summary>
    private const int Written = 4;

    /// <summary>
    /// 이 맵에 서야 할 마릿수 — 4 × √6 × 0.7 = 6.86 → 7. 줄이기 전(× 1)에는 10 이었다.
    /// </summary>
    private static readonly int Expected = (int)Math.Round(Written * Math.Sqrt(PlainSide * PlainSide / 400) * 0.7);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_map_holds_the_thinned_count_and_every_one_stands_on_its_floor()
    {
        const string name = "spawncount";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlainA, 25, 25));
        LeaveOnlyOneStandingDefinition(server);
        Waiting.MakeGameMaster(server, name);
        (WorldSession session, WorldClient world) = await Enter(server, name);
        using WorldSession _ = session;

        await Waiting.Until(() => world.State?.Map.Id == NovicePlainA, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);

        // 1초에 한 마리씩 선다(SpawnRate 1 ÷ 넓이 배수 6, 순회는 1초). 다 차도록 넉넉히 기다린다.
        await Task.Delay(TimeSpan.FromSeconds(15), _deadline.Token);

        // 시야는 걸음 수로 12 칸 안이라 한 자리에서 맵 전체가 보이지 않는다. 열 칸 간격으로 옮겨 다니며 모은다.
        // 괴물은 제자리에 묶어 두었으므로(PathQualifer Fixed) 모은 자리는 선 자리 그대로다.
        Dictionary<uint, Tile> seen = [];

        for (int y = 5; y < PlainSide; y += 10)
        {
            for (int x = 5; x < PlainSide; x += 10)
            {
                await world.SayAsync($"/tp \"노비스평원A\" {x} {y}", _deadline.Token);
                await Task.Delay(1200, _deadline.Token);

                foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
                {
                    seen[mob.Serial] = mob.Where;
                }
            }
        }

        bool[,] walls = Walls(server, NovicePlainA, PlainSide, PlainSide);

        Assert.All(seen, pair =>
        {
            Tile where = pair.Value;
            Assert.True(where.X >= 0 && where.Y >= 0 && where.X < PlainSide && where.Y < PlainSide,
                $"괴물 {pair.Key} 가 맵 밖 ({where.X},{where.Y}) 에 섰습니다.");
            Assert.False(walls[where.X, where.Y], $"괴물 {pair.Key} 가 벽 ({where.X},{where.Y}) 에 섰습니다.");
        });

        Assert.True(seen.Count == Expected,
            $"노비스평원A 에 선 시험 괴물이 {seen.Count}마리입니다({Expected}마리를 기대 — {Written} × √6 × 0.7). " +
            $"자리: {string.Join(" ", seen.Values.Select(t => $"({t.X},{t.Y})"))}");
    }

    /// <summary>
    /// 서버는 맵을 옮기거나 같은 맵 안에서 순간이동하면 제 쪽 시야(<c>Aisling.View</c>)를 비우고 0x15 를 보낸다.
    /// 그 전에 보여 준 괴물에게 0x0E 를 보내지 않으면, 0x15 에 화면을 비우지 않는 클라이언트(모바일)에는 지난
    /// 맵의 괴물이 **움직이지도 죽지도 않는 채로** 새 맵 위에 남는다 — 사냥하다 마을로 돌아오면 마을에 좀비가
    /// 서 있고, 마을이 사냥터보다 작으면 맵 밖에 서 있다(2026-09-24 사용자 보고).
    /// </summary>
    [Fact]
    public async Task Leaving_a_map_takes_its_monsters_off_the_screen()
    {
        const string name = "stalemob";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlainA, 25, 25));
        Waiting.MakeGameMaster(server, name);
        (WorldSession session, WorldClient world) = await Enter(server, name);
        using WorldSession _ = session;

        await Waiting.Until(() => world.State?.Map.Id == NovicePlainA, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "노비스평원A 에서 괴물이 보이지 않았습니다.", _deadline.Token, TimeSpan.FromSeconds(90));

        HashSet<uint> plain = [.. world.Creatures.Where(c => c.Kind == CreatureKind.Hostile).Select(c => c.Serial)];

        await world.SayAsync($"/tp \"{NoviceTown}\" 35 35", _deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == NoviceTownId, "노비스마을로 옮기지 못했습니다.", _deadline.Token);
        await Task.Delay(2000, _deadline.Token);

        uint[] left = [.. world.Creatures.Select(c => c.Serial).Where(plain.Contains)];

        Assert.True(left.Length == 0,
            $"노비스평원A 에서 보던 괴물 {left.Length}마리가 노비스마을 화면에 남았습니다(평원에서 {plain.Count}마리를 봤다).");
    }

    /// <summary>
    /// 같은 맵 안에서 다시 그리면(막힌 걸음·속도 초과가 부르는 Refresh — 느린 망에서는 걸음이 몰려 와 자주 걸린다) 곁의 괴물은 화면에서 한순간도 빠지지
    /// 않는다. 맵을 옮길 때 거두는 것(위 시험)을 모든 새로고침에 했더니 괴물이 모두 사라졌다가 1초쯤 뒤 돌아왔다
    /// (2026-09-25 사용자 "몬스터가 보였다가 사라진다").
    /// </summary>
    [Fact]
    public async Task Redrawing_the_same_map_keeps_the_monsters_nearby_on_the_screen()
    {
        const string name = "redrawmob";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlainA, 25, 25));
        Waiting.MakeGameMaster(server, name);
        (WorldSession session, WorldClient world) = await Enter(server, name);
        using WorldSession _ = session;

        await Waiting.Until(() => world.State?.Map.Id == NovicePlainA, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);

        HashSet<uint> near = [];
        int hops = 0;

        for (int round = 0; round < 6; round++)
        {
            await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && Near(c.Where)),
                "노비스평원A 에서 곁의 괴물이 보이지 않았습니다.", _deadline.Token, TimeSpan.FromSeconds(90));

            near = [.. world.Creatures.Where(c => c.Kind == CreatureKind.Hostile && Near(c.Where)).Select(c => c.Serial)];
            // 걸음을 서버가 허락하는 것(275ms)보다 빨리 두 번 — 둘째 걸음이 Refresh(true)를 부른다(Format06Handler).
            await world.WalkAsync(round % 2 == 0 ? Direction.East : Direction.West, _deadline.Token);
            await world.WalkAsync(round % 2 == 0 ? Direction.West : Direction.East, _deadline.Token);
            hops++;

            for (int look = 0; look < 75; look++)
            {
                uint[] gone = [.. near.Where(serial => world.Creatures.All(c => c.Serial != serial))];

                // 죽은 것은 없다(아무도 치지 않는다). 곁에서 걷던 것이 한 번에 시야 밖(12)으로 나갈 수는 없다.
                Assert.True(gone.Length == 0,
                    $"같은 맵 새로고침 {hops}번째에 곁의 괴물 {gone.Length}마리가 화면에서 빠졌습니다(곁에 {near.Count}마리).");

                await Task.Delay(20, _deadline.Token);
            }
        }

        static bool Near(Tile where) => Math.Abs(where.X - 25) + Math.Abs(where.Y - 25) <= 5;
    }

    /// <summary>
    /// 노비스평원A 의 정의를 모두 재우고(SpawnMax 0) 제자리에 묶인 시험 정의 하나만 남긴다. 빨리 서도록 SpawnRate 1.
    /// </summary>
    private static void LeaveOnlyOneStandingDefinition(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex plain = new($"\\\"AreaID\\\"\\s*:\\s*{NovicePlainA}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonSerializerOptions indented = new() { WriteIndented = true };

        string[] sources = [.. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .Where(path => plain.IsMatch(File.ReadAllText(path)))];
        Assert.NotEmpty(sources);

        JsonNode? model = null;

        foreach (string source in sources)
        {
            JsonNode template = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;
            model ??= JsonNode.Parse(template.ToJsonString());
            template["SpawnMax"] = 0;
            File.WriteAllText(source, template.ToJsonString(indented));
        }

        model!["Name"] = "마릿수시험";
        model["BaseName"] = "마릿수시험";
        model["SpawnMax"] = Written;
        model["SpawnType"] = 2;
        model["SpawnRate"] = 1;
        model["MoodType"] = 1;
        model["PathQualifer"] = 2;
        model["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "spawn-count.json"), model.ToJsonString(indented));
    }

    /// <summary>서버와 같은 식으로 벽을 읽는다(<c>Area.OnLoaded</c> · <c>ParseMapWalls</c>) — 칸마다 6바이트, 앞 2바이트는 바닥.</summary>
    private static bool[,] Walls(IsolatedHadesServer server, int map, int cols, int rows)
    {
        byte[] sotp = File.ReadAllBytes(Path.Combine(server.ContentLocation, "static", "sotp.dat"));
        byte[] tiles = File.ReadAllBytes(Path.Combine(server.ContentLocation, "maps", $"lod{map}.map"));
        bool[,] walls = new bool[cols, rows];

        bool Solid(int part) => part > 0 && sotp[part - 1] == 0x0F;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int at = (y * cols + x) * 6;
                int left = BitConverter.ToInt16(tiles, at + 2);
                int right = BitConverter.ToInt16(tiles, at + 4);
                walls[x, y] = Solid(left) || Solid(right);
            }
        }

        return walls;
    }

    private async Task<(WorldSession Session, WorldClient World)> Enter(IsolatedHadesServer server, string name)
    {
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        return (session, world);
    }
}
