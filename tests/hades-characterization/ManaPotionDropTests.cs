using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 사용자(2026-09-26) — "맵 전체에 마력 포션 드랍률 좀 높이고". 사냥터(노비스 포함)에 들어간 마력 포션
/// 세 가지의 <b>실제 확률을 그 전의 두 배</b>로 올렸다.
/// </summary>
/// <remarks>
/// <para>
/// 실제 확률 = <c>DropRate ÷ 목록 칸수</c>(<see cref="GearDropTests" />). 그래서 두 배는 곧 <c>DropRate</c> 두 배다.
/// 하급·중급마력포션은 0.6 이었으므로 1.2 — 1 을 넘는다. 옛 셈(목록에서 한 칸 고르고 그 칸을 굴린다)은
/// 한 칸이 1/칸수 를 넘을 수 없어 1.2 가 1.0 처럼 굴었다. 그래서 서버 셈(<c>Formulas/monsterexp.cs</c>
/// <c>DetermineRandomDrop</c>)을 <b>목록의 DropRate 를 이어 붙인 줄에서 한 점을 뽑는</b> 셈으로 바꿨다 —
/// 1 이하에서는 옛 셈과 같은 확률이고, 1 을 넘어도 <c>DropRate ÷ 칸수</c> 가 그대로 지켜진다. 단 한 괴물의
/// 목록 DropRate 합이 칸수를 넘으면 뒤쪽 물건이 밀려나므로 그것도 여기서 막는다.
/// </para>
/// </remarks>
public sealed class ManaPotionDropTests
{
    /// <summary>
    /// 2026-09-26 전의 DropRate. 지금 값은 그 <b>두 배 이상</b>이어야 한다 — 같은 날 드랍 종류를 늘리며
    /// (<c>scripts/build-drop-variety.py</c>) 목록 칸이 늘어난 만큼 <c>DropRate</c> 를 더 올려 실제 확률을
    /// 지켰으므로(하급마력 1.2 → 1.6 · 중급마력 1.2 → 1.5) 딱 두 배가 아니다.
    /// </summary>
    private static readonly (string Name, double Before)[] ManaPotions =
    [
        ("마라디움", 0.5),
        ("하급마력포션", 0.6),
        ("중급마력포션", 0.6),
    ];

    [Fact]
    public void Every_mana_potion_on_a_hunting_ground_drops_twice_as_often_as_before()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> wrong = [];

        foreach ((string name, double before) in ManaPotions)
        {
            double now = (double?)items[name]["DropRate"] ?? 0;

            if (now < (2 * before) - 1e-9)
            {
                wrong.Add($"{name} {now} (그 전 {before} → {2 * before} 이상이어야)");
            }
        }

        Assert.True(wrong.Count == 0, $"마력 포션 DropRate 가 두 배보다 낮습니다: {string.Join(", ", wrong)}");

        int carrying = Monsters().Count(m => Dropped(m).Any(n => ManaPotions.Any(p => p.Name == n)));
        Assert.True(carrying > 0, "마력 포션을 떨구는 괴물이 하나도 없습니다.");
    }

    /// <summary><c>Formulas/monsterexp.cs</c> <c>DropBoost</c> — 사용자 2026-09-26 "전체 확률 올려", 1.5배.</summary>
    private const double DropBoost = 1.5;

    [Fact]
    public void No_monster_list_adds_up_to_more_than_its_slots()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> over = [];

        foreach (JsonNode monster in Monsters())
        {
            string[] listed = [.. Dropped(monster)];

            if (listed.Length == 0)
            {
                continue;
            }

            // 서버가 굴릴 때 모든 DropRate 에 DropBoost(1.5)를 곱하므로(`monsterexp.cs`, 2026-09-26) 곱한 합으로 잰다.
            double sum = DropBoost * listed.Sum(n => items.TryGetValue(n, out JsonNode? item) ? (double?)item["DropRate"] ?? 0 : 0);

            if (sum > listed.Length + 1e-9)
            {
                over.Add($"{monster["Name"]}@{monster["AreaID"]} 합×1.5 {sum:F2} > {listed.Length}칸");
            }
        }

        Assert.True(over.Count == 0,
            $"DropRate 합이 목록 칸수를 넘는 괴물 — 뒤쪽 물건이 확률을 잃습니다: {string.Join(", ", over)}");
    }

    private static IEnumerable<string> Dropped(JsonNode monster)
    {
        JsonNode? drops = monster["Drops"];
        JsonArray? listed = drops?["$values"] as JsonArray ?? drops as JsonArray;

        foreach (JsonNode? entry in listed ?? [])
        {
            if (entry?.GetValue<string>() is { Length: > 0 } name && name != "random")
            {
                yield return name;
            }
        }
    }

    private static IEnumerable<JsonNode> Monsters() =>
        Definitions(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "monsters"));

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
