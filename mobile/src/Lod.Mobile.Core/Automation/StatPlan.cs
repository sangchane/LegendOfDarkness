using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Automation;

/// <summary>
/// 레벨업 점수를 어디에 찍을지 — 직업마다 목표치를 두고, 목표에서 가장 먼 능력치에 한 점.
/// </summary>
/// <remarks>
/// 지금은 무도가만 계획이 있다(지금까지 무도가만 플레이했다, 2026-10-02). 계획이 없는 직업은 null 을 돌려
/// 점수를 그대로 남긴다 — 무도가 목표치로 전사·법사 점수를 찍어 버리지 않게. 다른 직업을 만들 때 표
/// (<see cref="Tuning.StatBuilds" />)에 더한다.
/// </remarks>
public static class StatPlan
{
    /// <summary>다음 한 점을 찍을 능력치. 남은 점수가 없거나, 직업을 모르거나, 계획이 없거나, 목표를 다 채웠으면 null.</summary>
    public static Stat? Next(int? path, Vitals mine)
    {
        if (mine.Unspent <= 0 || path is not { } known || !Tuning.StatBuilds.TryGetValue(known, out (Stat Which, int Want)[]? build))
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
