using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 원작 얼개는 「괴물이 잡템을 떨군다 → NPC 에 판다 → 그 돈으로 상점에서 장비를 산다」다. 가운데 고리가
/// 끊기면 — 잡템에 팔 값이 없으면 — 주워 봐야 짐만 는다.
/// </summary>
/// <remarks>
/// <para>
/// 상점이 부르는 값은 <c>database/server/scripts/Mundanes/shop1.cs</c> 가 <c>Value / 1.6</c> 을 정수로
/// 자른 것이고, 그 값이 0 이면 <c>case 0x0019</c> 가 <b>말없이 돌아간다</b> — 창은 열리고 "그렇게 하지요"
/// 는 눌리는데 돈이 들어오지 않는다. 그래서 여기서 보는 것은 <c>Value &gt; 0</c> 이 아니라 **상점이
/// 실제로 부를 값**이다.
/// </para>
/// <para>
/// 장비(<c>EquipmentSlot</c> 이 붙은 것)는 빼고 본다. 장비 값은 팩 셋이 열 배씩 어긋나 있어
/// (레더튜닉 300 / 950 / 5,000) 따로 정해야 하고, 이 시험이 볼 것은 **주워서 파는 잡템** 쪽이다.
/// </para>
/// </remarks>
public sealed class DropSaleValueTests
{
    /// <summary>상점이 물건을 사 주는 값. <c>shop1.cs</c> 의 <c>item.Template.Value / 1.6</c> 그대로다.</summary>
    private const double ShopOffer = 1.6;

    [Fact]
    public void A_drop_that_is_not_gear_sells_for_something()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> worthless = [];
        List<string> missing = [];

        foreach (string name in DroppedNames())
        {
            if (!items.TryGetValue(name, out JsonNode? item))
            {
                missing.Add(name);
                continue;
            }

            // 장비는 이 시험이 보지 않는다.
            if ((int?)item["EquipmentSlot"] is > 0)
            {
                continue;
            }

            int value = (int?)item["Value"] ?? 0;

            if ((int)(value / ShopOffer) <= 0)
            {
                worthless.Add($"{name}(Value {value})");
            }
        }

        Assert.True(worthless.Count == 0,
            $"괴물이 떨구는데 상점이 한 푼도 주지 않는 잡템이 {worthless.Count} 가지입니다: " +
            $"{string.Join(", ", worthless.Order())}. " +
            $"(템플릿이 없는 이름: {(missing.Count == 0 ? "없음" : string.Join(", ", missing.Order()))})");
    }

    /// <summary>괴물 정의가 <c>Drops</c> 에 이름을 올린 것 전부.</summary>
    private static IEnumerable<string> DroppedNames()
    {
        HashSet<string> names = [];

        foreach (JsonNode monster in Definitions(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "monsters")))
        {
            JsonNode? drops = monster["Drops"];
            JsonArray? listed = drops?["$values"] as JsonArray ?? drops as JsonArray;

            foreach (JsonNode? entry in listed ?? [])
            {
                if (entry?.GetValue<string>() is { Length: > 0 } name && name != "random")
                {
                    names.Add(name);
                }
            }
        }

        Assert.NotEmpty(names);
        return names;
    }

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

    /// <summary>
    /// 폴더 아래의 정의 전부. 하데스가 싣는 정의 하나는 JSON 이 아니고(<c>minions/minion.json</c> 이 그림을
    /// <c>0x40C5</c> 로 적는다) 서버 제 손으로 쓴 것에는 꼬리 쉼표가 남으므로, 너그럽게 읽고 못 읽는 것은
    /// 건너뛴다.
    /// </summary>
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
