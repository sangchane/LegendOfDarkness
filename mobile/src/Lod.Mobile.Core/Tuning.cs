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

    /// <summary>
    /// 차례형 레벨업 점수 계획 — 앞에서부터 목표에 못 미친 첫 능력치에 한 점(<see cref="StatPlan" />, 사용자 2026-10-06).
    /// 전사: 콘 64 까지, 나머지는 힘(서버 상한 255). 도적: 위즈 23 → 콘 41 → 힘 78 → 덱스 49 → 인트 20(시작 3에서 딱 196점 = 98레벨 × 2).
    /// 여기 있는 직업은 <see cref="StatBuilds" /> 보다 먼저 본다.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, (Stat Which, int Want)[]> StatOrders = new Dictionary<int, (Stat Which, int Want)[]>
    {
        [1] = [(Stat.Con, 64), (Stat.Str, 255)],
        [2] = [(Stat.Wis, 23), (Stat.Con, 41), (Stat.Str, 78), (Stat.Dex, 49), (Stat.Int, 20)],
    };

    // 생태계 봇(설계 autopilot/eco-bots/03-prd.md 상수 표) — 시작값, 봇 사건 기록을 보고 손본다.

    /// <summary>사냥 중 체력 물약이 이만큼 밑으로 떨어지면 마을로.</summary>
    public const int EcoPotionLow = 5;

    /// <summary>마을에서 체력 물약을 이만큼까지 채운다.</summary>
    public const int EcoPotionStock = 30;

    /// <summary>가방 빈칸이 이만큼 밑이면 마을로(가방 150칸 — 서버 <c>Inventory.LENGTH</c>).</summary>
    public const int EcoBagLow = 5;

    public const int PackSlots = 150;

    /// <summary>사냥을 이만큼 했으면 마을에 한 번 들른다(장비 바꾸기·팔기).</summary>
    public static readonly TimeSpan EcoTownEvery = TimeSpan.FromMinutes(60);

    /// <summary>사람이 같은 맵에 이만큼 보이면 다른 사냥터로 비킨다.</summary>
    public static readonly TimeSpan EcoYield = TimeSpan.FromSeconds(30);

    /// <summary>사냥터에서 경험치가 이만큼 안 오르면 자리를 옮긴다(반경 12칸을 다 잡고 서 있지 않게, 클라우드 10-07).</summary>
    public static readonly TimeSpan EcoIdleMove = TimeSpan.FromSeconds(60);

    /// <summary>성직자 봇 — 마력 물약이 이만큼 밑이면 물약 가게에 가서 <see cref="EcoPriestManaStock" /> 개까지 채운다.</summary>
    public const int EcoPriestManaLow = 10;

    public const int EcoPriestManaStock = 30;

    /// <summary>이만큼 죽을 때마다 사냥터를 한 층 낮춘다.</summary>
    public const int EcoDeathLoop = 3;

    /// <summary>한 사냥터에 생태계 봇은 이만큼까지(실측 — 한 맵에 몰면 서버가 제곱으로 무거워진다).</summary>
    public const int EcoBotsPerMap = 4;

    /// <summary>마을에서 체력이 이 % 밑이면 쉬었다 나간다 — 사냥 중 물약을 마시는 줄(50%)과 같게.</summary>
    public const int EcoRestPercent = 50;

    /// <summary>
    /// 이 레벨부터 성직자와 파티로 사냥한다(결정 19). 배포 기록(10-06~07): 한 마리 잡는 시간이 12레벨부터 늘고 첫 죽음이 14레벨 — 기록이 쌓이면 고친다.
    /// </summary>
    public const int EcoPartyLevel = 11;

    /// <summary>파티 하나의 싸우는 봇 수(성직자 하나를 더해 넷).</summary>
    public const int EcoPartyFighters = 3;
}
