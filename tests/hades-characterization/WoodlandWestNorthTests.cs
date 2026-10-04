using System.Text.Json.Nodes;
using Lod.Mobile.Core.Model;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 서의·북의우드랜드 — 원작 lod441~464·lod700~723 을 노바 팩에서 새로 넣은 사냥터(사용자 2026-10-04). 월드맵 카드로 입구에 들고,
/// 입구 문은 월드맵으로 나간다. 노바 워프엔 레벨 제한이 없다(0~99). 노바에 길이 없던 구역은 두 줄로 잇는다(9-1 → 13-1 · 입구 → 14-1 → 20-1,
/// 사용자 2026-10-04). 자료는 <c>scripts/gen/world/build-woodland-west-north.py</c>.
/// </summary>
public sealed class WoodlandWestNorthTests
{
    [Theory]
    [InlineData("서의우드랜드", 23)]   // 1-1~20-1 · 9-2 · 17-2 · 19-2
    [InlineData("북의우드랜드", 23)]
    public void The_gate_reaches_every_zone_without_a_level_bar_and_every_zone_has_monsters(string side, int zoneCount)
    {
        string server = HadesWorkspace.ServerDataDirectory;
        Dictionary<string, JsonNode> areas = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "areas"), "*.json"))
        {
            JsonNode area = JsonNode.Parse(File.ReadAllText(path))!;
            areas.TryAdd(area["Name"]!.GetValue<string>(), area);
        }
        Dictionary<int, JsonNode> byId = areas.Values.ToDictionary(a => a["ID"]!.GetValue<int>());
        int gate = areas[$"{side}입구"]["ID"]!.GetValue<int>();
        int[] zones = [.. areas.Where(kv => kv.Key.StartsWith(side, StringComparison.Ordinal)).Select(kv => kv.Value["ID"]!.GetValue<int>()).Where(id => id != gate)];
        Assert.Equal(zoneCount, zones.Length);
        HashSet<int> mine = [gate, .. zones];

        Func<Tile, bool> Walled(int map) =>
            WorldMapTests.Walled(server, map, byId[map]["Cols"]!.GetValue<int>(), byId[map]["Rows"]!.GetValue<int>());

        List<(int From, int To)> warps = [];
        HashSet<(int Map, Tile Tile)> doors = [];
        List<(int Map, Tile Tile, string Path)> arrivals = [];
        bool doorToWorld = false;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "warps"), "*.json"))
        {
            JsonNode warp = JsonNode.Parse(File.ReadAllText(path))!;
            int from = warp["ActivationMapId"]!.GetValue<int>();
            int to = warp["To"]!["AreaID"]!.GetValue<int>();
            if (!mine.Contains(from) && !mine.Contains(to))
            {
                continue;
            }
            Assert.Equal(1, warp["LevelRequired"]!.GetValue<int>());
            foreach (JsonNode? at in warp["Activations"]!.AsArray())
            {
                Tile tile = new(at!["Location"]!["X"]!.GetValue<int>(), at["Location"]!["Y"]!.GetValue<int>());
                Assert.False(Walled(from)(tile), $"{path} 밟는 칸 {tile} 이 벽입니다.");
                doors.Add((from, tile));
            }
            if (warp["WarpType"]!.GetValue<string>() == "World")
            {
                doorToWorld |= from == gate;
                continue;
            }
            Tile arrival = new(warp["To"]!["Location"]!["X"]!.GetValue<int>(), warp["To"]!["Location"]!["Y"]!.GetValue<int>());
            Assert.False(Walled(to)(arrival), $"{path} 도착 칸 {arrival} 이 벽입니다.");
            arrivals.Add((to, arrival, path));
            warps.Add((from, to));
        }
        // 도착하자마자 다른 워프를 밟지 않는다.
        Assert.DoesNotContain(arrivals, a => doors.Contains((a.Map, a.Tile)));
        Assert.True(doorToWorld, "입구 문이 월드맵으로 나가지 않습니다.");

        // 월드맵 카드 도착 칸(노바 월드맵의 대기실 10,16)도 길이어야 한다.
        JsonNode card = JsonNode.Parse(File.ReadAllText(Path.Combine(server, "templates", "worldmaps", "temuair.json")))!["Portals"]!
            .AsArray().Single(p => p!["DisplayName"]!.GetValue<string>() == side)!;
        Assert.Equal(gate, card["Destination"]!["AreaID"]!.GetValue<int>());
        Assert.False(Walled(gate)(new Tile(card["Destination"]!["Location"]!["X"]!.GetValue<int>(), card["Destination"]!["Location"]!["Y"]!.GetValue<int>())));
        Assert.Equal(zoneCount, card["Zones"]!.AsArray().Count);

        HashSet<int> reached = [gate];
        Queue<int> next = new([gate]);
        while (next.TryDequeue(out int map))
        {
            foreach (var w in warps.Where(w => w.From == map && reached.Add(w.To)))
            {
                next.Enqueue(w.To);
            }
        }
        Assert.DoesNotContain(zones, zone => !reached.Contains(zone));

        HashSet<int> hunted = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "monsters", side), "*.json"))
        {
            JsonNode monster = JsonNode.Parse(File.ReadAllText(path))!;
            hunted.Add(monster["AreaID"]!.GetValue<int>());
            if (monster["Name"]!.GetValue<string>() == "맨티스")
            {
                Assert.Equal(6040, monster["MaximumHP"]!.GetValue<int>());   // 노바 6,040,000 은 ×1000 오타(SPEC 4)
            }
        }
        Assert.DoesNotContain(zones, zone => !hunted.Contains(zone));
    }

    /// <summary>장비 층 — 구역 번호 2~20 을 11·26·41·56·71·86 여섯 층에 앞에서부터 고르게(<c>scripts/lib/_drops.py</c> 와 같은 셈).</summary>
    private static readonly int[] Steps = [11, 26, 41, 56, 71, 86];

    /// <summary>서·북의우드랜드 2-1 이상 구역 — (맵, 이름, 장비 층). GearDropTests·DropVarietyTests 가 함께 쓴다.</summary>
    internal static IEnumerable<(int Map, string Name, int Layer)> Layered() =>
        Directory.EnumerateFiles(Path.Combine(HadesWorkspace.ServerDataDirectory, "areas"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!)
            .Select(area => (Map: area["ID"]!.GetValue<int>(), Name: area["Name"]!.GetValue<string>()))
            .Select(a => (a.Map, a.Name, Match: System.Text.RegularExpressions.Regex.Match(a.Name, @"^(서의|북의)우드랜드(\d+)-\d+$")))
            .Where(a => a.Match.Success && int.Parse(a.Match.Groups[2].Value) >= 2)
            .Select(a => (a.Map, a.Name, Steps[(int.Parse(a.Match.Groups[2].Value) - 2) * Steps.Length / 19]))
            .OrderBy(a => a.Map);

    /// <summary>층이 구역 깊이를 따라 줄지 않는다 — 9-1 아래 41레벨(동각반·동팔찌 층), 20-1 은 86.</summary>
    [Fact]
    public void Gear_layers_climb_with_the_zone_number()
    {
        Dictionary<string, int> layer = Layered().ToDictionary(z => z.Name, z => z.Layer);
        foreach (string side in new[] { "서의우드랜드", "북의우드랜드" })
        {
            Assert.Equal(11, layer[$"{side}2-1"]);
            Assert.Equal(41, layer[$"{side}9-1"]);
            Assert.Equal(layer[$"{side}9-1"], layer[$"{side}9-2"]);
            Assert.Equal(86, layer[$"{side}20-1"]);
            int[] climb = [.. Enumerable.Range(2, 19).Select(n => layer[$"{side}{n}-1"])];
            Assert.Equal(climb.Order(), climb);
        }
    }
}
