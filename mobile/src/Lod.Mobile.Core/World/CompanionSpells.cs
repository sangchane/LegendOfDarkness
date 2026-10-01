namespace Lod.Mobile.Core.World;

/// <summary>
/// 5.99 성직자 회복·버프·해제 마법의 값 — <c>성직자(비전직).txt</c> 의 SPELL_ 블록에서: 회복량은 위즈의 몇 배, 마력, 버프는
/// 몇 초. 쿠로는 신성력강화가 있으면 ×15·22마력, 없으면 ×8·15마력. 포션이 채우는 양은 서버 템플릿(templates/items)의
/// HealthRestore·ManaRestore.
/// </summary>
public static class CompanionSpells
{
    public enum Kind
    {
        Heal,
        GroupHeal,
        Buff,
        Cure,
    }

    /// <param name="State">버프는 서버가 알리는 상태 이름(5.99 스크립트의 horrama·enare), 해제는 푸는 디버프 이름.</param>
    public sealed record Entry(Kind Kind, int Power, int Mana, int Seconds = 0, string State = "");

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
