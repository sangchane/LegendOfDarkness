using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Ui;

/// <summary>
/// 방향판 위 속성 목걸이 칸 다섯(사용자, 2026-10-05): 수·토·풍·화는 속성마다 하나, 다섯째는 암흑↔생명 전환.
/// 목걸이의 속성은 서버가 0x0F·0x37 수치 꼬리에 이미 싣는 공격 속성(<see cref="ItemStats.Offense" />)으로 안다 —
/// 서버 템플릿의 OffenseElement 그대로다(1 불 · 2 물 · 3 바람 · 4 땅 · 5 빛=생명 · 6 어둠=암흑).
/// </summary>
public static class NecklaceSwap
{
    /// <summary>목걸이 자리(0x37 칸 번호, <see cref="WornPlace" />).</summary>
    public const int Place = 6;

    /// <summary>앞 네 칸의 차례 — 수·토·풍·화.</summary>
    public static IReadOnlyList<Element> Singles { get; } = [Element.Water, Element.Earth, Element.Wind, Element.Fire];

    /// <summary>다섯째 칸이 오가는 둘.</summary>
    public static IReadOnlyList<Element> Pair { get; } = [Element.Dark, Element.Light];

    public static string Letter(Element element) => element switch
    {
        Element.Water => "수", Element.Earth => "토", Element.Wind => "풍", Element.Fire => "화",
        Element.Dark => "암", Element.Light => "생", _ => string.Empty
    };

    /// <summary>목걸이면 그 속성, 목걸이가 아니면(또는 수치 꼬리가 없으면) None.</summary>
    public static Element Of(ItemStats? stats) => stats is { Place: Place } s ? (Element)s.Offense : Element.None;

    /// <summary>지금 낀 목걸이, 없으면 null.</summary>
    public static WornItem? Worn(IReadOnlyList<WornItem> worn) => worn.FirstOrDefault(on => on.Slot == Place);

    /// <summary>고를 수 있는 그 속성의 목걸이 — 지금 낀 것과 가방의 것, 이름마다 하나.</summary>
    public static IReadOnlyList<(string Name, int Icon, Element Element)> Choices(
        IReadOnlyList<InventoryItem> pack, IReadOnlyList<WornItem> worn, IReadOnlyList<Element> elements)
    {
        IEnumerable<(string, int, Element)> on = worn.Where(item => item.Slot == Place)
            .Select(item => (item.Name, item.Icon, (Element)(item.Stats?.Offense ?? 0)));
        IEnumerable<(string, int, Element)> carried = pack.Select(item => (item.Name, item.Icon, Of(item.Stats)));

        return [.. on.Concat(carried).Where(one => elements.Contains(one.Item3)).DistinctBy(one => one.Item1)];
    }

    /// <summary>칸이 가리키는 목걸이: 고른 이름, 고른 게 없으면 가진 것 중 첫째, 아무것도 없으면 null.</summary>
    public static string? Chosen(string? picked, IReadOnlyList<InventoryItem> pack, IReadOnlyList<WornItem> worn, Element element) =>
        !string.IsNullOrEmpty(picked) ? picked : Choices(pack, worn, [element]).Select(one => one.Name).FirstOrDefault();

    /// <summary>그 이름의 목걸이가 든 가방 칸 — 짧게 누르면 이 칸을 쓴다(0x1C). 가방에 없으면 null.</summary>
    public static int? SlotOf(IReadOnlyList<InventoryItem> pack, string? name) =>
        pack.FirstOrDefault(item => item.Name == name && item.Stats is { Place: Place })?.Slot;

    /// <summary>지금 낀 목걸이의 속성, 없으면 None.</summary>
    public static Element Current(IReadOnlyList<WornItem> worn) => (Element)(Worn(worn)?.Stats?.Offense ?? 0);

    /// <summary>전환 칸이 다음에 낄 쪽: 지금 낀 것이 암흑이면 생명, 아니면 암흑.</summary>
    public static Element Next(IReadOnlyList<WornItem> worn) => Next(Pair, worn, _ => true);

    /// <summary>
    /// 묶음 칸(세로의 수→토→풍→화 차례 칸, 암흑↔생명 칸)이 누르면 낄 속성 — 지금 낀 것 다음 차례 중 목걸이를 가진 첫째.
    /// 묶음 밖의 것을 끼고 있으면 묶음 맨 앞부터. 가진 게 하나도 없으면 바로 다음 차례(칸은 흐리게 보인다).
    /// </summary>
    public static Element Next(IReadOnlyList<Element> group, IReadOnlyList<WornItem> worn, Func<Element, bool> has)
    {
        int at = group.ToList().IndexOf(Current(worn));

        for (int step = 1; step <= group.Count; step++)
        {
            Element next = group[(at + step) % group.Count];

            if (next != Current(worn) && has(next))
            {
                return next;
            }
        }

        return group[(at + 1) % group.Count];
    }
}
