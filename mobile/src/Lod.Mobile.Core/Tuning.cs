using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core;

/// <summary>
/// 손으로 맞춘 클라이언트 수치 한곳 — 걷기·돌기·동작 빠르기, 레벨업 점수 계획. 서버가 정하는 값이 아니라
/// 사용자가 손맛을 보고 고른 값이라 바꿀 때 여기만 본다.
/// </summary>
public static class Tuning
{
    /// <summary>How long one tile takes to walk, and how many frames that walk is drawn in — chosen by feel (사용자,
    /// 2026-09-24·10-02) and close to the original's own step, 114ms × 4 = 0.456 s (docs/speed-reference.md).</summary>
    public const double StepSeconds = 0.44;

    /// <summary>방향키: 보고 있지 않은 쪽을 누르면 먼저 돌기만 하고, 이만큼 더 누르고 있어야 걷는다(원작처럼, 사용자 2026-10-02).</summary>
    public const double TurnHoldSeconds = 0.2;

    /// <summary>
    /// 레벨업 점수 계획: Hades <c>Class</c> 번호 → 능력치마다 목표치(<see cref="StatPlan" />). 번호는
    /// <see cref="WorldClient.Path"/> 와 같다(5 무도가). 지금은 무도가만 — 다른 직업을 만들 때 더한다.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, (Stat Which, int Want)[]> StatBuilds = new Dictionary<int, (Stat Which, int Want)[]>
    {
        [5] = [(Stat.Con, 65), (Stat.Str, 77), (Stat.Int, 43), (Stat.Wis, 36)],
    };
}
