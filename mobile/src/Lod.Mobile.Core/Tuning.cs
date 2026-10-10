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

    /// <summary>
    /// 경매장(설계 <c>autopilot/loot-auction/</c> 03 상수 표, DL-4·DL-11) — 봇 하나가 걸어 두는 올림 수, 시작가·즉시 구매가는 상인 매입가의 배수,
    /// 살 때는 물건 하나에 들고 있는 금화의 이 % 와 상한 중 작은 것까지(성직자 시작 금화 1억이 시세를 끌어올리지 않게).
    /// </summary>
    public const int EcoAuctionMax = 5;

    public const int EcoAuctionStart = 2;

    public const int EcoAuctionBuyout = 4;

    public const int EcoAuctionBudget = 30;

    public const long EcoAuctionSpendCap = 1_000_000;

    /// <summary>
    /// 속성 목걸이(공격)·벨트(방어)의 장비 점수 덤 — 속성 없는 것보다 먼저 고른다(사용자 10-07 「방어 속성을 뭐라도 끼고 있는건
    /// 중요하지 바다의금벨트 같은거」). 100 = 체력 1000 만큼이라 주작의목걸이(체력 15000)처럼 훨씬 센 것은 그대로 이긴다.
    /// </summary>
    public const int EcoElementScore = 100;

    /// <summary>
    /// 장비 점수(<c>EcoShopping.Score</c>, 사용자 2026-10-11) — 직업 주 능력치 1(신 이름 접두사 하나: 전사·무도가 힘·콘, 도적 힘·덱스, 성직자·마법사
    /// 인트·위즈)과 마법방어 10(칸)이 이만큼. 그 밖 능력치는 1, 무장(−AC)은 1 — 접두사 하나가 같은 부위 기본 장비보다 확실히 앞선다(체력의 = 체력 100 = 10).
    /// </summary>
    public const int EcoScoreStat = 30;

    /// <summary>99 — 모든 직업이 마법방어 10 · 체력 500 이상 하나를 이만큼, 무장 1 을 <see cref="EcoScoreAc99" /> 로 본다(주 능력치 1 보다 앞서게).</summary>
    public const int EcoScoreTop = 100;

    public const int EcoScoreAc99 = 50;

    /// <summary>입은 것이 없는 부위는 경매에서 금화의 이 % 까지(맨몸 — 죽은 뒤 <see cref="EcoAuctionBudget" /> 에 막혀 못 사지 않게).</summary>
    public const int EcoAuctionBareBudget = 60;

    /// <summary>
    /// 경매 시세(SPEC death-penalty-gear #4) — 서버 경매 기록의 최근 이 날수 낙찰가 중앙값. 기록은 이만큼마다 한 번 읽는다.
    /// 올릴 때 시작가·즉시 구매가 = 시세의 이 %(시세가 없으면 상인 매입가 배수 <see cref="EcoAuctionStart" />·<see cref="EcoAuctionBuyout" />).
    /// </summary>
    public const int EcoMarketDays = 7;

    public static readonly TimeSpan EcoMarketEvery = TimeSpan.FromMinutes(5);

    public const int EcoMarketStart = 90;

    public const int EcoMarketBuyout = 130;

    /// <summary>올릴 때 쓰는 시세의 상한 — 상인 매입가의 이 배(옛 즉시 구매가 4배의 두 배). 봇끼리 130% 에 사고팔아 값이 끝없이 오르지 않게.</summary>
    public const int EcoMarketCap = 8;

    /// <summary>
    /// 살 때 — 점수 이득 100 마다 시세보다 이 % 더(접두사 하나 이득 30 이면 +15%, 99 의 칸 이득 100 이면 +50%), 시세 위로 많아야 <see cref="EcoMarketOverMax" />%.
    /// 금화 비율 상한(<see cref="EcoAuctionBudget" />)은 그대로.
    /// </summary>
    public const int EcoMarketOver = 50;

    public const int EcoMarketOverMax = 200;

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

    /// <summary>
    /// 파티원(파티장 말고 싸우는 봇)은 파티장이 선 칸에서 이만큼 안의 괴물만 친다 — 중심이 파티장을 따라간다. 성직자는 파티장 곁(3칸)에서
    /// 10칸 안만 회복·깨우기를 하는데, 저마다 중심에서 12칸까지 흩어지면 파티원이 그 밖에서 혼수로 죽었다(클라우드 10-07: 혼수 죽음
    /// 26건 중 파티원 25 · 그중 성직자가 깨우러 온 것 1). 4 + 붙어 치는 1 + 성직자 3 = 8칸.
    /// </summary>
    public const int EcoPartyReach = 4;
}
