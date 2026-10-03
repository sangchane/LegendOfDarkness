namespace Lod.Mobile.Core.Automation;

/// <summary>
/// 5.99 성직자 회복·버프·해제 마법과 봇이 함께 쓰는 마법사 저주·나르콜리의 값 — <c>성직자(비전직).txt</c> 의 SPELL_ 블록에서: 회복량은 위즈의 몇 배, 마력, 버프는
/// 몇 초. 쿠로는 신성력강화가 있으면 ×15·22마력, 없으면 ×8·15마력. 포션이 채우는 양은 서버 템플릿(templates/items)의
/// HealthRestore·ManaRestore.
/// </summary>
public static class CompanionSpells
{
    /// <summary>
    /// 봇 탭 「마법사」 체크(0xF1 6 의 비트, 0x5E 1 꼬리) — 저주는 셋 따로(사용자 2026-10-03: 걸 저주를 고른다), 나르콜리 하나.
    /// 봇은 체크된 저주 중 배운 가장 센 것 하나만 건다(저주는 한 칸).
    /// </summary>
    [Flags]
    public enum Magic : byte
    {
        None = 0,
        Lento = 1,
        Sleep = 2,
        Bardo = 4,
        Depreco = 8,
        Prabo = 16,
        All = Lento | Sleep | Bardo | Depreco | Prabo,
    }

    /// <summary>
    /// 고른 적이 없을 때의 체크 — 봇(주인 레벨 − 2, 서버 <c>Companions.LevelFor</c>)이 쓸 수 있는 가장 센 저주 하나와 나르콜리
    /// (사용자 2026-10-03: "가장 센 거만 체크"). 레벨은 서버 <c>Companions.PriestSpells</c> 와 같다 — 렌토 11 · 바르도 41 · 데프레코 71 · 프라보 99.
    /// </summary>
    public static Magic DefaultMagic(int ownerLevel)
    {
        int bot = Math.Max(1, ownerLevel - 2);

        return Magic.Sleep | (bot >= 99 ? Magic.Prabo : bot >= 71 ? Magic.Depreco : bot >= 41 ? Magic.Bardo : bot >= 11 ? Magic.Lento : Magic.None);
    }

    /// <summary>이 마법(꼬리 뗀 이름)을 켜고 끄는 체크 — 저주·나르콜리가 아니면 None.</summary>
    public static Magic SwitchOf(string name) => Bare(name) switch
    {
        "렌토" => Magic.Lento,
        "바르도" => Magic.Bardo,
        "데프레코" => Magic.Depreco,
        "프라보" => Magic.Prabo,
        "나르콜리" => Magic.Sleep,
        _ => Magic.None,
    };

    public enum Kind
    {
        Heal,
        GroupHeal,
        Buff,
        Cure,
        Curse,
        Sleep,
    }

    /// <param name="State">버프는 서버가 알리는 상태 이름(5.99 스크립트의 horrama·enare), 해제는 푸는 디버프 이름.</param>
    /// <param name="Icon">괴물에게 거는 것(저주·수면)은 걸렸는지를 둘레 알림(0x5C)의 그림 번호로 본다.</param>
    public sealed record Entry(Kind Kind, int Power, int Mana, int Seconds = 0, string State = "", int Icon = 0);

    /// <summary>저주 칸(5.99 <c>magic 1</c>)의 그림 — 렌토·바르도·데프레코 모두 같은 칸이라 하나만 걸린다(서버 <c>Pack599.Curse</c>).</summary>
    public const int CurseIcon = 82;

    /// <summary>수면(<c>debuff_sleep</c>)의 그림.</summary>
    public const int SleepIcon = 90;

    private static readonly Dictionary<string, Entry> Known = new(StringComparer.Ordinal)
    {
        ["쿠로"] = new(Kind.Heal, 8, 15),
        ["쿠라노"] = new(Kind.Heal, 20, 30),
        ["쿠라노소"] = new(Kind.Heal, 30, 70),
        ["수페라쿠라노"] = new(Kind.Heal, 40, 130),
        ["엑스쿠라노"] = new(Kind.Heal, 70, 350),
        ["쿠러스"] = new(Kind.GroupHeal, 15, 25),
        ["쿠라누스"] = new(Kind.GroupHeal, 30, 95),
        ["쿠라네라"] = new(Kind.GroupHeal, 40, 250),
        ["엑스쿠라네라"] = new(Kind.GroupHeal, 70, 430),
        ["호르라마"] = new(Kind.Buff, 0, 55, 120, "horrama"),
        ["에나르마"] = new(Kind.Buff, 0, 40, 150, "enare"),
        // 5.99 SPELL_디나르콜리 mobnar_end → 하데스 수면(sleep), SPELL_디소루마 mobsor_end → 빙결(frozen). 30마력.
        ["디나르콜리"] = new(Kind.Cure, 0, 30, 0, "sleep"),
        ["디소루마"] = new(Kind.Cure, 0, 30, 0, "frozen"),
        // 5.99 법사(비전직).txt — 저주는 세기(방어 +15·+25·+35·+45)·마력·120초, 나르콜리는 50마력·20초(20% 빗나감).
        ["렌토"] = new(Kind.Curse, 15, 30, 120, Icon: CurseIcon),
        ["바르도"] = new(Kind.Curse, 25, 50, 120, Icon: CurseIcon),
        ["데프레코"] = new(Kind.Curse, 35, 65, 120, Icon: CurseIcon),
        ["프라보"] = new(Kind.Curse, 45, 130, 120, Icon: CurseIcon),
        ["나르콜리"] = new(Kind.Sleep, 0, 50, 20, Icon: SleepIcon),
    };

    /// <summary>포션 하나가 채우는 양 — AutoPotion 의 두 목록과 같은 이름들.</summary>
    public static readonly IReadOnlyDictionary<string, int> HealthRestore = new Dictionary<string, int>
    {
        ["쿠룸"] = 250, ["최하급체력포션"] = 500, ["하급체력포션"] = 1000, ["중급체력포션"] = 2000, ["상급체력포션"] = 3000, ["엑스쿠라눔"] = 10000,
    };

    public static readonly IReadOnlyDictionary<string, int> ManaRestore = new Dictionary<string, int>
    {
        ["마라디움"] = 100, ["최하급마력포션"] = 500, ["하급마력포션"] = 1000, ["파프리카"] = 1000, ["중급마력포션"] = 1500,
        ["상급마력포션"] = 2000, ["블루피치"] = 2500,
    };

    /// <summary>배운 마법 중 이 이름의 값. 신성력강화가 쿠로를 바꾼다.</summary>
    public static Entry? Of(string name, bool empowered)
    {
        name = Bare(name);

        return name == "쿠로" && empowered ? new Entry(Kind.Heal, 15, 22)
            : Known.TryGetValue(name, out Entry? entry) ? entry : null;
    }

    /// <summary>
    /// 서버가 마법 칸(0x17)에 붙여 보내는 숙련도 꼬리 " (Lev:1/100)" 를 뗀 이름. "리젠(Lev1)" 처럼 이름에 든 괄호는 둔다.
    /// </summary>
    public static string Bare(string name)
    {
        int tail = name.IndexOf(" (Lev:", StringComparison.Ordinal);

        return tail > 0 ? name[..tail] : name;
    }
}
