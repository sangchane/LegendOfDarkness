using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사용자(2026-09-26) — 99레벨 이전 사냥터의 드랍 <b>종류</b>를 늘린다. 재료(잡템·괴물 부산물)는 늘리지 않고,
/// 속성·접미사 장비와 포션만 더한다. 새 칸 때문에 기존 물건의 실제 확률이 떨어지지 않게 하고, 새 장비 한
/// 종은 실제 확률 2%(1.5배 전)를 넘지 않는다. 정의를 만드는 것은 <c>scripts/build-drop-variety.py</c> 다.
/// </summary>
/// <remarks>
/// 실제 확률 = <c>DropRate × DropBoost(1.5) ÷ 목록 칸수</c>(<c>Formulas/monsterexp.cs</c> DetermineRandomDrop).
/// </remarks>
public sealed class DropVarietyTests
{
    private const double DropBoost = 1.5;

    /// <summary>새 장비 한 종의 실제 확률 윗선 — 1.5배 전 2%, 곱한 뒤 3%.</summary>
    private const double GearMost = 0.02 * DropBoost;

    internal static readonly string[] LeatherGloves11 =
        ["로오의가죽장갑", "이아의가죽장갑", "메투스의가죽장갑", "세토아의가죽장갑", "세오의가죽장갑", "셔스의가죽장갑", "칸의가죽장갑"];

    internal static readonly string[] LeatherBelts11 = ["화염의가죽벨트", "바다의가죽벨트", "바람의가죽벨트", "대지의가죽벨트"];

    internal static readonly string[] Crystal51 =
        ["화염의크리스탈목걸이", "바다의크리스탈목걸이", "바람의크리스탈목걸이", "대지의크리스탈목걸이"];

    internal static readonly string[] Obsidian81 =
        ["화염의흑요석목걸이", "바다의흑요석목걸이", "바람의흑요석목걸이", "대지의흑요석목걸이"];

    internal static readonly string[] BronzeGloves41 = ["로오의동장갑", "칸의동장갑"];

    private static readonly string[] AbelPotions = ["상급체력포션", "상급마력포션"];

    private static readonly int[] Abel = [20584, 20585, 20586, 20587, 20588, 20589, 20590, 20591, 20592, 20593, 20594];

    /// <summary>크라켄·킹아크퍼스는 `build-gear-drops.py` FIELD_BOSSES 가 한 칸짜리 목록으로 관리한다.</summary>
    private static readonly HashSet<string> Reserved = ["크라켄1", "크라켄2", "킹아크퍼스1", "킹아크퍼스2"];

    /// <summary>맵 · 입장 레벨 · 그 사냥터에 더한 장비 한 벌 · 적어도 몇 종이 새로 보여야 하나.</summary>
    private static readonly (int[] Maps, int Entry, string[] Gear, int Least)[] Grounds =
    [
        ([20022, 20023, 20024], 11, LeatherGloves11, 3),
        ([20263, 20264, 20265, 20266, 20267, 20268], 21, LeatherBelts11, 3),
        ([20025, 20026], 51, Crystal51, 2),
        ([20020], 81, Obsidian81, 4),
        ([20584, 20585, 20586, 20587, 20588], 51, BronzeGloves41, 2),
    ];

    [Fact]
    public void Each_later_ground_shows_new_level_matched_gear_no_likelier_than_two_percent()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> wrong = [];

        foreach ((int[] maps, int entry, string[] gear, int least) in Grounds)
        {
            foreach (string name in gear)
            {
                int level = (int?)items[name]["LevelRequired"] ?? 0;
                if (level > entry)
                {
                    wrong.Add($"{name} 레벨 {level} > 입장 {entry}");
                }
            }

            foreach (int map in maps)
            {
                JsonNode[] here = [.. Monsters().Where(m => (int?)m["AreaID"] == map && !Reserved.Contains(Name(m)))];
                int shown = here.SelectMany(Dropped).Where(gear.Contains).Distinct().Count();

                if (shown < least)
                {
                    wrong.Add($"{map}: 새 장비 {shown}종(적어도 {least})");
                }

                foreach (JsonNode monster in here)
                {
                    string[] listed = [.. Dropped(monster)];

                    foreach (string name in listed.Where(gear.Contains))
                    {
                        double real = ((double?)items[name]["DropRate"] ?? 0) * DropBoost / listed.Length;
                        if (real > GearMost + 1e-9)
                        {
                            wrong.Add($"{Name(monster)}@{map} {name} {real:P1} > {GearMost:P0}");
                        }
                    }
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join(", ", wrong));
    }

    [Fact]
    public void Every_ordinary_abel_monster_drops_both_high_potions()
    {
        JsonNode[] here = [.. Monsters().Where(m => Abel.Contains((int?)m["AreaID"] ?? 0) && !Reserved.Contains(Name(m)))];
        Assert.NotEmpty(here);

        string[] missing =
        [
            .. here.Where(m => !AbelPotions.All(Dropped(m).Contains)).Select(m => $"{Name(m)}@{m["AreaID"]}"),
        ];

        Assert.True(missing.Length == 0, $"아벨해안에서 상급 포션이 없는 괴물: {string.Join(", ", missing)}");
    }

    private static string Name(JsonNode monster) => monster["Name"]?.GetValue<string>() ?? "";

    private static IEnumerable<string> Dropped(JsonNode monster)
    {
        JsonNode? drops = monster["Drops"];
        JsonArray? values = drops is JsonObject ? drops["$values"] as JsonArray : drops as JsonArray;

        foreach (JsonNode? entry in values ?? [])
        {
            if (entry?.GetValue<string>() is { Length: > 0 } name && name != "random")
            {
                yield return name;
            }
        }
    }

    private static JsonNode[] Monsters() => _monsters ??=
        [.. Definitions(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "monsters"))];

    private static JsonNode[]? _monsters;

    private static IReadOnlyDictionary<string, JsonNode> Items()
    {
        Dictionary<string, JsonNode> items = [];

        foreach (JsonNode item in Definitions(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "items")))
        {
            if (item["Name"]?.GetValue<string>() is { Length: > 0 } name)
            {
                items[name] = item;
            }
        }

        Assert.NotEmpty(items);
        return items;
    }

    private static IEnumerable<JsonNode> Definitions(string folder)
    {
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            JsonNode? node;

            try
            {
                node = JsonNode.Parse(File.ReadAllText(path), documentOptions: lenient);
            }
            catch (JsonException)
            {
                continue;
            }

            if (node is not null)
            {
                yield return node;
            }
        }
    }
}
