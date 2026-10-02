using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 뤼케시온해안 — 71~98레벨 사냥터(사용자 2026-10-02). 월드맵 카드로 대기실에 들고, 대기실 문은 월드맵으로 나간다(노바 팩).
/// 워프·괴물은 <c>scripts/gen/world/build-rucesion-coast.py</c> 가 혼든 워프·노바 배치에서 옮긴다.
/// </summary>
public sealed class RucesionCoastTests
{
    /// <summary>
    /// 대기실에서 워프만 타고 열한 구역 모두에 닿고, 대기실로 드는 워프는 모두 71레벨(월드맵 카드가 이 제한을 따른다),
    /// 대기실 문은 월드맵, 괴물이 열한 구역 모두에 있다.
    /// </summary>
    [Fact]
    public void The_lobby_reaches_every_zone_behind_level_71_and_every_zone_has_monsters()
    {
        string server = HadesWorkspace.ServerDataDirectory;
        Dictionary<string, int> ids = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "areas"), "*.json"))
        {
            JsonNode area = JsonNode.Parse(File.ReadAllText(path))!;
            ids.TryAdd(area["Name"]!.GetValue<string>(), area["ID"]!.GetValue<int>());
        }
        int lobby = ids["뤼케시온해안대기실"];
        int[] zones = [.. ids.Where(kv => kv.Key.StartsWith("뤼케시온해안", StringComparison.Ordinal) && kv.Value != lobby).Select(kv => kv.Value)];
        Assert.Equal(11, zones.Length);

        List<(int From, int To)> warps = [];
        List<int> intoLobby = [];
        bool doorToWorld = false;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "warps"), "*.json"))
        {
            JsonNode warp = JsonNode.Parse(File.ReadAllText(path))!;
            int to = warp["To"]!["AreaID"]!.GetValue<int>();
            int from = warp["ActivationMapId"]!.GetValue<int>();
            if (to == lobby)
            {
                intoLobby.Add(warp["LevelRequired"]!.GetValue<int>());
            }
            doorToWorld |= from == lobby && warp["WarpType"]!.GetValue<string>() == "World";
            warps.Add((from, to));
        }

        Assert.NotEmpty(intoLobby);
        Assert.All(intoLobby, level => Assert.Equal(71, level));
        Assert.True(doorToWorld, "대기실 문이 월드맵으로 나가지 않습니다.");

        HashSet<int> reached = [lobby];
        Queue<int> next = new([lobby]);
        while (next.TryDequeue(out int map))
        {
            foreach (var w in warps.Where(w => w.From == map && reached.Add(w.To)))
            {
                next.Enqueue(w.To);
            }
        }
        Assert.DoesNotContain(zones, zone => !reached.Contains(zone));

        HashSet<int> hunted = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "monsters", "뤼케시온해안"), "*.json"))
        {
            hunted.Add(JsonNode.Parse(File.ReadAllText(path))!["AreaID"]!.GetValue<int>());
        }
        Assert.DoesNotContain(zones, zone => !hunted.Contains(zone));
    }
}
