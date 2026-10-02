using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 구광산 — 마인마을 10시 방향 출구(0,49~54)로 들어가는 29층 광산(사용자 2026-10-02). 맵·워프는 혼든 팩
/// 「공식길드전용던전」에서 <c>scripts/gen/world/build-old-mine.py</c> 가 옮긴다. 8-1 은 어느 팩에도 없어 빠져 있다.
/// </summary>
public sealed class OldMineTests
{
    /// <summary>마인마을 10시 출구 여섯 칸이 99레벨부터 대기실로 가고, 대기실에서 워프만 타고 모든 층에 닿으며, 대기실 문이 마인마을로 돌아온다.</summary>
    [Fact]
    public void The_west_exit_of_mine_town_leads_through_the_lobby_to_every_floor()
    {
        string server = HadesWorkspace.ServerDataDirectory;
        Dictionary<string, int> ids = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "areas"), "*.json"))
        {
            JsonNode area = JsonNode.Parse(File.ReadAllText(path))!;
            ids.TryAdd(area["Name"]!.GetValue<string>(), area["ID"]!.GetValue<int>());
        }

        List<(int From, int X, int Y, int To)> warps = [];
        List<int> entranceLevels = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server, "templates", "warps"), "*.json"))
        {
            JsonNode warp = JsonNode.Parse(File.ReadAllText(path))!;
            int to = warp["To"]!["AreaID"]!.GetValue<int>();
            if (warp["ActivationMapId"]!.GetValue<int>() == ids["마인마을"] && to == ids["구광산대기실"])
            {
                entranceLevels.Add(warp["LevelRequired"]!.GetValue<int>());
            }
            foreach (JsonNode? at in warp["Activations"]!.AsArray())
            {
                warps.Add((at!["AreaID"]!.GetValue<int>(), at["Location"]!["X"]!.GetValue<int>(), at["Location"]!["Y"]!.GetValue<int>(), to));
            }
        }

        int town = ids["마인마을"], lobby = ids["구광산대기실"];
        for (int y = 49; y <= 54; y++)
        {
            Assert.Contains((town, 0, y, lobby), warps);
        }
        Assert.Contains(warps, w => w.From == lobby && w.To == town);
        Assert.Equal(Enumerable.Repeat(99, 6), entranceLevels);   // 99레벨부터 — 입구에만

        HashSet<int> reached = [lobby];
        Queue<int> next = new([lobby]);
        while (next.TryDequeue(out int map))
        {
            foreach (var w in warps.Where(w => w.From == map && reached.Add(w.To)))
            {
                next.Enqueue(w.To);
            }
        }

        string[] floors = [.. ids.Keys.Where(name => name.StartsWith("구광산", StringComparison.Ordinal) && name != "구광산대기실")];
        Assert.Equal(35, floors.Length);                       // 1-1 ~ 29-1 과 갈래층, 8-1 없음
        Assert.DoesNotContain(floors, name => !reached.Contains(ids[name]));
    }
}
