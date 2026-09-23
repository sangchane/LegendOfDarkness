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

    /// <summary>노비스 사냥터. 마을(20373)에는 주민만 있어 뺀다.</summary>
    private static readonly int[] NoviceGround =
    [
        20393, 20394,
        20380, 20381, 20382, 20383, 20384, 20385, 20386, 20387, 20388,
    ];

    /// <summary>
    /// 노비스가 처음 사 입는 옷. 값은 여기 박지 않고 템플릿에서 읽는다 — 원작 도감이 950 전이라 적었고
    /// (<see cref="OriginalItemValueTests" />) 그 값이 바뀌면 이 시험도 같이 움직여야 한다.
    /// </summary>
    private const string FirstTunic = "레더튜닉";

    /// <summary>
    /// 우리가 정한 시약 값. 근거는 <c>scripts/build-novice-drops.py</c> 머리글에 있다 — 도감 수치표에도
    /// 원작 아카이브에도 이 둘은 없고, 서버팩 셋 중 <b>혼든만</b> 값을 적었다(쿠룸 300 · 마라디움 1,000).
    /// 셋이 일치하지 않으므로 팩 값을 쓰지 않고, 혼든의 비(3 : 10)만 남긴 채 절반으로 내려
    /// 「첫 옷까지 열 마리 안팎」에 맞춘 값이다.
    /// </summary>
    private static readonly (string Name, int Value)[] Potions = [("쿠룸", 150), ("마라디움", 500)];

    /// <summary>「열 마리 안팎」의 폭. 목록 칸수가 둘인 괴물(브라운맨티스·지네)은 시약이 나올 확률이
    /// 그만큼 높아 아래쪽에, 잡템까지 셋인 괴물은 위쪽에 선다.</summary>
    private const double Fewest = 7;

    private const double Most = 14;

    /// <summary>
    /// 시약 값은 우리가 정한 값이어야 한다 — 근거가 없어 우리가 정한 자리라, 아무도 안 보면 말없이
    /// 팩 값으로 되돌아간다.
    /// </summary>
    [Fact]
    public void The_potions_a_novice_finds_are_priced_where_we_set_them()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> wrong = [];

        foreach ((string name, int value) in Potions)
        {
            Assert.True(items.ContainsKey(name), $"시약 «{name}» 의 아이템 정의가 없습니다.");

            if ((int?)items[name]["Value"] != value)
            {
                wrong.Add($"{name} {items[name]["Value"]}(정한 값 {value})");
            }
        }

        Assert.True(wrong.Count == 0,
            $"시약 값이 정한 값과 다릅니다: {string.Join(", ", wrong)}. " +
            "python3 scripts/build-novice-drops.py --쓰기 로 다시 만드세요.");
    }

    /// <summary>
    /// 초반 벌이의 크기를 잰다. 노비스 괴물 한 마리가 내놓는 것(금화 + 잡템 + 시약을 판 값)으로 첫 옷을
    /// 사려면 <b>열 마리 안팎</b>이어야 한다 — 사용자가 준 잣대다(2026-09-23).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>시약도 센다.</b> 마시는 물건이지만 상점이 사 주고, 실제로 초반 벌이는 시약이 끌고 간다.
    /// 빼고 세면 「첫 옷까지 몇 마리」가 서너 배로 부풀어 거짓말이 된다.
    /// </para>
    /// <para>
    /// 셈은 <c>Formulas/monsterexp.cs</c> 그대로다 — <c>LootQualifer.Random</c> 은 <c>Drops</c> 에서
    /// 하나를 같은 확률로 고른 뒤 그 물건의 <c>DropRate</c> 를 한 번 굴리므로 한 마리가 어떤 물건을
    /// 내놓을 확률은 <c>DropRate ÷ 목록 칸수</c> 다. 금화는 따로 <c>Gold × GoldChance</c> 로 온다.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_novice_pays_for_a_first_tunic_in_about_ten_kills()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        Assert.True(items.ContainsKey(FirstTunic), $"첫 옷 «{FirstTunic}» 의 아이템 정의가 없습니다.");
        int tunic = (int?)items[FirstTunic]["Value"] ?? 0;
        Assert.True(tunic > 0, $"«{FirstTunic}» 의 값이 0 입니다 — 몇 마리인지 셀 수 없습니다.");

        List<string> outside = [];
        int counted = 0;

        foreach (JsonNode monster in Definitions(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "monsters"))
                     .Where(m => NoviceGround.Contains((int?)m["AreaID"] ?? 0)))
        {
            counted++;
            double earned = ((int?)monster["Gold"] ?? 0) * (((int?)monster["GoldChance"] ?? 100) / 100.0);
            string[] drops = [.. Dropped(monster)];

            foreach (string name in drops.Where(items.ContainsKey))
            {
                // 장비는 입는 물건이라 벌이에 넣지 않는다.
                if ((int?)items[name]["EquipmentSlot"] is > 0)
                {
                    continue;
                }

                double odds = ((double?)items[name]["DropRate"] ?? 0) / drops.Length;
                earned += odds * (int)(((int?)items[name]["Value"] ?? 0) / ShopOffer);
            }

            double kills = earned > 0 ? tunic / earned : double.PositiveInfinity;

            if (kills < Fewest || kills > Most)
            {
                outside.Add($"{monster["Name"]}@{monster["AreaID"]} {kills:F1}마리(한 마리 {earned:F0}전)");
            }
        }

        Assert.True(counted > 0, "노비스 사냥터에 괴물 정의가 하나도 없습니다.");
        Assert.True(outside.Count == 0,
            $"첫 옷({FirstTunic} {tunic}전)까지 {Fewest:F0}~{Most:F0}마리 밖인 노비스 괴물이 {outside.Count} 마리입니다: " +
            $"{string.Join(", ", outside.Order())}. 잣대는 「열 마리 안팎」입니다 — " +
            "시약·잡템 값은 python3 scripts/build-novice-drops.py · scripts/build-pack-gold.py 가 정합니다.");
    }

    /// <summary>한 괴물 정의가 떨구겠다고 적어 둔 이름. <c>random</c> 은 이름이 아니라 낱말이다.</summary>
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
