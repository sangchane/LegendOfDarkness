using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 장비를 얻는 길은 둘이고 그 둘은 레벨로 갈린다 — <b>기본템은 상점에서만 사고, 사냥터는 레벨이 맞는
/// 접미사·속성 장비만 가끔 준다.</b> 사용자가 정한 것이다(2026-09-23 "낮은 확률로 드랍되긴해" →
/// 2026-09-25 "기본템을 빼고 그 자리에 접미사·속성 장비를 넣어라, 사냥터마다 레벨이 맞게").
/// </summary>
/// <remarks>
/// <para>
/// 여기서 지키는 것은 넷이다. 노비스 동선 괴물의 드롭 목록에 장비 이름이 <b>하나도 없다</b>,
/// 우드랜드·포테의숲 괴물의 드롭 목록에 <b>기본템(달마티카·단도복 같은 접미사 없는 것)이 하나도 없다</b>,
/// 대신 <b>그 사냥터 레벨에 맞는 접미사·속성 장비</b>가 옛 기본템 자리와 같은 확률로 있다, 그리고 떨구라고
/// 적힌 이름이 <b>전부 실제 아이템</b>이다.
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
/// <b>왜 사냥터마다 다른 장비인가.</b> 접미사(사람이름) 반지·목걸이는 11~12레벨 한 층뿐이다(하데스표
/// 496종을 다 훑어도 이 계열은 5~16레벨 보석 등급이 전부다, <c>data/pack-compare/한글이름-검토.tsv</c>,
/// <c>scripts/gen/items/build-suffix-gear-ko.py</c>). 그래서 우드랜드3-4(11레벨 대)만 접미사 반지를 쓰고, 그 위
/// 세 사냥터는 이미 층이 있는 4원소 공격 속성 장비를 그 사냥터 레벨에 맞춰 쓴다(자세한 근거는
/// <c>scripts/gen/items/build-gear-drops.py</c> <c>TIERS</c> 주석).
/// </para>
/// <para>
/// <b>용의발톱은 아무 데도 없어야 한다.</b> <c>LevelRequired 1</c> 에 피해 180~200, 값 0 이다 — 같은
/// 아이템이 Novaonline 팩에서는 레벨제한 99 다. 1레벨이 주우면 초반이 통째로 무너진다.
/// </para>
/// <para>정의를 만드는 것은 <c>scripts/gen/items/build-gear-drops.py</c> 다.</para>
/// <para>
/// <b>2026-10-09 서클대로</b> — 사냥터 장비는 그 사냥터 서클 레벨 폭 안의 접두 장비뿐이고(<see cref="GroundCircle" />), 위
/// 「사냥터마다 다른 한 벌」은 <c>build-drop-variety.py</c> 가 서클 한 벌로 바꿔 끼운다. 그래서 한 종의 확률 대신 괴물마다 장비 합을 잰다.
/// </para>
/// </remarks>
public sealed class GearDropTests
{
    /// <summary>하데스가 목록에서 하나를 골라 굴리는 갈래. <c>LootQualifer.Random</c>.</summary>
    private const int LootRandom = 1 << 1;

    /// <summary>잡템만 나오는 곳 — 노비스 동선과 우드랜드 첫 구역들(워프 레벨문이 1~10 이거나 없다).</summary>
    private static readonly int[] Early =
    [
        20373, 20393, 20394,
        20380, 20381, 20382, 20383, 20384, 20385, 20386, 20387, 20388,
        20015, 20016, 20017,
        20834, 20846, // 서·북의우드랜드1-1(2026-10-04) — 노바엔 레벨문이 없어 동의 1-1 을 따른다
    ];

    /// <summary>
    /// 장비가 나오는 곳. 맵과 그 사냥터 이름. 우드랜드2-1 은 2026-09-25 에 더했다 — 워프 레벨문이
    /// 11(`warps.json` 우드랜드입구→우드랜드2-1)이라 우드3-4 와 같은 대접이다. 아벨해안 일반 몹(그래브·
    /// 문어·바크·슬러그·애스코모이드·일·터틀·퐁퐁이)도 같은 날 더했다 — 크라켄·킹아크퍼스는 같은 맵에
    /// 서 있어도 <c>FIELD_BOSSES</c> 가 따로 관리하므로 <see cref="ReservedNames"/> 로 뺀다.
    /// </summary>
    private static readonly (int Map, string Name)[] Later =
    [
        (20022, "우드랜드2-1"), (20023, "우드랜드3-1"), (20024, "우드랜드4-1"),
        (20025, "우드랜드5-1"), (20026, "우드랜드6-1"),
        (20020, "우드랜드14-1"),
        (20263, "포테의숲1존"), (20264, "포테의숲2존"), (20265, "포테의숲3존"),
        (20266, "포테의숲4존"), (20267, "포테의숲5존"), (20268, "포테의숲6존"),
        (20584, "아벨해안1-a"), (20585, "아벨해안1-b"),
        (20586, "아벨해안2-a"), (20587, "아벨해안2-b"), (20588, "아벨해안2-c"),
        (20589, "아벨해안3-a"), (20590, "아벨해안3-b"), (20591, "아벨해안3-c"),
        (20592, "아벨해안4-a"), (20593, "아벨해안4-b"), (20594, "아벨해안4-c"),
        // 서·북의우드랜드 2-1~20-1(2026-10-04, build-woodland-west-north.py) — 맵 이름에서 찾는다.
        .. WoodlandWestNorthTests.Layered().Select(zone => (zone.Map, zone.Name)),
    ];

    /// <summary><c>FIELD_BOSSES</c> 가 관리하는 이름 — 아벨해안 일반 몹 잣대에서 뺀다.</summary>
    private static readonly string[] ReservedNames = ["크라켄1", "크라켄2", "킹아크퍼스1", "킹아크퍼스2"];

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
            "python3 scripts/gen/items/build-gear-drops.py --쓰기 로 다시 만드세요.");
    }

    /// <summary>접두 장비(방어 접미사·공격 속성·축복·체력·풍요)의 앞머리.</summary>
    internal static readonly string[] SuffixHeads =
        ["로오의", "이아의", "메투스의", "세토아의", "세오의", "셔스의", "칸의", "화염의", "바다의", "바람의", "대지의", "축복의", "체력의", "풍요의"];

    /// <summary>
    /// 사냥터마다 그 서클(1~10 · 11~40 · 41~70 · 71~98 · 99, <c>docs/item-prices-by-circle.md</c>)의 레벨 폭 — 입장 레벨이 든 서클.
    /// 사용자 2026-10-09 「마을별로 서클 기본템 팔듯이 사냥터도 서클별로 접두사 붙은 아이템 나오게」·「71,81,99는 해당 서클 존에서」:
    /// 사냥터 장비는 이 폭 안 레벨의 접두 장비뿐이다(<c>scripts/gen/items/build-drop-variety.py</c> <c>fill_gear</c>).
    /// <c>build-gear-drops.py</c> 가 까는 한 벌(은제방패 등)은 이제 장비 칸 자리일 뿐 — 그 생성기가 서클 한 벌로 바꿔 끼운다.
    /// </summary>
    internal static readonly Dictionary<int, (int Low, int High)> GroundCircle = WithWestNorth(new()
    {
        [20022] = (11, 40), [20023] = (11, 40), [20024] = (11, 40),
        [20263] = (11, 40), [20264] = (11, 40), [20265] = (11, 40),
        [20266] = (11, 40), [20267] = (11, 40), [20268] = (11, 40),
        [20025] = (41, 70), [20026] = (41, 70),
        [20020] = (71, 98),
        [20584] = (41, 70), [20585] = (41, 70), [20586] = (41, 70), [20587] = (41, 70), [20588] = (41, 70),
        [20589] = (41, 70), [20590] = (41, 70), [20591] = (41, 70), [20592] = (41, 70), [20593] = (41, 70),
        [20594] = (41, 70),
    });

    /// <summary>서·북의우드랜드(2026-10-04) — 구역 층(11·26·41·56·71·86)이 든 서클.</summary>
    private static Dictionary<int, (int Low, int High)> WithWestNorth(Dictionary<int, (int Low, int High)> grounds)
    {
        foreach ((int map, _, int layer) in WoodlandWestNorthTests.Layered())
        {
            grounds[map] = layer switch
            {
                < 41 => (11, 40),
                < 71 => (41, 70),
                _ => (71, 98),
            };
        }
        return grounds;
    }

    /// <summary>
    /// 한 괴물이 장비를 떨굴 확률(1.5배 뒤, 장비 칸 전부의 합)의 폭. 2026-10-09 전에는 한 벌 한 종이 1~6%(1.5배 전) 였다 —
    /// 이제 장비 칸이 모두 같은 DropRate 라 한 종이 아니라 괴물마다 합을 잰다(바꾸기 전 4.6~10%, 뒤 4.6~8.9%).
    /// </summary>
    private const double GroundGearLeast = 0.03;

    private const double GroundGearMost = 0.12;

    [Fact]
    public void Later_grounds_carry_only_prefixed_gear_of_their_circle()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        List<string> wrong = [];
        List<string> missing = [];
        List<string> outside = [];

        foreach ((int map, string ground) in Later)
        {
            JsonNode[] here =
            [
                .. Monsters().Where(m => (int?)m["AreaID"] == map
                    && !ReservedNames.Contains(m["Name"]?.GetValue<string>())),
            ];
            Assert.True(here.Length > 0, $"{ground}({map}) 에 괴물 정의가 없습니다.");
            (int low, int high) = GroundCircle[map];

            foreach (JsonNode monster in here)
            {
                string[] listed = [.. Dropped(monster)];
                string[] gear = [.. listed.Where(name => IsGear(items, name))];

                // 접두 없는 장비(기본템·표로 고친 84종)는 상점 몫이고, 서클 밖 레벨은 다른 서클 사냥터 몫이다.
                wrong.AddRange(gear
                    .Where(name => !SuffixHeads.Any(name.StartsWith)
                        || (int?)items[name]["LevelRequired"] is not { } level || level < low || level > high)
                    .Select(name => $"{ground} {monster["Name"]} → {name}(레벨 {items[name]["LevelRequired"]}, 서클 {low}~{high})"));

                if (gear.Length == 0)
                {
                    missing.Add($"{ground} {monster["Name"]}");
                    continue;
                }

                // 목록에서 하나를 고르는 갈래여야 확률을 셀 수 있다.
                Assert.True(((int?)monster["LootType"] & LootRandom) == LootRandom,
                    $"{ground} {monster["Name"]} 의 LootType 이 {monster["LootType"]} 입니다 — " +
                    $"장비를 실으려면 목록에서 하나를 고르는 갈래(Random {LootRandom})여야 합니다.");

                double real = gear.Sum(name => DropBoost * ((double?)items[name]["DropRate"] ?? 0)) / listed.Length;

                if (real < GroundGearLeast || real > GroundGearMost)
                {
                    outside.Add($"{ground} {monster["Name"]} 장비 합 {real:P1}({gear.Length}/{listed.Length}칸)");
                }
            }
        }

        Assert.True(wrong.Count == 0,
            $"서클 밖이거나 접두 없는 장비 드롭 줄이 {wrong.Count}개입니다: {string.Join(", ", wrong.Order())}. " +
            "python3 scripts/gen/items/build-drop-variety.py --쓰기 로 다시 만드세요.");
        Assert.True(missing.Count == 0,
            $"장비를 하나도 안 떨구는 사냥터 괴물이 {missing.Count}마리입니다: {string.Join(", ", missing.Order())}.");
        Assert.True(outside.Count == 0,
            $"장비 확률 합이 {GroundGearLeast:P0}~{GroundGearMost:P0} 밖인 괴물이 {outside.Count}마리입니다: " +
            $"{string.Join(", ", outside.Order())}.");
    }

    /// <summary><c>Formulas/monsterexp.cs</c> <c>DropBoost</c> — 사용자 2026-09-26 "전체 확률 올려", 1.5배.</summary>
    private const double DropBoost = 1.5;

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

            // 실제 확률 = DropRate × 1.5(`monsterexp.cs` DropBoost, 2026-09-26). 1.5배 뒤 120% 라 한 괴물 합 80% 상한
            // (`scripts/gen/items/build-drop-cap.py`)이 DropRate 를 0.5333 으로 내렸고, 그 실제 확률이 팩이 적은 80% 그대로다.
            Assert.Equal(rate, Math.Round(1.5 * ((double?)items[prize]["DropRate"] ?? 0), 3));

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
            "python3 scripts/gen/items/build-gear-drops.py --쓰기 로 다시 만드세요.");
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

    /// <summary>
    /// 되살린 접미사 장비 넷을 원작 표 값과 맞대본다 — 표는
    /// <c>data/game-data/items-original-sheets.json</c>(생성기 <c>scripts/gen/items/build-suffix-gear-from-sheet.py</c>).
    /// 넷 다 <c>Group</c> 이 <c>하데스표/...</c> 인 것만 골랐다 — 5.99 팩 자기 물건(<c>5.99표/...</c>)은
    /// 아래 <see cref="Sheet_never_touches_589_packs_own_gear"/> 가 따로 본다.
    /// </summary>
    [Fact]
    public void Restored_suffix_gear_matches_the_original_sheet()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();
        IReadOnlyDictionary<string, JsonNode> sheet = OriginalSheet();

        string[] sample = ["이아의호안석반지", "로오의동각반", "로오의은제방패", "칸의호안석반지"];

        foreach (string name in sample)
        {
            Assert.True(sheet.ContainsKey(name), $"원작 표에 «{name}» 줄이 없습니다.");
            Assert.True(items.ContainsKey(name), $"«{name}» 의 아이템 정의가 없습니다.");

            JsonNode row = sheet[name];
            JsonNode item = items[name];

            Assert.True(int.Parse(row["레벨제한"]!.GetValue<string>()) == (int?)item["LevelRequired"],
                $"«{name}» 레벨제한 — 표 {row["레벨제한"]} vs 템플릿 {item["LevelRequired"]}.");
            Assert.True(int.Parse(row["판매가격"]!.GetValue<string>()) == (int?)item["Value"],
                $"«{name}» 판매가격 — 표 {row["판매가격"]} vs 템플릿 {item["Value"]}.");
            Assert.True(int.Parse(row["내구력"]!.GetValue<string>()) == (int?)item["MaxDurability"],
                $"«{name}» 내구력 — 표 {row["내구력"]} vs 템플릿 {item["MaxDurability"]}.");
            Assert.True(int.Parse(row["무게"]!.GetValue<string>()) == (int?)item["CarryWeight"],
                $"«{name}» 무게 — 표 {row["무게"]} vs 템플릿 {item["CarryWeight"]}.");
        }
    }

    /// <summary>
    /// 표로 되살리다가 한 번 밟은 덫 — 로오의반지·칸의목걸이는 이름이 접미사 모양이지만 5.99 팩 자기
    /// 물건이다(<c>db/item/Armor/공통반지.txt</c>·<c>공통목걸이.txt</c>, <c>Group: "5.99표/..."</c>).
    /// 2026-09-25 <c>build-suffix-gear-from-sheet.py</c> 가 이 둘까지 표 값(레벨1·능력치 없음)으로 덮어
    /// <c>OriginalItemValueTests.The_gold_ring_the_mantis_drops_is_worth_what_the_sheet_says</c> 를
    /// 깼다 — 값이 500(팩)에서 200(표)으로 바뀌었었다. 이제 생성기가 <c>Group</c> 이 <c>5.99표/</c> 로
    /// 시작하면 건드리지 않으니, 원래 값(레벨11·체력·마력 있음)이 그대로인지 여기서 지킨다.
    /// </summary>
    [Fact]
    public void Sheet_never_touches_589_packs_own_gear()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();

        Assert.Equal(1, (int?)items["로오의반지"]["LevelRequired"]); // 레벨만 원작 표대로(사용자 2026-10-04)
        Assert.Equal(500, (int?)items["로오의반지"]["Value"]);
        Assert.Equal("5.99표/반지/공통반지", items["로오의반지"]["Group"]?.GetValue<string>());

        Assert.Equal(1, (int?)items["칸의목걸이"]["LevelRequired"]); // 레벨만 원작 표대로(사용자 2026-10-04)
        Assert.Equal(1000, (int?)items["칸의목걸이"]["Value"]);
        Assert.Equal("5.99표/목걸이/공통목걸이", items["칸의목걸이"]["Group"]?.GetValue<string>());
    }

    /// <summary>
    /// 죽어 있던 팩 드롭 3종 — 5.99 팩이 떨구라고 적어 두었지만 아이템에 <c>DropRate</c> 가 없어 영영
    /// 안 나왔다. 아벨해안이 열려(2026-09-25) 이제 손이 닿으므로 살린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>근거</b> — 5.99 팩 <c>db/mob/Abel/Abel_Monster.txt</c>: 크라켄1 <c>드롭아이템 3 실버아쿠아링</c>
    /// (49줄) · 크라켄2 <c>드롭아이템 5 실버아쿠아링</c>(97줄) · 킹아크퍼스2 <c>드롭아이템 40 골드아쿠아링</c>
    /// (259줄). Novaonline <c>db/mob/아벨해안/아벨해안.txt</c> 도 크라켄(1) 3% · 킹아크퍼스2 40% 로 같다 —
    /// 크라켄2 의 5% 만 5.99 에만 있다. 아이템의 <c>DropRate</c> 는 하나뿐이라 크라켄1·2 를 가르지 못해 두
    /// 팩이 겹치는 3% 를 쓴다. <c>db/mob/Casmanum/Casmanum_Monster.txt</c> 그림록퀸 → 그림록퀸홀은
    /// <c>드롭아이템 1</c>(1%) — Novaonline <c>mine.txt</c> 는 15%, 혼든은 그림록퀸1-3 5% · 그림록퀸2-3 3%
    /// 로 셋이 갈려, 이 서버의 괴물 정의가 온 5.99 값을 쓴다(카스마늄 갱도는 아직 손대지 않은 지역이라
    /// 다른 근거가 없다). → 2026-10-02 사용자 「헬옷 드랍률 0.5% 도 안 돼」 로 0.003(× 1.5 = 0.45%), 구광산 헬옷과 같다.
    /// </para>
    /// <para>
    /// <b>왜 안 떨어졌나</b> — 넷 다 <c>LootType</c> 이 <c>Table</c>(4) 이었다. Table 갈래는 <c>DropRate</c>
    /// 를 가중치로 써서 목록에서 하나를 고르는데(<c>LootDropper.Drop</c>), 목록이 한 칸뿐이고
    /// <c>DropRate</c> 가 0 이면 굴러가는 확률이 사실상 0 이다. 목록에서 하나를 고른 뒤 그 값을 확률로
    /// 굴리는 <c>Random</c>(2) 으로 바꿔야 <c>DropRate</c> 가 실제 확률이 된다(우드랜드·포테 장비와 같은
    /// 갈래, <c>DetermineRandomDrop</c>).
    /// </para>
    /// <para>
    /// 이 넷은 <see cref="Later" /> 목록 밖(아벨해안·카스마늄)이라 걸어 다니는 사냥터의 1~5% 잣대를 걸지
    /// 않는다 — 팩이 적은 값을 그대로 믿는다. 정의를 만드는 것은 <c>scripts/gen/items/build-gear-drops.py</c> 의
    /// <c>FIELD_BOSSES</c> 다.
    /// </para>
    /// </remarks>
    private static readonly (string Beast, string Prize, double Rate)[] FieldBosses =
    [
        ("크라켄1", "실버아쿠아링", 0.03),
        ("크라켄2", "실버아쿠아링", 0.03),
        ("킹아크퍼스2", "골드아쿠아링", 0.40),
        ("그림록퀸", "그림록퀸홀", 0.003),
    ];

    [Fact]
    public void Field_bosses_actually_roll_their_pack_listed_drop()
    {
        IReadOnlyDictionary<string, JsonNode> items = Items();

        foreach ((string beast, string prize, double rate) in FieldBosses)
        {
            JsonNode[] carrying =
            [
                .. Monsters().Where(m => m["Name"]?.GetValue<string>() == beast && Dropped(m).Contains(prize)),
            ];

            Assert.True(carrying.Length > 0, $"{beast} 가 {prize} 를 떨구는 정의를 못 찾았습니다.");

            foreach (JsonNode monster in carrying)
            {
                // Table 갈래는 DropRate 를 가중치로 써서 목록이 한 칸이면 사실상 안 뽑힌다 — 확률을
                // 실제로 굴리려면 Random 이어야 한다.
                Assert.True(((int?)monster["LootType"] & LootRandom) == LootRandom,
                    $"{beast}@{monster["AreaID"]} 의 LootType 이 {monster["LootType"]} 입니다 — " +
                    $"목록이 한 칸이면 Random({LootRandom})이어야 DropRate 가 실제로 굴러갑니다.");
            }

            Assert.True(items.ContainsKey(prize), $"«{prize}» 의 아이템 정의가 없습니다.");
            Assert.Equal(rate, (double?)items[prize]["DropRate"] ?? 0);
        }
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

    /// <summary>사용자가 원작에서 모은 표. 이름 → 그 줄(<c>수치표</c>).</summary>
    private static IReadOnlyDictionary<string, JsonNode> OriginalSheet()
    {
        string path = Path.Combine(HadesWorkspace.RepositoryRoot, "data", "game-data", "items-original-sheets.json");
        JsonNode root = JsonNode.Parse(File.ReadAllText(path))!;
        Dictionary<string, JsonNode> byName = [];

        foreach (JsonNode? row in root["수치표"]!.AsArray())
        {
            if (row?["이름"]?.GetValue<string>() is { Length: > 0 } name)
            {
                byName[name] = row;
            }
        }

        return byName;
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
