using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Ui;

/// <summary>
/// 상점 창 갈래 탭 — 사용자 2026-10-08 「탭도 안나눠져 있는거 같거든 … 갑옷, 악세사리(귀걸이, 반지), 장갑, 팔찌, 각반은 한 카테고리,
/// 벨트류, 방패 이런식으로」. 서버가 상점 목록 뒤에 붙여 보내는 수치의 입는 자리(<see cref="ItemStats.Place" />)로 가른다 — 무기 1 ·
/// 갑옷 2 · 방패 3 · 투구 4 · 귀걸이 5 · 목걸이 6 · 반지 7·8 · 장갑·팔찌 9·10 · 벨트 11 · 각반 12 · 신발 13.
/// </summary>
public static class ShopKinds
{
    /// <summary>탭 차례.</summary>
    public static readonly string[] Order = ["무기", "갑옷", "방패", "투구", "장신구", "장갑·팔찌·각반", "벨트", "신발", "기타"];

    /// <summary>한 물건의 갈래 — 수치가 없으면(옛 서버·소모품) 기타.</summary>
    public static string Of(ItemStats? stats) => stats?.Place switch
    {
        1 => "무기",
        2 => "갑옷",
        3 => "방패",
        4 => "투구",
        5 or 6 or 7 or 8 => "장신구",
        9 or 10 or 12 => "장갑·팔찌·각반",
        11 => "벨트",
        13 => "신발",
        _ => "기타"
    };

    /// <summary>이 상점에 있는 갈래만, 탭 차례대로.</summary>
    public static IReadOnlyList<string> In(IEnumerable<ItemStats?> goods)
    {
        HashSet<string> present = [.. goods.Select(Of)];
        return [.. Order.Where(present.Contains)];
    }
}
