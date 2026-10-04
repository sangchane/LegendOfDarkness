using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 죽음의마을 — 99레벨 사냥터(사용자 2026-10-04). 월드맵 카드·마인마을 워프로 입구5에 들고, 입구5 위쪽에서 죽음의마을1로 든다
/// (노바·5.99 같은 칸). 워프·수치는 <c>scripts/gen/world/build-death-village.py</c>.
/// </summary>
public sealed class DeathVillageTests
{
    /// <summary>
    /// 입구5로 드는 워프 중 가장 엄한 것이 99(월드맵 카드가 이를 따른다), 입구5에서 워프만 타고 네 구역에 닿고,
    /// 네 구역 모두에 괴물이 있으며 좀비 체력은 노바 값(3만, 사용자 「노바 안에서 맞춤」)이다.
    /// </summary>
    [Fact]
    public void The_gate_reaches_all_four_zones_behind_level_99_with_nova_zombies()
    {
        string server = HadesWorkspace.ServerDataDirectory;
        Dictionary<string, int> ids = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "areas"), "*.json"))
        {
            JsonNode area = JsonNode.Parse(File.ReadAllText(path))!;
            ids.TryAdd(area["Name"]!.GetValue<string>(), area["ID"]!.GetValue<int>());
        }
        int gate = ids["죽음의마을입구5"];
        int[] zones = [.. Enumerable.Range(1, 4).Select(n => ids[$"죽음의마을{n}"])];

        List<(int From, int To)> warps = [];
        List<int> intoGate = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "warps"), "*.json"))
        {
            JsonNode warp = JsonNode.Parse(File.ReadAllText(path))!;
            int to = warp["To"]!["AreaID"]!.GetValue<int>();
            if (to == gate)
            {
                intoGate.Add(warp["LevelRequired"]!.GetValue<int>());
            }
            warps.Add((warp["ActivationMapId"]!.GetValue<int>(), to));
        }
        Assert.Equal(99, intoGate.Max());

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
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "monsters", "5.99"), "*@죽음의마을?.json"))
        {
            JsonNode monster = JsonNode.Parse(File.ReadAllText(path))!;
            hunted.Add(monster["AreaID"]!.GetValue<int>());
            if (monster["Name"]!.GetValue<string>() == "좀비")
            {
                Assert.Equal(30000, monster["MaximumHP"]!.GetValue<int>());
            }
        }
        Assert.DoesNotContain(zones, zone => !hunted.Contains(zone));
    }
}
