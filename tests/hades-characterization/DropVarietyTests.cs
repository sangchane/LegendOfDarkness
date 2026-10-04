using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사용자(2026-09-26) — 99레벨 이전 사냥터의 드랍 <b>종류</b>를 늘린다. 재료(잡템·괴물 부산물)는 늘리지 않고,
/// 속성·접미사 장비와 포션만 더한다. 새 칸 때문에 기존 물건의 실제 확률이 떨어지지 않게 하고, 새 장비 한
/// 종은 실제 확률 2%(1.5배 전)를 넘지 않는다. 정의를 만드는 것은 <c>scripts/gen/items/build-drop-variety.py</c> 다.
/// </summary>
/// <remarks>
/// 실제 확률 = <c>DropRate × DropBoost(1.5) ÷ 목록 칸수</c>(<c>Formulas/monsterexp.cs</c> DetermineRandomDrop).
/// </remarks>
public sealed class DropVarietyTests
{
    private const double DropBoost = 1.5;

    /// <summary>새 장비 한 종의 실제 확률 윗선 — 1.5배 전 2%, 곱한 뒤 3%.</summary>
    private const double GearMost = 0.02 * DropBoost;

    private static readonly string[] AbelPotions = ["상급체력포션", "상급마력포션"];

    private static readonly int[] Abel = [20584, 20585, 20586, 20587, 20588, 20589, 20590, 20591, 20592, 20593, 20594];

    /// <summary>크라켄·킹아크퍼스는 `build-gear-drops.py` FIELD_BOSSES 가 한 칸짜리 목록으로 관리한다.</summary>
    private static readonly HashSet<string> Reserved = ["크라켄1", "크라켄2", "킹아크퍼스1", "킹아크퍼스2"];

    /// <summary>접미사(방어)·속성(공격) 장비의 앞머리.</summary>
    private static readonly string[] Suffixes =
        ["로오의", "이아의", "메투스의", "세토아의", "세오의", "셔스의", "칸의", "화염의", "바다의", "바람의", "대지의", "축복의", "체력의", "풍요의"];

    /// <summary>맵 · 입장 레벨 · 적어도 몇 부위(EquipmentSlot)의 접미사·속성 장비가 보여야 하나.</summary>
    private static readonly (int[] Maps, int Entry, int LeastSlots)[] Grounds =
    [
        ([20022, 20023, 20024], 11, 6),
        ([20263, 20264, 20265, 20266, 20267, 20268], 21, 6),
        ([20025, 20026], 51, 6),
        ([20020], 81, 5),
        ([20584, 20585, 20586, 20587, 20588], 51, 4),
    ];

    /// <summary>
    /// 사용자(2026-10-04) 「나오는 종류가 너무 적다 — 장비 부위별로 레벨에 맞게」: 사냥터마다 여러 부위의
    /// 접미사·속성 장비가 나오고, 하나도 입장 레벨보다 높아 못 입는 것이 없으며, 한 종은 2%(1.5배 전)를 넘지 않는다.
    /// </summary>
    [Fact]
    public void Each_later_ground_shows_suffix_gear_of_many_slots_at_its_level()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> wrong = [];

        foreach ((int[] maps, int entry, int leastSlots) in Grounds)
        {
            JsonNode[] here = [.. Monsters().Where(m => maps.Contains((int?)m["AreaID"] ?? 0) && !Reserved.Contains(Name(m)))];
            string[] gear =
            [
                .. here.SelectMany(Dropped).Distinct()
                    .Where(n => Suffixes.Any(n.StartsWith) && (int?)items[n]["EquipmentSlot"] is > 0)
                    // `build-gear-drops.py` 가 먼저 깐 한 벌은 그쪽 시험(GearDropTests)이 본다.
                    .Where(n => !maps.Any(map => GearDropTests.GroundGear[map].Contains(n))),
            ];
            int slots = gear.Select(n => (int?)items[n]["EquipmentSlot"]).Distinct().Count();

            if (slots < leastSlots)
            {
                wrong.Add($"{maps[0]}: {slots}부위(적어도 {leastSlots})");
            }

            wrong.AddRange(gear.Where(n => ((int?)items[n]["LevelRequired"] ?? 0) > entry)
                .Select(n => $"{n} 레벨 {items[n]["LevelRequired"]} > 입장 {entry}"));

            foreach (JsonNode monster in here)
            {
                string[] listed = [.. Dropped(monster)];

                foreach (string name in listed.Where(gear.Contains))
                {
                    double real = ((double?)items[name]["DropRate"] ?? 0) * DropBoost / listed.Length;
                    if (real > GearMost + 1e-9)
                    {
                        wrong.Add($"{Name(monster)} {name} {real:P1} > {GearMost:P0}");
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
