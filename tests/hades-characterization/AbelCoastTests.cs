using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 아벨해안 — 아벨마을 남쪽 끝(44~45,67)을 밟으면 아벨해안대기실(38,41)이고, 대기실 서쪽 끝(0,31~34)이 아벨해안1-B 다.
/// 돌아올 때는 1-B 동쪽 끝(49,17~20) → 대기실(2,31~34), 대기실 문(41,42~43) → 아벨마을(44~45,63).
/// 근거는 5.99 팩 <c>warp/Abel_Warp.txt</c>(<c>data/server-packs/extracted/5.99-server/warps.json</c>).
/// </summary>
/// <remarks>
/// 워프 자체는 첫 워프 이식(하데스 04f49d3e2) 때 들어왔지만 레벨 칸을 버렸다 — 5.99 줄 끝 두 칸이 들어오는 레벨
/// 범위다. 5.99 는 아벨해안 130여 줄 모두에 51~80 을 걸었지만, **입구(아벨마을 → 대기실) 2줄에만** 건다 —
/// 사용자 2026-09-25 "입구에만 걸어 두면 돼"(안쪽에서 81 이 되어도 해안 안을 다닐 수 있다). 나머지는 제한이 없다.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class AbelCoastTests : IDisposable
{
    private const int AbelTown = 20030;
    private const int CoastLobby = 20595;
    private const int CoastOneB = 20585;

    private const string AbelWarpFile = "warp/Abel_Warp.txt";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));

    public void Dispose() => _deadline.Dispose();

    /// <summary>아벨해안에 닿는 5.99 줄마다 같은 칸·같은 목적지의 워프가 있고, 입구 두 줄만 5.99 의 레벨 범위를 갖는다.</summary>
    [Fact]
    public void Every_abel_coast_warp_exists_and_only_the_entrance_carries_the_599_level_range()
    {
        string server = HadesWorkspace.ServerDataDirectory;
        Dictionary<string, int> ids = AreaIds(server);
        Dictionary<(int Map, int X, int Y, int To), JsonNode> warps = Warps(server);

        JsonArray rows = JsonNode.Parse(File.ReadAllText(Path.Combine(
            HadesWorkspace.RepositoryRoot, "data", "server-packs", "extracted", "5.99-server", "warps.json")))!.AsArray();

        List<string> wrong = [];
        int seen = 0;

        foreach (JsonNode? row in rows)
        {
            string from = row!["출발맵"]!.GetValue<string>(), to = row["도착맵"]!.GetValue<string>();

            if (row["출처"]!.GetValue<string>() != AbelWarpFile
                || !(from.StartsWith("아벨해안", StringComparison.Ordinal) || to.StartsWith("아벨해안", StringComparison.Ordinal)))
            {
                continue;
            }

            seen++;
            JsonArray raw = row["raw"]!.AsArray();
            int low = int.Parse(raw[7]!.GetValue<string>()), high = int.Parse(raw[8]!.GetValue<string>());
            (int, int, int, int) key = (ids[from], int.Parse(row["출발"]![0]!.GetValue<string>()),
                int.Parse(row["출발"]![1]!.GetValue<string>()), ids[to]);

            if (!warps.TryGetValue(key, out JsonNode? warp))
            {
                wrong.Add($"{from}{key.Item2},{key.Item3} → {to}: 워프가 없다");
                continue;
            }

            int required = warp["LevelRequired"]?.GetValue<int>() ?? 0;
            int maximum = warp["LevelMaximum"]?.GetValue<int>() ?? 0;

            bool entrance = from.StartsWith("아벨마을", StringComparison.Ordinal) && to.StartsWith("아벨해안대기실", StringComparison.Ordinal);
            (int wantLow, int wantHigh) = entrance ? (Math.Max(1, low), high < 99 ? high : 0) : (1, 0);

            if (required != wantLow || maximum != wantHigh)
            {
                wrong.Add($"{from}({key.Item2},{key.Item3}) → {to}: {required}~{maximum} (바랐던 것 {wantLow}~{wantHigh}, 5.99 는 {low}~{high})");
            }
        }

        Assert.Equal(132, seen);
        Assert.True(wrong.Count == 0, $"{wrong.Count}줄이 5.99 와 다르다:\n{string.Join('\n', wrong.Take(10))}");
    }

    /// <summary>
    /// 51레벨이 아벨마을에서 걸어 대기실을 지나 아벨해안1-B 에 닿고, 같은 길로 아벨마을에 돌아온다.
    /// 50레벨은 아벨마을 남쪽 끝에서 막힌다.
    /// </summary>
    [Fact]
    public async Task A_level_51_walks_from_abel_to_the_first_coast_and_back_and_a_level_50_is_turned_away()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (AbelTown, 44, 64));
        server.Start(TimeSpan.FromMinutes(2));

        WorldClient young = await Enter(server, "coastyoung", level: 50);
        await Waiting.WalkUntil(young, Direction.South, () => young.State?.Where.Y == 66, _deadline.Token);
        await young.WalkAsync(Direction.South, _deadline.Token);
        await Task.Delay(1500, _deadline.Token);
        await young.RefreshAsync(_deadline.Token);
        await Task.Delay(500, _deadline.Token);

        Assert.True(young.State?.Map.Id == AbelTown,
            $"50레벨이 아벨마을 남쪽 끝(44,67)을 밟고 {young.State} 로 갔습니다 — 5.99 는 51~80 만 들여보냅니다.");
        Assert.Contains("레벨이 낮습니다", young.Said, StringComparison.Ordinal);

        // 막힌 사람은 워프 칸(44,67)에 그대로 서 있다 — 비키지 않으면 다음 사람이 그 칸을 못 밟는다.
        await young.LogOutAsync(_deadline.Token);

        WorldClient world = await Enter(server, "coastwalker", level: 51);

        // 아벨마을 44,64 → 44,66 → (44,67) 대기실 38,41.
        await Waiting.WalkUntil(world, Direction.South, () => world.State?.Map.Id == CoastLobby, _deadline.Token, attempts: 12);
        await Arrive(world, CoastLobby, new Tile(38, 41), "아벨마을 남쪽 끝을 밟았는데 아벨해안대기실 38,41 로 가지 않았습니다.");

        // 대기실 서쪽 끝(0,31) → 아벨해안1-B 47,17.
        await WalkTo(server, world, CoastLobby, new Tile(1, 31));
        await Waiting.WalkUntil(world, Direction.West, () => world.State?.Map.Id == CoastOneB, _deadline.Token);
        await Arrive(world, CoastOneB, new Tile(47, 17), "대기실 서쪽 끝을 밟았는데 아벨해안1-B 47,17 로 가지 않았습니다.");

        // 돌아온다 — 1-B 동쪽 끝(49,17) → 대기실 2,31 → 문(41,42) → 아벨마을 44,63.
        await Waiting.WalkUntil(world, Direction.East, () => world.State?.Map.Id == CoastLobby, _deadline.Token);
        await Arrive(world, CoastLobby, new Tile(2, 31), "아벨해안1-B 동쪽 끝을 밟았는데 대기실 2,31 로 돌아오지 않았습니다.");

        await WalkTo(server, world, CoastLobby, new Tile(40, 42));
        await Waiting.WalkUntil(world, Direction.East, () => world.State?.Map.Id == AbelTown, _deadline.Token);
        await Arrive(world, AbelTown, new Tile(44, 63), "대기실 문(41,42)을 밟았는데 아벨마을 44,63 으로 돌아오지 않았습니다.");
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string name, int level)
    {
        LoginFlow.TryCreateAccount(server, name);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = level;
        File.WriteAllText(saved, character.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is { } state && state.Map.Id == AbelTown, $"{name} 가 아벨마을에 들어가지 못했습니다.", _deadline.Token);
        return world;
    }

    private async Task WalkTo(IsolatedHadesServer server, WorldClient world, int mapId, Tile goal)
    {
        MapInfo map = world.State!.Map;
        IReadOnlyList<Tile>? way = Pathing.Way(world.State.Where, goal,
            WorldMapTests.Walled(server, mapId, map.Columns, map.Rows), reach: 120);

        Assert.True(way is not null, $"{mapId} {world.State.Where} 에서 {goal} 로 가는 길이 맵에 없습니다.");
        await WorldMapTests.WalkTheWay(world, way!, mapId, _deadline.Token);
    }

    private Task Arrive(WorldClient world, int mapId, Tile where, string failure) =>
        Waiting.Until(() => world.State is { } state && state.Map.Id == mapId && state.Where == where,
            $"{failure} 마지막: {world.State} · 서버가 한 말: {world.Said}", _deadline.Token);

    private static Dictionary<string, int> AreaIds(string server)
    {
        Dictionary<string, int> ids = [];

        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "areas"), "*.json"))
        {
            JsonNode area = JsonNode.Parse(File.ReadAllText(path))!;
            ids.TryAdd(area["Name"]!.GetValue<string>(), area["ID"]!.GetValue<int>());
        }

        return ids;
    }

    private static Dictionary<(int, int, int, int), JsonNode> Warps(string server)
    {
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        Dictionary<(int, int, int, int), JsonNode> warps = [];

        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "warps"), "*.json"))
        {
            JsonNode warp;

            try
            {
                warp = JsonNode.Parse(File.ReadAllText(path), documentOptions: lenient)!;
            }
            catch (JsonException)
            {
                continue; // 못 읽는 워프는 WarpIntegrityTests 가 본다.
            }

            if (warp["To"]?["AreaID"] is null || warp["Activations"] is not JsonArray activations)
            {
                continue;
            }

            int to = warp["To"]!["AreaID"]!.GetValue<int>();

            foreach (JsonNode? at in activations)
            {
                warps.TryAdd((at!["AreaID"]!.GetValue<int>(), at["Location"]!["X"]!.GetValue<int>(),
                    at["Location"]!["Y"]!.GetValue<int>(), to), warp);
            }
        }

        return warps;
    }
}
