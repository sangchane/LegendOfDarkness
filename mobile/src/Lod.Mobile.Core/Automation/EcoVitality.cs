namespace Lod.Mobile.Core.Automation;

/// <summary>
/// ⑤ 99 뒤 체력·마력 사기(결정 16, <c>autopilot/eco-bots/vitality-SPEC.md</c>) — 세오는 체력 +50, 칸은 마력 +25 를 주고 한 번마다 그때
/// 최대 × 500 의 쌓인 경험치를 가져간다(5.99 <c>세오.cs</c>·<c>칸.cs</c>, 다 벗은 채로). 쌓인 경험치로 몇 번 살 수 있나.
/// </summary>
public static class EcoVitality
{
    /// <summary>세오가 한 번에 주는 체력.</summary>
    public const int Health = 50;

    /// <summary>칸이 한 번에 주는 마력.</summary>
    public const int Mana = 25;

    /// <summary>
    /// 한 번 들를 때 이만큼까지 — 서버는 한 번마다 상태를 두 번 보낸다(<c>exp_del</c>·<c>set_basevita</c>).
    /// ponytail: 한 시간에 한 번 들르니 더 쌓였으면 다음에 이어 산다.
    /// </summary>
    public const int MostAtOnce = 200;

    /// <summary>
    /// 지금 최대(다 벗은 것)와 쌓인 경험치로 몇 번 살 수 있나. 서버 검사(<c>get_baseexp ≥ 최대</c>, get_baseexp = 쌓인 경험치 ÷ 500)와
    /// 빼는 양(최대 × 500)이 같아, 남은 것이 그때 최대 × 500 이상이면 한 번 더.
    /// </summary>
    public static int Times(int maximum, long banked, int step)
    {
        int times = 0;

        for (long cost = maximum * 500L; times < MostAtOnce && banked >= cost; cost = (maximum += step) * 500L)
        {
            banked -= cost;
            times++;
        }

        return times;
    }
}
