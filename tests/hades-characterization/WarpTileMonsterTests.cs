using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 괴물은 워프 칸에 서지도(젠), 걸어 들어가지도 않는다(<c>Area.IsWarp</c>) — 자동 사냥이 괴물을 쫓다 그 칸을 밟고
/// 딴 맵으로 넘어가던 일(사용자, 2026-10-05). 노비스평원A 를 가운데 열 줄(y 15~24)만 남기고 워프 칸으로 덮은 뒤,
/// 그 띠 안에 세운 괴물이 30초 동안 한 번도 띠 밖에 보이지 않는지 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class WarpTileMonsterTests : IDisposable
{
    private const int NovicePlainA = 20393;
    private const int Side = 50;
    private const int Top = 15;
    private const int Bottom = 24;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Monsters_neither_spawn_on_nor_walk_onto_warp_tiles()
    {
        const string name = "warpband";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NovicePlainA, 25, 20));
        CoverWithWarps(server);
        LeaveOnlyWanderers(server);
        Waiting.MakeGameMaster(server, name);

        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);
        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == NovicePlainA, "노비스평원A 에 들어가지 못했습니다.", _deadline.Token);
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile),
            "노비스평원A 에 시험 괴물이 서지 않았습니다.", _deadline.Token, TimeSpan.FromSeconds(60));

        HashSet<Tile> seen = [];

        for (DateTime end = DateTime.UtcNow.AddSeconds(30); DateTime.UtcNow < end;)
        {
            seen.UnionWith(world.Creatures.Where(c => c.Kind == CreatureKind.Hostile).Select(c => c.Where));
            await Task.Delay(100, _deadline.Token);
        }

        Tile[] onWarp = [.. seen.Where(t => t.Y < Top || t.Y > Bottom)];
        Assert.True(onWarp.Length == 0, $"괴물이 워프 칸에 섰습니다: {string.Join(" ", onWarp.Select(t => $"({t.X},{t.Y})"))}");

        // 띠 가장자리까지 와서 막혔어야 시험이 뜻이 있다 — 벽에 닿지도 않았다면 아무것도 확인하지 않은 것이다.
        Assert.Contains(seen, t => t.Y == Top || t.Y == Bottom);
    }

    /// <summary>y 15~24 밖의 모든 칸을 노비스마을로 보내는 워프 하나로 덮는다.</summary>
    private static void CoverWithWarps(IsolatedHadesServer server)
    {
        JsonArray spots = [];

        for (int y = 0; y < Side; y++)
        {
            for (int x = 0; x < Side; x++)
            {
                if (y < Top || y > Bottom)
                {
                    spots.Add(new JsonObject { ["AreaID"] = NovicePlainA, ["Location"] = new JsonObject { ["X"] = x, ["Y"] = y }, ["PortalKey"] = 0 });
                }
            }
        }

        JsonObject warp = new()
        {
            ["ActivationMapId"] = NovicePlainA,
            ["Activations"] = spots,
            ["LevelRequired"] = 1,
            ["To"] = new JsonObject { ["AreaID"] = 20373, ["Location"] = new JsonObject { ["X"] = 35, ["Y"] = 35 }, ["PortalKey"] = 0 },
            ["WarpType"] = "Map",
            ["Name"] = "warpband",
        };

        File.WriteAllText(Path.Combine(server.ContentLocation, "templates", "warps", "warpband.json"), warp.ToJsonString());
    }

    /// <summary>노비스평원A 의 정의를 재우고 돌아다니는(Wander) 순한 정의 하나만 남긴다.</summary>
    private static void LeaveOnlyWanderers(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex plain = new($"\\\"AreaID\\\"\\s*:\\s*{NovicePlainA}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonNode? model = null;

        foreach (string source in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories).Where(path => plain.IsMatch(File.ReadAllText(path))))
        {
            JsonNode template = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;
            model ??= JsonNode.Parse(template.ToJsonString());
            template["SpawnMax"] = 0;
            File.WriteAllText(source, template.ToJsonString());
        }

        Assert.NotNull(model);
        model!["Name"] = "워프시험";
        model["BaseName"] = "워프시험";
        model["SpawnMax"] = 6;
        model["SpawnType"] = 2;
        model["SpawnRate"] = 1;
        model["MoodType"] = 1;
        model["PathQualifer"] = 1;
        model["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "warp-band.json"), model.ToJsonString());
    }
}
