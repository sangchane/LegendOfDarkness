using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 아이템의 <c>DropRate</c> 만으로는 어느 괴물이 그것을 떨구는지 알 수 없다. 배치는 이미 괴물
/// 템플릿의 <c>Drops</c> 에 적힌 연결만 근거로 삼고, 죽어 있는 연결은 5.99 원본
/// <c>mobs.json</c> 에 같은 괴물-아이템과 양수 확률이 있을 때만 살린다.
/// </summary>
public sealed class EvidenceBackedDropDistributionTests
{
    private const int ItemTemplateCount = 1364;
    private const int ReferencedItemTypeCount = 299;   // 부위별 접미사·속성·축복·체력·풍요 장비(2026-10-04, build-drop-variety.py)
    private const int DropRelationCount = 1680;        // 723 + 부위별 장비(2026-10-04, build-drop-variety.py)
    private const int LootRandom = 2;
    private const int LootTable = 4;

    private static readonly IReadOnlyDictionary<string, double> EvidenceRates =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["2갱도열쇠"] = 0.0666,
            ["3갱도열쇠"] = 0.0666,
            ["가위"] = 0.0133,
            ["거북이등껍질"] = 1.2,
            ["고사목뿌리"] = 0.2,
            ["그래브의집게"] = 1.2,
            ["바크의척추뼈"] = 0.8,
            ["엑스쿠라눔"] = 0.02,
            ["좀비의막대기"] = 0.2,
            ["좀비의살"] = 0.1333,
            ["좀비지팡이"] = 0.2,
            ["크리스마스얼음"] = 0.2,
            ["킹아크퍼스의팬던트"] = 0.2666,
            ["퐁퐁이의점액질"] = 1.2,
        };

    [Fact]
    public void Every_monster_drop_name_resolves_to_an_item_template()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        DropRelation[] missing = [.. Relations().Where(relation => !items.ContainsKey(relation.Item))];

        Assert.True(missing.Length == 0,
            $"괴물 Drops가 정의되지 않은 아이템을 {missing.Length}번 가리킵니다: " +
            string.Join(", ", missing.Select(Describe).Order()));
    }

    [Fact]
    public void Every_referenced_item_has_a_positive_drop_rate()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        string[] nonfunctional =
        [
            .. Relations()
                .Select(relation => relation.Item)
                .Distinct(StringComparer.Ordinal)
                .Where(name => !items.TryGetValue(name, out JsonNode? item) || DropRate(item) <= 0)
                .Order(),
        ];

        string[] details =
        [
            .. nonfunctional.Select(name =>
            {
                int uses = Relations().Count(relation => relation.Item == name);
                string rate = items.TryGetValue(name, out JsonNode? item)
                    ? DropRate(item).ToString(CultureInfo.InvariantCulture)
                    : "템플릿 없음";
                return $"{name}(DropRate {rate}, {uses}개 연결)";
            }),
        ];

        Assert.True(nonfunctional.Length == 0,
            $"Drops에 있지만 실제로 떨어질 수 없는 아이템이 {nonfunctional.Length}종입니다: " +
            string.Join(", ", details));
    }

    /// <summary>
    /// 현재 배치 자체가 허용 목록이다. 양수 <c>DropRate</c> 가 남은 미연결 아이템을 보고 새 연결을
    /// 만들면 이 299종·1680개 스냅샷이 바뀐다.
    /// </summary>
    [Fact]
    public void Drop_connections_are_not_synthesized_from_item_drop_rates()
    {
        JsonNode[] templates = ItemTemplates();
        DropRelation[] relations = Relations();

        Assert.Equal(ItemTemplateCount, templates.Length);
        Assert.Equal(ReferencedItemTypeCount,
            relations.Select(relation => relation.Item).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(DropRelationCount, relations.Length);
        Assert.Equal(54, relations.Count(relation =>
            relation.Area is >= 20455 and <= 20465 && relation.Item == "골드아쿠아링"));
    }

    /// <summary>
    /// RED 상태의 14종을 살릴 수 있는 근거를 확인한다. 5.99 추출기는 첫 드랍을 문자열 두 칸으로,
    /// 같은 괴물의 둘째 이후 드랍을 중첩 배열로 보존하므로 둘을 모두 읽는다.
    /// </summary>
    [Fact]
    public void Every_nonfunctional_relation_has_the_same_monster_item_evidence_in_599()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        HashSet<(string Monster, string Item)> evidence = PackEvidence();
        DropRelation[] unsupported =
        [
            .. Relations()
                .Where(relation => !items.TryGetValue(relation.Item, out JsonNode? item) || DropRate(item) <= 0)
                .Where(relation => !evidence.Contains((relation.Monster, relation.Item)))
                .Distinct()
                .OrderBy(relation => relation.Monster)
                .ThenBy(relation => relation.Item),
        ];

        Assert.True(unsupported.Length == 0,
            $"죽은 드랍 연결 중 5.99 mobs.json에 같은 괴물-아이템 양수 확률 근거가 없는 것이 " +
            $"{unsupported.Length}개입니다: {string.Join(", ", unsupported.Select(Describe))}");
    }

    [Fact]
    public void Recovered_rates_and_singleton_random_semantics_stay_canonical()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();

        foreach ((string name, double expected) in EvidenceRates)
        {
            Assert.Equal(expected, DropRate(items[name]), precision: 4);
        }

        DropRelation[] singleton =
        [
            .. Relations().Where(relation =>
                EvidenceRates.ContainsKey(relation.Item) && relation.ListCount == 1),
        ];

        Assert.Equal(139, singleton.Length);
        Assert.All(singleton, relation =>
        {
            Assert.True((relation.LootType & LootRandom) != 0, $"{Describe(relation)}가 Random이 아닙니다.");
            Assert.True((relation.LootType & LootTable) == 0, $"{Describe(relation)}가 아직 Table입니다.");
        });
    }

    private static string Describe(DropRelation relation) =>
        $"{relation.Monster}@{relation.Area} → {relation.Item}";

    private static double DropRate(JsonNode item) => (double?)item["DropRate"] ?? 0;

    private static DropRelation[] Relations() => _relations ??=
    [
        .. MonsterTemplates().SelectMany(monster => Dropped(monster.Node).Select(item =>
            new DropRelation(
                monster.Node["Name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(monster.Path),
                (int?)monster.Node["AreaID"] ?? 0,
                item,
                Dropped(monster.Node).Count(),
                (int?)monster.Node["LootType"] ?? 0))),
    ];

    private static DropRelation[]? _relations;

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

    private static IReadOnlyDictionary<string, JsonNode> Items() => _items ??=
        ItemTemplates().ToDictionary(
            item => item["Name"]?.GetValue<string>()
                ?? throw new InvalidDataException("Name이 없는 아이템 템플릿이 있습니다."),
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, JsonNode>? _items;

    private static JsonNode[] ItemTemplates() => _itemTemplates ??=
        [.. Definitions(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "items")).Select(file => file.Node)];

    private static JsonNode[]? _itemTemplates;

    private static Definition[] MonsterTemplates() => _monsterTemplates ??=
        [.. Definitions(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "monsters"))];

    private static Definition[]? _monsterTemplates;

    private static HashSet<(string Monster, string Item)> PackEvidence()
    {
        string path = Path.Combine(
            HadesWorkspace.RepositoryRoot, "data", "server-packs", "extracted", "5.99-server", "mobs.json");
        JsonArray monsters = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        HashSet<(string Monster, string Item)> evidence = [];

        foreach (JsonNode? monster in monsters)
        {
            if (monster?["이름"]?.GetValue<string>() is not { Length: > 0 } name)
            {
                continue;
            }

            foreach ((double rate, string item) in EvidenceDrops(monster["fields"]?["드롭아이템"]))
            {
                if (rate > 0)
                {
                    evidence.Add((name, item));
                }
            }
        }

        return evidence;
    }

    private static IEnumerable<(double Rate, string Item)> EvidenceDrops(JsonNode? node)
    {
        if (node is not JsonArray values)
        {
            yield break;
        }

        if (values.Count >= 2
            && values[0]?.GetValue<string>() is { } rateText
            && double.TryParse(rateText, NumberStyles.Float, CultureInfo.InvariantCulture, out double rate)
            && values[1]?.GetValue<string>() is { Length: > 0 } item)
        {
            yield return (rate, item);
        }

        foreach (JsonArray nested in values.OfType<JsonArray>())
        {
            foreach ((double nestedRate, string nestedItem) in EvidenceDrops(nested))
            {
                yield return (nestedRate, nestedItem);
            }
        }
    }

    private static IEnumerable<Definition> Definitions(string folder)
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
                // 서버와 기존 드랍 characterization 테스트가 읽지 못하는 옛 템플릿을 건너뛰는 규칙과 같다.
                continue;
            }

            if (node is not null)
            {
                yield return new Definition(path, node);
            }
        }
    }

    private sealed record Definition(string Path, JsonNode Node);

    private sealed record DropRelation(string Monster, int Area, string Item, int ListCount, int LootType);
}
