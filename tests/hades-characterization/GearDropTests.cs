using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 장비를 얻는 길은 둘이고 그 둘은 레벨로 갈린다 — <b>저레벨은 사서 입고, 그 위는 가끔 주워 입는다.</b>
/// 사용자가 정한 것이다(2026-09-23): "괴물이 잡템만 남기는건 저레벨때나 그렇고 장비같은건 낮은 확률로
/// 드랍되긴해".
/// </summary>
/// <remarks>
/// <para>
/// 여기서 지키는 것은 세 가지다. 노비스 동선 괴물의 드롭 목록에 장비 이름이 <b>하나도 없다</b>,
/// 우드랜드·포테의숲 괴물에는 <b>있고</b> 실제로 떨어질 확률이 <b>1~5%</b> 안이다, 그리고 떨구라고 적힌
/// 이름이 <b>전부 실제 아이템</b>이다.
/// </para>
/// <para>
/// <b>왜 목록을 보고 확률을 안 보나.</b> 떨어질 확률은 괴물이 아니라 아이템 템플릿의 <c>DropRate</c> 에
/// 붙어 있다(<c>ItemTemplate.cs:93</c>). 같은 장비를 노비스 괴물과 우드랜드 괴물이 함께 떨구면 확률이
/// 같이 움직여, 확률로는 "노비스만 빼기"를 할 수 없다. 그래서 가르는 자리는 <b>목록</b>이다 —
/// 노비스 괴물의 <c>Drops</c> 에 장비 이름이 없으면 <c>DropRate</c> 가 얼마든 안 떨어진다.
/// </para>
/// <para>
/// <b>실제 확률 = <c>DropRate</c> ÷ 목록 칸수.</b> 하데스는 <c>Drops</c> 에서 하나를 같은 확률로 고른 뒤
/// 그 물건의 <c>DropRate</c> 를 한 번 굴린다(<c>Formulas/monsterexp.cs</c> DetermineRandomDrop). 그래서
/// 6% 짜리 장비가 두 칸짜리 목록에 있으면 3%, 세 칸이면 2% 다.
/// </para>
/// <para>
/// <b>용의발톱은 아무 데도 없어야 한다.</b> <c>LevelRequired 1</c> 에 피해 180~200, 값 0 이다 — 같은
/// 아이템이 Novaonline 팩에서는 레벨제한 99 다. 1레벨이 주우면 초반이 통째로 무너진다.
/// </para>
/// <para>정의를 만드는 것은 <c>scripts/build-gear-drops.py</c> 다.</para>
/// </remarks>
public sealed class GearDropTests
{
    /// <summary>하데스가 목록에서 하나를 골라 굴리는 갈래. <c>LootQualifer.Random</c>.</summary>
    private const int LootRandom = 1 << 1;

    /// <summary>5.99 팩이 장비를 떨구는 괴물에 쓰는 폭. 우리가 넣는 것도 이 안에 있어야 한다.</summary>
    private const double Least = 0.01;

    private const double Most = 0.05;

    /// <summary>잡템만 나오는 곳 — 노비스 동선과 우드랜드 첫 구역들(워프 레벨문이 1~22 이거나 없다).</summary>
    private static readonly int[] Early =
    [
        20373, 20393, 20394,
        20380, 20381, 20382, 20383, 20384, 20385, 20386, 20387, 20388,
        20015, 20016, 20017, 20022,
    ];

    /// <summary>장비가 나오는 곳. 맵과 그 사냥터 이름.</summary>
    private static readonly (int Map, string Name)[] Later =
    [
        (20023, "우드랜드3-1"), (20024, "우드랜드4-1"),
        (20025, "우드랜드5-1"), (20026, "우드랜드6-1"),
        (20020, "우드랜드14-1"),
        (20263, "포테의숲1존"), (20264, "포테의숲2존"), (20265, "포테의숲3존"),
        (20266, "포테의숲4존"), (20267, "포테의숲5존"), (20268, "포테의숲6존"),
    ];

    [Fact]
    public void Early_monsters_leave_junk_and_never_gear()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> wearing = [];

        foreach (JsonNode monster in Monsters().Where(m => Early.Contains((int?)m["AreaID"] ?? 0)))
        {
            foreach (string name in Dropped(monster).Where(name => IsGear(items, name)))
            {
                wearing.Add($"{monster["Name"]}@{monster["AreaID"]} → {name}");
            }
        }

        Assert.True(wearing.Count == 0,
            $"초반 사냥터 괴물이 장비를 떨굽니다 ({wearing.Count} 줄): {string.Join(", ", wearing.Order())}. " +
            "저레벨은 잡템을 팔아 상점에서 사 입는 것이 원작 얼개입니다 — " +
            "python3 scripts/build-gear-drops.py --쓰기 로 다시 만드세요.");
    }

    [Fact]
    public void Woodland_and_pote_monsters_leave_gear_between_one_and_five_percent()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> bare = [];
        List<string> outside = [];

        foreach ((int map, string ground) in Later)
        {
            JsonNode[] here = [.. Monsters().Where(m => (int?)m["AreaID"] == map)];
            Assert.True(here.Length > 0, $"{ground}({map}) 에 괴물 정의가 없습니다.");

            string[] gear = [.. here.SelectMany(m => Dropped(m).Where(name => IsNormalGear(items, name)))];

            if (gear.Length == 0)
            {
                bare.Add($"{ground}({map})");
                continue;
            }

            foreach (JsonNode monster in here)
            {
                string[] listed = [.. Dropped(monster)];

                foreach (string name in listed.Where(name => IsNormalGear(items, name)))
                {
                    // 목록에서 하나를 고르는 갈래여야 확률을 셀 수 있다.
                    Assert.True(((int?)monster["LootType"] & LootRandom) == LootRandom,
                        $"{ground} {monster["Name"]} 의 LootType 이 {monster["LootType"]} 입니다 — " +
                        $"장비를 실으려면 목록에서 하나를 고르는 갈래(Random {LootRandom})여야 합니다.");

                    double rate = (double?)items[name]["DropRate"] ?? 0;
                    double real = rate / listed.Length;

                    if (real < Least || real > Most)
                    {
                        outside.Add(
                            $"{ground} {monster["Name"]} → {name} {real:P1}" +
                            $"(DropRate {rate} ÷ {listed.Length}칸)");
                    }
                }
            }
        }

        Assert.True(bare.Count == 0,
            $"장비가 하나도 안 떨어지는 사냥터가 {bare.Count} 곳입니다: {string.Join(", ", bare)}.");
        Assert.True(outside.Count == 0,
            $"실제 확률이 {Least:P0}~{Most:P0} 밖인 장비 드롭이 {outside.Count} 줄입니다: " +
            $"{string.Join(", ", outside.Order())}.");
    }

    [Fact]
    public void Every_dropped_name_is_a_real_item()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> broken = [];

        foreach (JsonNode monster in Monsters())
        {
            foreach (string name in Dropped(monster).Where(name => !items.ContainsKey(name)))
            {
                broken.Add($"{monster["Name"]} → {name}");
            }
        }

        Assert.True(broken.Count == 0,
            $"그 이름의 아이템이 없어 영영 안 떨어지는 드롭 줄이 {broken.Count} 개입니다: " +
            $"{string.Join(", ", broken.Order())}.");
    }

    /// <summary>
    /// <b>1~5% 밖에 서도 되는 단 하나 — 이름을 적어 둔 예외다.</b> 폭을 늘리지 않고 여기 이름을 적는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 칸: 괴물 · 떨구는 것 · 확률. <b>자이언트맨티스 → 세줄금반지 80%</b> 는 5.99 팩이 손수 적은 것이고
    /// (<c>드롭아이템 80 세줄금반지</c>), 사냥터 한 벌처럼 우리가 고른 것이 아니다. Novaonline 은 같은
    /// 괴물에 100% 로 적었고 혼든에는 이 반지가 없다.
    /// </para>
    /// <para>
    /// <b>왜 1~5% 를 안 지켜도 되나.</b> 저것은 걸어 다니다 만나는 사냥터 괴물의 잣대다. 자이언트맨티스는
    /// 맵에 서 있지 않는다 — <c>scripts/Pack599/Npcs/포테의숲오솔길입장.cs</c> 가 포테의숲5존에서 열어
    /// 주는 <b>개인 던전</b>의 보스로 한 판에 한 마리만 불려 나온다(<c>mob_spawn3 … 1</c>). 입장이
    /// 52레벨 미만이고 안쪽 워프 레벨문이 21~52 라 <b>초반 동선(1~20)에서는 만날 수 없다</b>.
    /// 체력 19,500 · 피해 300~350 인 한 번짜리 보스에 80% 는 "문을 열면 거의 준다"는 뜻이다.
    /// </para>
    /// <para>아래 시험이 그 전제를 실제로 지킨다 — 이 물건이 걸어 다니는 사냥터에는 <b>한 줄도</b> 없어야 한다.</para>
    /// <para>
    /// <b>레벨제한은 원작 도감이 정본이다</b> — 세줄금반지는 도감에 <c>레벨제한 11</c> 로 적혀 있고
    /// (<c>판매가격 500000</c>) Novaonline 도 11 이다. 서버에 들어온 5.99 값만 30 이었다.
    /// </para>
    /// </remarks>
    private static readonly (string Beast, string Prize, double Rate, int Level)[] Bosses =
    [
        ("자이언트맨티스", "세줄금반지", 0.80, 11),
    ];

    [Fact]
    public void The_dungeon_boss_drops_what_the_pack_wrote_and_nothing_else_does()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();

        foreach ((string beast, string prize, double rate, int level) in Bosses)
        {
            Assert.True(items.ContainsKey(prize), $"«{prize}» 의 아이템 정의가 없습니다.");

            JsonNode[] carrying =
            [
                .. Monsters().Where(monster => Dropped(monster).Contains(prize)),
            ];

            Assert.True(carrying.Length == 1,
                $"«{prize}» 를 떨구는 괴물이 {carrying.Length} 마리입니다 — 팩에는 {beast} 하나뿐입니다: " +
                $"{string.Join(", ", carrying.Select(m => $"{m["Name"]}@{m["AreaID"]}").Order())}.");

            JsonNode boss = carrying[0];
            Assert.Equal(beast, boss["Name"]?.GetValue<string>());

            // 목록이 한 칸이어야 `DropRate` 가 그대로 확률이 된다.
            string[] listed = [.. Dropped(boss)];
            Assert.True(listed.Length == 1,
                $"{beast} 의 드롭 목록이 {listed.Length} 칸입니다 ({string.Join(", ", listed)}) — " +
                $"칸이 늘면 실제 확률이 {rate:P0} 가 아니라 그 나눗셈이 됩니다.");

            // Table 갈래는 `DropRate` 를 가중치로 써서 확률을 셀 수 없다.
            Assert.True(((int?)boss["LootType"] & LootRandom) == LootRandom,
                $"{beast} 의 LootType 이 {boss["LootType"]} 입니다 — 확률을 세려면 Random({LootRandom}) 이어야 합니다.");

            Assert.Equal(rate, (double?)items[prize]["DropRate"] ?? 0);

            // 도감 값. 떨어져도 못 끼면 떨어지지 않은 것과 같다.
            Assert.True((int?)items[prize]["LevelRequired"] == level,
                $"«{prize}» 의 레벨제한이 {items[prize]["LevelRequired"]} 입니다 — 원작 도감은 {level} 입니다.");

            // 이 예외가 걸어 다니는 사냥터로 새면 1~5% 잣대가 뚫린다.
            Assert.True((int?)boss["AreaID"] is null or 0,
                $"{beast} 가 맵 {boss["AreaID"]} 에 서 있습니다 — 개인 던전 스크립트가 불러 세우는 보스라야 " +
                "1~5% 잣대 밖에 설 수 있습니다.");

            string[] walked =
            [
                .. Monsters()
                    .Where(m => Early.Contains((int?)m["AreaID"] ?? -1)
                        || Later.Any(l => l.Map == ((int?)m["AreaID"] ?? -1)))
                    .Where(m => Dropped(m).Contains(prize))
                    .Select(m => $"{m["Name"]}@{m["AreaID"]}"),
            ];

            Assert.True(walked.Length == 0,
                $"걸어 다니는 사냥터 괴물이 «{prize}» ({rate:P0}) 를 떨굽니다: {string.Join(", ", walked.Order())}. " +
                "그 폭은 1~5% 입니다 — 예외는 개인 던전 보스뿐입니다.");
        }
    }

    /// <summary>
    /// 노비스 괴물에 장비를 달았다가 뗀 자국이 아이템 쪽에 남아 있었다 — 아무 목록에도 없는 9종에
    /// <c>DropRate 0.1</c> 이 그대로였다. 떨어지지는 않지만 「이건 왜 0.1 이지」로 읽힌다.
    /// </summary>
    /// <remarks>
    /// <b>우리가 들여온 5.99 장비만 본다.</b> 하데스가 제 손으로 쓴 영문 아이템 쪽에도 아무도 안 굴리는
    /// <c>DropRate</c> 가 900장 넘게 남아 있는데, 그것은 우리 것이 아니라 건드리지 않는다.
    /// </remarks>
    [Fact]
    public void No_gear_we_brought_in_keeps_a_drop_rate_nobody_rolls()
    {
        HashSet<string> listed = [.. Monsters().SelectMany(Dropped)];
        List<string> leftover = [];

        foreach ((string name, JsonNode item) in Items())
        {
            if (listed.Contains(name)
                || item["Group"]?.GetValue<string>()?.StartsWith("5.99표/") != true
                || (int?)item["EquipmentSlot"] is not > 0
                || item["DropRate"] is null)
            {
                continue;
            }

            leftover.Add($"{name}({item["DropRate"]})");
        }

        Assert.True(leftover.Count == 0,
            $"아무도 안 떨구는데 DropRate 가 남은 5.99 장비가 {leftover.Count} 종입니다: " +
            $"{string.Join(", ", leftover.Order())}. " +
            "python3 scripts/build-gear-drops.py --쓰기 로 다시 만드세요.");
    }

    /// <summary>방어 접미사 — 사냥터 드랍 전용(사용자 결정 2026-09-24). 상점 쪽은 <c>TownGearShopTests</c>.</summary>
    private static readonly string[] DefenseSuffixGear = ["로오의반지", "칸의목걸이"];

    /// <summary>공격 속성 — 상점과 사냥터 둘 다(같은 결정).</summary>
    private static readonly string[] AttackElementGear =
    [
        "대지의목걸이", "대지의벨트", "바다의목걸이", "바다의벨트",
        "바람의목걸이", "바람의벨트", "화염의목걸이", "화염의벨트",
    ];

    /// <summary>일반 장비(1~5%)보다 드물게 — 한 마리당 0.5~1%.</summary>
    private const double RareLeast = 0.005;

    private const double RareMost = 0.01;

    [Fact]
    public void Later_grounds_drop_defense_suffix_and_attack_element_gear_more_rarely_than_normal_gear()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> missingSuffix = [];
        List<string> missingElement = [];
        List<string> outside = [];

        foreach ((int map, string ground) in Later)
        {
            JsonNode[] here = [.. Monsters().Where(m => (int?)m["AreaID"] == map)];

            foreach (JsonNode monster in here)
            {
                string[] listed = [.. Dropped(monster)];
                string? suffix = listed.FirstOrDefault(n => DefenseSuffixGear.Contains(n));
                string? element = listed.FirstOrDefault(n => AttackElementGear.Contains(n));

                if (suffix is null)
                {
                    missingSuffix.Add($"{ground} {monster["Name"]}");
                    continue;
                }

                if (element is null)
                {
                    missingElement.Add($"{ground} {monster["Name"]}");
                    continue;
                }

                foreach (string name in new[] { suffix, element })
                {
                    double rate = (double?)items[name]["DropRate"] ?? 0;
                    double real = rate / listed.Length;

                    if (real < RareLeast || real > RareMost)
                    {
                        outside.Add($"{ground} {monster["Name"]} → {name} {real:P2}(DropRate {rate} ÷ {listed.Length}칸)");
                    }
                }
            }
        }

        Assert.True(missingSuffix.Count == 0,
            $"방어 접미사 장비가 없는 장비 사냥터 괴물이 {missingSuffix.Count}마리입니다: {string.Join(", ", missingSuffix.Order())}.");
        Assert.True(missingElement.Count == 0,
            $"공격 속성 장비가 없는 장비 사냥터 괴물이 {missingElement.Count}마리입니다: {string.Join(", ", missingElement.Order())}.");
        Assert.True(outside.Count == 0,
            $"접미사·속성 장비의 실제 확률이 {RareLeast:P1}~{RareMost:P1} 밖인 줄이 {outside.Count}개입니다: "
            + $"{string.Join(", ", outside.Order())}.");
    }

    [Fact]
    public void Nothing_drops_the_dragon_claw()
    {
        List<string> carrying =
        [
            .. Monsters()
                .Where(monster => Dropped(monster).Contains("용의발톱"))
                .Select(monster => $"{monster["Name"]}@{monster["AreaID"]}"),
        ];

        Assert.True(carrying.Count == 0,
            $"용의발톱(레벨제한 1 · 피해 180~200 · 값 0)을 떨구는 괴물이 {carrying.Count} 마리입니다: " +
            $"{string.Join(", ", carrying.Order())}.");
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

    private static bool IsGear(IReadOnlyDictionary<string, JsonNode> items, string name) =>
        items.TryGetValue(name, out JsonNode? item) && (int?)item["EquipmentSlot"] is > 0;

    /// <summary>
    /// 일반 장비만 — 방어 접미사·공격 속성은 <see cref="DefenseSuffixGear"/>·<see cref="AttackElementGear"/>
    /// 가 따로 보는 더 드문 확률(0.5~1%)이라 여기서는 뺀다.
    /// </summary>
    private static bool IsNormalGear(IReadOnlyDictionary<string, JsonNode> items, string name) =>
        IsGear(items, name) && !DefenseSuffixGear.Contains(name) && !AttackElementGear.Contains(name);

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

    /// <summary>
    /// 폴더 아래의 정의 전부. 하데스가 제 손으로 쓴 것에는 꼬리 쉼표가 남아 있으므로 너그럽게 읽고,
    /// 못 읽는 것은 건너뛴다(<see cref="DropSaleValueTests" /> 와 같은 규칙).
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
