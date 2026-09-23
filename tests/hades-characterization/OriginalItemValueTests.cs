using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 팩에서 들여온 무기·갑옷의 값은 **원작 도감이 정본이다**(사용자 결정, 2026-09-23). 팩은 같은 이름의
/// 물건에 값을 다시 매긴 갈래다 — 갑옷 값을 계단으로 뭉갰고(전부 300 / 3,000 / 7,000 …), 방어를 깎았고,
/// 레벨 칸을 한 칸씩 올렸다(도감 1·11·41·71·99 → 팩 1·21·51·81·99). `scripts/build-gear-from-original.py`
/// 가 도감에 값이 적힌 칸만 되돌린다. 이 시험이 그 되돌림을 지킨다.
/// </summary>
/// <remarks>
/// 도감은 <c>data/game-data/items-original-sheets.json</c> 의 <c>수치표</c> 5,722행이다
/// (<c>docs/items/어둠템#1.xlsx</c> → <c>scripts/build-original-item-sheets.py</c>). 칸 이름은 짐작이 아니라
/// 서버팩과 맞대어 정한 것이고, 판매가격(열4)·공격력(열25)은 2026-09-23 에 혼든 77%·노바 72% 로 가렸다.
/// </remarks>
public sealed class OriginalItemValueTests
{
    /// <summary>되돌리기가 고르는 묶음 — 팩에서 들여온 무기·갑옷.</summary>
    private static readonly string[] Reverted = ["5.99표/무기/", "5.99표/갑옷/"];

    /// <summary>
    /// 되돌리지 않는 지팡이 10종. 도감의 이 줄들은 **글자 하나까지 같다**(공격 4m20 · 레벨 11) — 등급을
    /// 나누기 전 시절의 표라, 되돌리면 마법사·성직자 무기 사다리 다섯 칸이 레벨 11 한 칸으로 뭉개진다.
    /// </summary>
    private static readonly string[] Staves =
    [
        "매직마르시아", "매직쥬피티아", "매직솔라", "매직루나", "매직파나",
        "홀리머큐리아", "홀리쥬피티아", "홀리솔라", "홀리루나", "홀리파나",
    ];

    /// <summary>도감 칸 → 템플릿 칸. 부호가 그대로 옮겨진다(<c>Option 1</c> 이 빼기다).</summary>
    private static readonly (string Column, string Field)[] Modifiers =
    [
        ("방어력", "AcModifer"), ("명중수정", "HitModifer"), ("공격수정", "DmgModifer"),
        ("체력변화", "HealthModifer"), ("마력변화", "ManaModifer"), ("힘변화", "StrModifer"),
        ("덱스변화", "DexModifer"), ("인트변화", "IntModifer"), ("위즈변화", "WisModifer"),
        ("콘변화", "ConModifer"),
    ];

    /// <summary>
    /// 도감에 이름이 있는 팩 무기·갑옷은 되돌린 칸이 한 칸도 어긋나면 안 된다. 되돌리기 전에는 169장이
    /// 모두 어긋나 있었다(854칸) — 가격 163 · 방어력 133 · 레벨제한 70 · 무게 66 · 내구력 65 · 공격력 50 …
    /// </summary>
    [Fact]
    public void Gear_the_original_sheet_names_matches_it_field_for_field()
    {
        Dictionary<string, JsonNode> sheet = Sheet();
        List<string> wrong = [];
        int checked_ = 0;

        foreach ((string name, JsonNode item) in PackGear())
        {
            if (!sheet.TryGetValue(name, out JsonNode? row) || Staves.Contains(name))
            {
                continue;
            }

            checked_++;

            Told(wrong, name, "Value", Number(row, "판매가격"), Whole(item, "Value"));
            Told(wrong, name, "CarryWeight", Number(row, "무게"), Whole(item, "CarryWeight"));
            Told(wrong, name, "MaxDurability", Number(row, "내구력"), Whole(item, "MaxDurability"));
            Told(wrong, name, "Class", Number(row, "직업제한"), Whole(item, "Class"));
            Told(wrong, name, "LevelRequired", Math.Clamp(Number(row, "레벨제한"), 1, 99), Whole(item, "LevelRequired"));

            // 공격력은 `10m20` 한 칸에 최소·최대가 붙어 있다. 갑옷 줄에는 없다.
            string attack = Text(row, "공격력");

            if (attack.Contains('m'))
            {
                string[] both = attack.Split('m');
                Told(wrong, name, "DmgMin", Leading(both[0]), Whole(item, "DmgMin"));
                Told(wrong, name, "DmgMax", Leading(both[1]), Whole(item, "DmgMax"));
            }

            foreach ((string column, string field) in Modifiers)
            {
                Told(wrong, name, field, Number(row, column), Modifier(item, field));
            }
        }

        Assert.True(checked_ > 150, $"도감에 이름이 있는 팩 무기·갑옷이 {checked_}장뿐입니다 — 159장이어야 합니다.");
        Assert.True(wrong.Count == 0,
            $"도감과 어긋난 칸 {wrong.Count}개 (python3 scripts/build-gear-from-original.py --쓰기):\n  "
            + string.Join("\n  ", wrong.Take(40)));
    }

    /// <summary>
    /// 지팡이 10종은 되돌리지 않는다 — 팩의 레벨 사다리(1·21·51·81·99)가 그대로 살아 있어야 한다.
    /// 도감대로 되돌리면 열 자루가 전부 레벨 11 이 되어 사다리가 사라진다.
    /// </summary>
    [Fact]
    public void The_ten_staves_keep_the_pack_ladder()
    {
        Dictionary<string, JsonNode> items = Items();
        Dictionary<string, JsonNode> sheet = Sheet();

        foreach (string name in Staves)
        {
            Assert.True(items.ContainsKey(name), $"{name} 아이템 템플릿이 없습니다.");
            Assert.True(sheet.ContainsKey(name), $"{name} 이 도감 수치표에 없습니다 — 두는 까닭이 사라졌습니다.");

            // 도감은 열 자루 모두 레벨 11 이라 적었다. 되돌리기가 그것을 따라갔으면 사다리가 뭉개진 것이다.
            Assert.Equal(11, Number(sheet[name], "레벨제한"));
        }

        int[] ladder = Staves.Select(name => Whole(items[name], "LevelRequired")).Distinct().Order().ToArray();

        Assert.Equal([1, 21, 51, 81, 99], ladder);
    }

    /// <summary>
    /// 되돌리기가 **도감에 없는 칸**을 건드리면 안 된다. 평타 몸 동작·속도·그림은 팩 값이 유일한 근거이고,
    /// <c>DropRate</c> 는 <c>scripts/build-gear-drops.py</c> 가 얹은 것이라 장비 생성기가 덮으면 사라진다.
    /// </summary>
    [Fact]
    public void Reverting_left_the_columns_the_sheet_has_no_word_on_alone()
    {
        Dictionary<string, JsonNode> items = Items();

        // 설단검 — 도적 단검. 되돌리기가 값·공격력을 고치지만 동작 134·속도 18·그림 6 은 팩 것이다.
        Assert.Equal(134, Whole(items["설단검"], "AttackMotion"));
        Assert.Equal(18, Whole(items["설단검"], "AttackSpeed"));
        Assert.Equal(6, Whole(items["설단검"], "Image"));

        // 커틀라스 — 우드랜드3-1·4-1 이 떨군다. 떨굴 확률은 도감에 없는 칸이라 되돌리기가 지우면 안 된다.
        Assert.Equal(3, Whole(items["커틀라스"], "Image"));
        Assert.Equal(0.06, items["커틀라스"]["DropRate"]!.GetValue<double>(), 3);

        // 레더튜닉 — 그림은 팩 것이다. 떨굴 확률이 없는 것이 맞다(아무도 안 떨구고 상점에서 산다 —
        // GearDropTests.No_gear_we_brought_in_keeps_a_drop_rate_nobody_rolls 가 그 쪽을 본다).
        Assert.Equal(2, Whole(items["레더튜닉"], "Image"));
        Assert.Null(items["레더튜닉"]["DropRate"]);

        // 도복 — 입으면 무기 없이 주먹이 나간다(공격모션 132).
        Assert.Equal(132, Whole(items["도복"], "AttackMotion"));
    }

    /// <summary>
    /// 도감에 이름이 없는 것은 되돌릴 근거가 없다 — 팩 값 그대로 두어야 한다. 팩이 새로 만든 159장과
    /// 도감이 빠뜨린 10장, 그리고 노바에서 들여온 너클이 여기 해당한다.
    /// </summary>
    [Fact]
    public void Gear_the_sheet_never_heard_of_keeps_its_pack_values()
    {
        Dictionary<string, JsonNode> items = Items();
        Dictionary<string, JsonNode> sheet = Sheet();

        foreach ((string name, int value, int level) in new[]
                 {
                     ("레더메일", 3000, 21),   // 5.99 전사방어구 — 도감에 없다
                     ("용의발톱", 0, 1),       // 5.99 무도가무기 — 도감에 없다
                     ("글러브1", 500, 1),      // 노바 너클 — 도감에도 5.99 에도 없다
                     ("견습자의글러브", 1500, 11),
                 })
        {
            Assert.False(sheet.ContainsKey(name), $"{name} 이 도감에 생겼습니다 — 이 시험의 전제가 무너집니다.");
            Assert.Equal(value, Whole(items[name], "Value"));
            Assert.Equal(level, Whole(items[name], "LevelRequired"));
        }

        int untouched = PackGear().Count(one => !sheet.ContainsKey(one.Key));

        Assert.Equal(169, untouched);
    }

    /// <summary>
    /// 무기·갑옷 <b>밖에서 판매가격 한 칸만</b> 도감을 따르는 것. 지금은 세줄금반지 한 장이다 —
    /// 자이언트맨티스가 80% 로 떨구는 상인데 서버 값이 0 이라 팔아도 한 푼이 아니었다(사용자 결정,
    /// 2026-09-23: "그정도 난이도가 있어" → 도감 값 50만을 그대로 쓴다).
    /// </summary>
    /// <remarks>
    /// <b>왜 묶음째 되돌리지 않나.</b> 반지·귀걸이·목걸이·장갑·각반·허리띠·신발·방패·투구·장식 416장 중
    /// 도감에 이름이 있는 것이 219장이고, 묶음째 되돌리면 그 219장의 값이 한꺼번에 움직인다. 방금 문을
    /// 연 우드랜드 보석상(<c>보석상여주인@우드랜드입구#10,15</c>) 물목 22개 중 도감에 이름이 있는 21개가
    /// <b>하나도 빠짐없이</b> 거기 들어 있다 —
    /// 로오의반지 500→200 · 가죽방패 3,000→750 처럼 상점 값이 통째로 흔들린다. 그래서
    /// <c>scripts/build-gear-from-original.py</c> 의 <c>VALUE_ONLY</c> 에 <b>이름을 적은 것만</b> 따라간다.
    /// </remarks>
    [Fact]
    public void The_gold_ring_the_mantis_drops_is_worth_what_the_sheet_says()
    {
        Dictionary<string, JsonNode> items = Items();
        Dictionary<string, JsonNode> sheet = Sheet();

        // 도감이 먼저다 — 표가 바뀌면 이 시험은 아래 50만이 아니라 바뀐 값을 지킨다.
        Assert.True(sheet.ContainsKey("세줄금반지"), "세줄금반지가 도감 수치표에 없습니다.");
        Assert.Equal(500_000, Number(sheet["세줄금반지"], "판매가격"));
        Assert.Equal(Number(sheet["세줄금반지"], "판매가격"), Whole(items["세줄금반지"], "Value"));

        // 따라간 것은 값 한 칸뿐이다. 내구력은 도감 5,000 · 서버 3,000 으로 갈린 채 그대로여야 한다.
        Assert.Equal(5_000, Number(sheet["세줄금반지"], "내구력"));
        Assert.Equal(3_000, Whole(items["세줄금반지"], "MaxDurability"));

        // 다른 장신구는 안 따라간다 — 보석상 물목 두 장으로 그 경계를 지킨다.
        Assert.Equal(500, Whole(items["로오의반지"], "Value"));   // 도감 200
        Assert.Equal(3_000, Whole(items["가죽방패"], "Value"));   // 도감 750
    }

    /// <summary>
    /// 되돌린 뒤에도 상점이 파는 것이 공짜가 되면 안 된다 — <c>shop1.cs</c> 는 <c>GoldPoints >= Value</c>
    /// 만 보므로 값이 0 이면 그냥 내준다.
    /// </summary>
    [Fact]
    public void Nothing_the_shops_sell_became_free()
    {
        Dictionary<string, JsonNode> items = Items();
        string mundanes = Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "mundanes");
        List<string> free = [];

        foreach (string file in Directory.EnumerateFiles(mundanes, "*.json"))
        {
            JsonNode? keeper = JsonNode.Parse(File.ReadAllText(file));

            if (keeper?["ScriptKey"]?.GetValue<string>() != "shop1")
            {
                continue;
            }

            foreach (JsonNode? one in keeper["DefaultMerchantStock"]?.AsArray() ?? [])
            {
                string goods = one!.GetValue<string>();

                if (items.TryGetValue(goods, out JsonNode? made) && Whole(made, "Value") <= 0)
                {
                    free.Add($"{Path.GetFileNameWithoutExtension(file)} 의 {goods}");
                }
            }
        }

        Assert.True(free.Count == 0, $"값이 0 이라 상점이 공짜로 내주는 것: {string.Join(", ", free)}");
    }

    private static void Told(List<string> wrong, string name, string field, int want, int have)
    {
        if (want != have)
        {
            wrong.Add($"{name} {field}: 도감 {want} ≠ 서버 {have}");
        }
    }

    /// <summary>되돌리기가 고르는 것과 똑같이 고른다 — <c>Group</c> 이 팩 무기·갑옷인 템플릿.</summary>
    private static Dictionary<string, JsonNode> PackGear()
    {
        return Items()
            .Where(one => Reverted.Any(group => (one.Value["Group"]?.GetValue<string>() ?? "").StartsWith(group, StringComparison.Ordinal)))
            .ToDictionary(one => one.Key, one => one.Value, StringComparer.Ordinal);
    }

    private static Dictionary<string, JsonNode> Items()
    {
        Dictionary<string, JsonNode> found = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(
                     Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "items"), "*.json"))
        {
            JsonNode? template = JsonNode.Parse(File.ReadAllText(file));

            if (template?["Name"]?.GetValue<string>() is { } name)
            {
                found[name] = template;
            }
        }

        return found;
    }

    /// <summary>도감 수치표를 이름으로 찾을 수 있게. 같은 이름이 두 줄이면 앞의 것을 쓴다(지금은 없다).</summary>
    private static Dictionary<string, JsonNode> Sheet()
    {
        string path = Path.Combine(HadesWorkspace.RepositoryRoot, "data", "game-data", "items-original-sheets.json");

        Assert.True(File.Exists(path), $"원작 도감이 없습니다: {path} (python3 scripts/build-original-item-sheets.py)");

        JsonNode sheet = JsonNode.Parse(File.ReadAllText(path))!;
        Dictionary<string, JsonNode> rows = new(StringComparer.Ordinal);

        foreach (JsonNode? row in sheet["수치표"]!.AsArray())
        {
            if (row?["이름"]?.GetValue<string>() is { } name && !rows.ContainsKey(name))
            {
                rows[name] = row;
            }
        }

        return rows;
    }

    private static int Whole(JsonNode item, string field) => item[field]?.GetValue<int>() ?? 0;

    /// <summary>수정치는 <c>{Option, Value}</c> 두 칸이다. <c>Option 1</c> 이 빼기이고, 칸이 없으면 0 이다.</summary>
    private static int Modifier(JsonNode item, string field)
    {
        if (item[field] is not { } made)
        {
            return 0;
        }

        int value = made["Value"]?.GetValue<int>() ?? 0;

        return made["Option"]?.GetValue<int>() == 1 ? -value : value;
    }

    private static string Text(JsonNode row, string column) => row[column]?.GetValue<string>() ?? "";

    private static int Number(JsonNode row, string column) => Leading(Text(row, column));

    /// <summary>도감 값은 글자다. <c>0g</c> 처럼 찌꺼기가 붙은 줄이 있어 앞의 숫자만 읽는다.</summary>
    private static int Leading(string value)
    {
        string digits = "";

        foreach (char letter in value.Trim())
        {
            if (char.IsAsciiDigit(letter) || (letter == '-' && digits.Length == 0))
            {
                digits += letter;
            }
            else
            {
                break;
            }
        }

        return digits is "" or "-" ? 0 : int.Parse(digits);
    }
}
