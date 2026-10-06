using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Automation;

/// <summary>
/// 레벨업 점수를 어디에 찍을지 — 차례형 직업(<see cref="Tuning.StatOrders" />, 전사·도적)은 앞에서부터 못 미친 첫 능력치,
/// 그 밖의 직업은 목표치에서 가장 먼 능력치에 한 점.
/// </summary>
/// <remarks>
/// 계획은 무도가(가장 먼 것)·전사·도적(차례형, 2026-10-06). 계획이 없는 직업(마법사·성직자)은 null 을 돌려
/// 점수를 그대로 남긴다.
/// </remarks>
public static class StatPlan
{
    /// <summary>다음 한 점을 찍을 능력치. 남은 점수가 없거나, 직업을 모르거나, 계획이 없거나, 목표를 다 채웠으면 null.</summary>
    public static Stat? Next(int? path, Vitals mine)
    {
        if (mine.Unspent <= 0 || path is not { } known)
        {
            return null;
        }

        if (Tuning.StatOrders.TryGetValue(known, out (Stat Which, int Want)[]? order))
        {
            return order.Where(want => want.Want > Have(mine, want.Which)).Select(want => (Stat?)want.Which).FirstOrDefault();
        }

        if (!Tuning.StatBuilds.TryGetValue(known, out (Stat Which, int Want)[]? build))
        {
            return null;
        }

        return build
            .Where(want => want.Want > Have(mine, want.Which))
            .OrderByDescending(want => want.Want - Have(mine, want.Which))
            .Select(want => (Stat?)want.Which)
            .FirstOrDefault();
    }

    private static int Have(Vitals mine, Stat which) => which switch
    {
        Stat.Str => mine.Str,
        Stat.Int => mine.Int,
        Stat.Wis => mine.Wis,
        Stat.Con => mine.Con,
        _ => mine.Dex,
    };
}
