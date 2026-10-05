using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Automation;

/// <summary>동료 봇의 설정. 봇 프로그램의 설정 파일에서 바꿀 수 있다.</summary>
/// <param name="HealOwnerPercent">주인 체력이 이 % 아래면 회복.</param>
/// <param name="HealSelfPercent">자기 체력이 이 % 아래면 회복.</param>
/// <param name="FollowFrom">주인과 이만큼 넘게 떨어지면 따라 걷기 시작한다.</param>
/// <param name="FollowTo">따라 걷다가 이만큼 가까워지면 선다.</param>
/// <param name="PotionHealthPercent">자기 체력이 이 % 아래면 체력 포션.</param>
/// <param name="PotionManaPercent">자기 마력이 이 % 아래면 마력 포션(가장 싼 회복도 못 걸 마력이면 그 전에라도).</param>
/// <param name="Priest">봇 탭 「성직자」 — 해제(디나르콜리·디소루마)·버프(호르라마·에나르마) 켬.</param>
/// <param name="Heal">한 사람 회복 셀렉트(<see cref="CompanionSpells.Heals" />) — 0 자동, k 는 k 번째까지, 255 끄기.</param>
/// <param name="GroupHeal">파티 회복 셀렉트(<see cref="CompanionSpells.GroupHeals" />) — 위와 같다.</param>
/// <param name="Magic">봇 탭 「마법사」 체크 — 걸어도 되는 저주(렌토·바르도·데프레코)와 나르콜리. 주인이 앱에서 고른다(0x5E 종류 1 꼬리).</param>
public sealed record CompanionSettings(
    int HealOwnerPercent = 70,
    int HealSelfPercent = 50,
    int FollowFrom = 3,
    int FollowTo = 2,
    int PotionHealthPercent = 40,
    int PotionManaPercent = 30,
    CompanionSpells.Magic Magic = CompanionSpells.Magic.All,
    CompanionSpells.Priest Priest = CompanionSpells.Priest.All,
    int Heal = CompanionSpells.HealAuto,
    int GroupHeal = CompanionSpells.HealAuto)
{
    /// <summary>주인이 봇 탭에서 고른 대로 이 마법을 써도 되나.</summary>
    public bool Allows(string spell) => CompanionSpells.Allowed(spell, Magic, Priest, Heal, GroupHeal);
}

/// <summary>봇 둘레의 괴물 하나 — 저주·나르콜리를 고르려고.</summary>
/// <param name="Cursed">저주 그림(82)이 보인다.</param>
/// <param name="Asleep">수면 그림(90)이 보인다.</param>
/// <param name="OwnerHits">주인이 방금(3초 안) 이 괴물을 쳤다(0x5D 의 Source).</param>
/// <param name="HitsOwner">이 괴물이 방금 주인을 쳤다.</param>
public sealed record Foe(uint Serial, Tile At, bool Cursed, bool Asleep, bool OwnerHits, bool HitsOwner);

public enum CompanionAct
{
    /// <summary>할 일이 없다.</summary>
    Wait,

    /// <summary>쓰러졌다(혼수·죽음) — 아무것도 하지 않는다.</summary>
    Stop,

    /// <summary>마법 한 번(<see cref="CompanionStep.Slot" /> 을 <see cref="CompanionStep.Target" /> 에게).</summary>
    Cast,

    /// <summary>한 칸 걷는다(<see cref="CompanionStep.Toward" />).</summary>
    Walk,

    /// <summary>마력이 모자라 쉰다 — 저절로 차기를 기다린다.</summary>
    Rest,

    /// <summary>가방의 포션 하나(<see cref="CompanionStep.Slot" /> 은 가방 칸).</summary>
    Drink,

    /// <summary>혼수인 주인을 깨운다(바로 옆 칸에서, 서버 0xF1 5).</summary>
    WakeOwner,
}

public sealed record CompanionStep(CompanionAct Act, int Slot = 0, uint Target = 0, Direction Toward = Direction.South, string Why = "");

/// <summary>한 틱에 봇이 보는 세상. 봇 프로그램이 WorldClient 에서 모아 넘긴다.</summary>
public sealed record CompanionSight
{
    public required uint Me { get; init; }

    /// <summary>주인 serial(0x5E). 0 이면 주인이 없다.</summary>
    public required uint Master { get; init; }

    public required Tile Standing { get; init; }
    public required Vitals? Vitals { get; init; }
    public bool Comatose { get; init; }

    /// <summary>주인이 선 칸 — 같은 맵에서 보일 때만. 안 보이면 null.</summary>
    public Tile? OwnerAt { get; init; }

    /// <summary>
    /// 남의 체력 %(0x13). 파티원 체력을 따로 알리는 패킷은 없다 — 맞을 때 곁의 사람에게 가는 막대(<c>Sprite.
    /// CompleteDamageApplication</c>)와, 회복 뒤 우리 서버가 더 보내는 막대(<c>ServerFormat5D.Healed</c>)로 안다.
    /// </summary>
    public Func<uint, int?> HealthOf { get; init; } = _ => null;

    public IReadOnlyList<LearnedSpell> Spells { get; init; } = [];

    /// <summary>봇 가방 — 포션을 여기서 찾는다.</summary>
    public IReadOnlyList<InventoryItem> Pack { get; init; } = [];

    /// <summary>
    /// 주인·봇에게 지금 걸린 것의 서버 이름(0x5E 종류 3). 서버가 아직 알리지 않았으면 null — 그때는 버프를 제 시계로 다시 건다.
    /// </summary>
    public Func<uint, IReadOnlyCollection<string>?> StatusesOf { get; init; } = _ => null;

    /// <summary>벽·맵 밖.</summary>
    public Func<Tile, bool> Blocked { get; init; } = _ => false;

    /// <summary>보이는 괴물(지나갈 수 있는 것·상인 빼고).</summary>
    public IReadOnlyList<Foe> Foes { get; init; } = [];

    /// <summary>다른 사람·괴물이 선 칸(주인 칸 포함해도 된다 — 주인 칸은 따로 뺀다).</summary>
    public IReadOnlyCollection<Tile> Occupied { get; init; } = [];

    public required TimeSpan Now { get; init; }
}

/// <summary>
/// 동료 봇의 판단 — 엔진 없이. <see cref="AutoHunt" /> 처럼 우선순위 순으로 훑어 할 수 있는 첫 일 하나를 돌려준다:
/// 멈춤(혼수·죽음·유령) &gt; 주인 혼수 깨우기 &gt; 해제(수면·빙결) &gt; 주인 회복 &gt; 봇 체력 포션 &gt; 자기 회복 마법 &gt; 봇 마력 포션 &gt; 버프 유지 &gt;
/// 돕기(저주·나르콜리) &gt; 따라가기 &gt; 쉬기 &gt; 기다림.
/// SleepHunter4 의 파티원 회복(<c>PlayerMacroState</c> 의 FlowerQueue — 체력이 기준 아래인 이를 먼저)과 버프 유지(지속 시간이
/// 끝나면 다시)를 본떴다.
/// </summary>
public sealed class CompanionBrain
{
    /// <summary>마법 하나를 쓰고 다음 무엇이든 하기까지. 서버는 걷는 중의 주문을 끊는다(CancelCastingWhenWalking).</summary>
    public static readonly TimeSpan CastGap = TimeSpan.FromSeconds(1);

    /// <summary>해제는 앞 주문 뒤 이만큼만 기다린다 — 서버는 다른 마법이면 곧 받는다.</summary>
    public static readonly TimeSpan CureGap = TimeSpan.FromMilliseconds(300);

    /// <summary>회복 사이 — 한 번 걸고 체력바(0x13)가 오르는 것을 본 뒤에.</summary>
    public static readonly TimeSpan HealGap = TimeSpan.FromMilliseconds(1500);

    /// <summary>주인을 채우는 동안 회복 사이 — 서버 마법 딜레이(0.25초) 바로 위.</summary>
    public static readonly TimeSpan FillGap = TimeSpan.FromMilliseconds(300);

    /// <summary>깨우기가 안 먹었을 때 다시 깨우기까지 — 그 사이 주문은 회복·이모탈에 쓴다.</summary>
    public static readonly TimeSpan WakeRetry = TimeSpan.FromSeconds(3);

    /// <summary>걸음 사이. 서버 걷기 제한을 넉넉히 지킨다(시험들의 450ms 보다 느리게).</summary>
    public static readonly TimeSpan WalkGap = TimeSpan.FromMilliseconds(500);

    /// <summary>버프는 지속 시간이 다 지나고 이만큼 뒤에 다시 — 일찍 걸면 걸린 사람에게 "이미 걸려있습니다." 가 간다.</summary>
    public static readonly TimeSpan BuffSlack = TimeSpan.FromSeconds(1);

    /// <summary>버프를 건 뒤 서버의 상태 알림(1초마다)에 나타나기를 기다리는 시간 — 그 안에 또 걸지 않는다.</summary>
    public static readonly TimeSpan BuffConfirm = TimeSpan.FromSeconds(3);

    /// <summary>포션 사이 — 한 병 마시고 가방·체력이 바뀌는 것을 본 뒤에.</summary>
    public static readonly TimeSpan DrinkGap = TimeSpan.FromMilliseconds(1500);

    /// <summary>이만큼보다 멀면 주인을 회복하지 않는다(화면 밖).</summary>
    public const int CastReach = 10;

    /// <summary>저주·나르콜리는 마력이 이 % 이상일 때만 — 나머지는 회복 몫이다.</summary>
    public const int AssistManaPercent = 50;

    /// <summary>
    /// 잠든 것을 본 괴물은 이만큼(나르콜리 길이) 다시 재우지 않는다 — 주인이 기본공격으로 깨울 때마다 다시 걸면 마력이 바닥난다
    /// (사용자, 2026-10-03). 잠은 첫 한 대를 두 배로 만들고 끝나는 것으로 친다.
    /// </summary>
    public static readonly TimeSpan SleepAgain = TimeSpan.FromSeconds(20);

    // 괴물 serial → 잠든 것을 처음 본 때 · (마법, 괴물) → 건 때. 오래된 것은 돕기마다 지운다(봇은 오래 돈다).
    private readonly Dictionary<uint, TimeSpan> _sleptSeen = [];
    private readonly Dictionary<(string Spell, uint Target), TimeSpan> _tried = [];

    private readonly Dictionary<(string Spell, uint Target), TimeSpan> _buffed = [];
    private TimeSpan _lastWake = Reckon.Never;

    // 주인 체력이 기준 아래로 내려가 회복을 시작했으면 가득 찰 때까지 이어 채운다 — 기준(70%)을 넘는 순간 멈춰
    // 한두 번 주고 말았다(사용자 2026-10-05).
    private bool _fillingOwner;
    private TimeSpan _lastCast = Reckon.Never;
    private TimeSpan _lastHeal = Reckon.Never;
    private TimeSpan _lastWalk = Reckon.Never;
    private TimeSpan _lastDrink = Reckon.Never;
    private uint _master;

    // 서버가 되돌린 걸음 — 벽 파일이 없는 맵의 벽이나 선 괴물. 막힌 칸으로 잠시 기억해 돌아간다.
    private readonly Dictionary<Tile, TimeSpan> _refused = [];
    private (Tile From, Tile To)? _stepped;

    /// <summary>되돌려진 칸을 막힌 칸으로 기억하는 시간 — 괴물은 비키므로 영영은 아니다.</summary>
    public static readonly TimeSpan RefusedFor = TimeSpan.FromSeconds(10);
    private bool _following;

    public CompanionStep Next(CompanionSight sight, CompanionSettings settings)
    {
        if (sight.Master != _master)
        {
            // 주인이 바뀌면 걸어 둔 버프 기억은 뜻이 없다.
            _master = sight.Master;
            _buffed.Clear();
            _sleptSeen.Clear();
            _tried.Clear();
            _following = false;
        }

        if (sight.Master == 0)
        {
            return new(CompanionAct.Wait, Why: "주인 없음");
        }

        if (sight.Comatose || sight.Vitals is { MaximumHealth: > 0, Health: <= 0 }
            || sight.StatusesOf(sight.Me)?.Contains("ghost") == true)
        {
            return new(CompanionAct.Stop, Why: "쓰러짐");
        }

        Reading reading = Read(sight, settings);
        NoteSleepers(sight);

        return Emergency(sight, settings, reading)
               ?? Recover(sight, settings, reading)
               ?? Maintain(sight, settings, reading)
               // 주인이 다친 동안은 저주·나르콜리를 쉬고 다음 회복을 기다린다 — 회복 사이(1.5초)마다 저주를 끼워 넣다가 쓰러진
               // 주인을 못 살렸다(사용자 2026-10-04).
               ?? (reading.OwnerHurt ? null : Assist(sight, settings, reading))
               ?? Follow(sight, settings, reading.Now)
               ?? (reading.Mana < reading.Cheapest
                   ? new(CompanionAct.Rest, Why: "마력 부족")
                   : new(CompanionAct.Wait, Why: "기다림"));
    }

    /// <summary>한 틱의 판단에 두루 쓰는 값 — 주문 사이가 됐나, 주인이 가까운가·다쳤나, 마력, 가장 싼 회복.</summary>
    private readonly record struct Reading(
        TimeSpan Now,
        bool CanCast,
        bool CanHeal,
        bool CanDrink,
        bool OwnerNear,
        bool OwnerHurt,
        bool SelfHurt,
        bool Empowered,
        int Mana,
        int Cheapest);

    private Reading Read(CompanionSight sight, CompanionSettings settings)
    {
        TimeSpan now = sight.Now;
        bool canCast = now - _lastCast >= CastGap;
        bool ownerNear = sight.OwnerAt is { } at && Reckon.Steps(at, sight.Standing) <= CastReach;
        int ownerHealth = ownerNear ? sight.HealthOf(sight.Master) ?? 100 : 100;
        bool empowered = sight.Spells.Any(one => CompanionSpells.Bare(one.Name) == "신성력강화");
        int cheapest = sight.Spells
            .Select(one => CompanionSpells.Of(one.Name, empowered))
            .Where(entry => entry is { Kind: CompanionSpells.Kind.Heal })
            .Select(entry => entry!.Mana)
            .DefaultIfEmpty(0)
            .Min();

        return new Reading(
            now,
            canCast,
            // 주인을 채우는 중이면 틱마다(FillGap) — 1.5초마다 한 번으로는 큰 체력을 못 따라갔다(사용자 2026-10-05 「1틱마다 쭉쭉」).
            CanHeal: _fillingOwner
                ? now - _lastCast >= FillGap && now - _lastHeal >= FillGap
                : canCast && now - _lastHeal >= HealGap,
            CanDrink: now - _lastDrink >= DrinkGap,
            ownerNear,
            // 체력바 0% 도 회복한다 — 다라밀공 뒤 체력 1/100,000 은 0% 로 와서 「쓰러짐」으로 보고 걸렀다(사용자 2026-10-05).
            // 쓰러졌는지는 혼수(skulled) 상태로만 본다 — 그때는 Emergency 가 깨운다.
            OwnerHurt: ownerNear && sight.StatusesOf(sight.Master)?.Contains("skulled") != true
                       && (_fillingOwner = ownerHealth < settings.HealOwnerPercent || (_fillingOwner && ownerHealth < 100)),
            SelfHurt: Reckon.HealthPercent(sight.Vitals) < settings.HealSelfPercent,
            empowered,
            Mana: sight.Vitals?.Mana ?? 0,
            cheapest);
    }

    /// <summary>급한 일 — 혼수인 주인 깨우기, 그다음 해제(수면·빙결).</summary>
    private CompanionStep? Emergency(CompanionSight sight, CompanionSettings settings, Reading reading)
    {
        TimeSpan now = reading.Now;

        // 주인이 혼수면 가장 먼저 — 옆 칸으로 가서 깨운다(사용자 결정 2026-09-26, 서버 0xF1 5 — 코마디움과 같은 효과, 아무것도 안 쓴다).
        if (sight.OwnerAt is { } fallen && sight.StatusesOf(sight.Master)?.Contains("skulled") == true)
        {
            if (Reckon.Steps(fallen, sight.Standing) > 1)
            {
                return StepTo(sight, fallen, now, "주인 깨우러 가기");
            }

            // 안 먹으면 3초마다 다시 — 그 사이 주문 차례는 회복·이모탈에 준다.
            if (now - _lastCast >= CastGap && now - _lastWake >= WakeRetry)
            {
                _lastCast = now;
                _lastWake = now;
                return new(CompanionAct.WakeOwner, Target: sight.Master, Why: "주인 깨우기");
            }

            // 깨우기 사이에는 기다리지 않고 아래(이모탈·회복)로 넘어간다 — 서버가 깨우기를 말없이 거절하면 봇이 15초 동안 깨우기만
            // 되풀이하다 쓰러졌다(사용자 2026-10-05 「회복을 제대로 안 쓴다」).
        }

        // 해제가 가장 먼저 — 수면(나르콜리)·빙결이면 주인은 아무것도 못 한다(사용자, 2026-09-26). 주문 사이(1초)를 다 기다리지
        // 않는다(CureGap) — 막 버프를 걸었어도 곧 푼다.
        if (now - _lastCast >= CureGap && Cure(sight, settings, reading.Empowered, reading.Mana, reading.OwnerNear) is { } cure)
        {
            return Cast(cure, now, heal: false);
        }

        return null;
    }

    /// <summary>채우기 — 주인 회복, 봇 체력 포션, 자기 회복 마법, 봇 마력 포션 차례.</summary>
    private CompanionStep? Recover(CompanionSight sight, CompanionSettings settings, Reading reading)
    {
        TimeSpan now = reading.Now;

        // 괴물이 붙었거나 봇이 위험하면 먼저 이모탈(10초 무적) — 그동안 주인을 계속 채울 수 있다(사용자 2026-10-04 「이모탈을 배우면
        // 여유가」). 체력 50% 아래를 기다리면 99 사냥터에서는 이미 늦어 쓰러진 뒤에 나갔다 — 괴물이 2칸 안이면 미리 건다.
        // 이미 무적(dion)이면 걸지 않는다 — 서버가 「이미 걸려있습니다」로 거절한다.
        bool threatened = reading.SelfHurt || sight.Foes.Any(foe => Reckon.Steps(foe.At, sight.Standing) <= 2);
        if (reading.CanCast && threatened && sight.StatusesOf(sight.Me)?.Contains("dion") != true
            && Best(sight, settings, CompanionSpells.Kind.Shield, sight.Me, reading.Empowered, reading.Mana, "무적") is { } shield)
        {
            return Cast(shield, now, heal: false);
        }

        // 주인 회복(둘 다 아프면 파티 회복).
        if (reading.CanHeal && reading.OwnerHurt)
        {
            CompanionStep? heal = (reading.SelfHurt ? Best(sight, settings, CompanionSpells.Kind.GroupHeal, sight.Master, reading.Empowered, reading.Mana, "파티 회복") : null)
                                  ?? Best(sight, settings, CompanionSpells.Kind.Heal, sight.Master, reading.Empowered, reading.Mana, "주인 회복");

            if (heal is not null)
            {
                return Cast(heal, now, heal: true);
            }
        }

        // 봇 체력 포션 — 회복 마법보다 먼저(마력을 아낀다).
        if (reading.CanDrink && Reckon.HealthPercent(sight.Vitals) < settings.PotionHealthPercent
            && Potion(sight.Pack, CompanionSpells.HealthRestore, Missing(sight.Vitals?.MaximumHealth, sight.Vitals?.Health)) is { } health)
        {
            _lastDrink = now;
            return new(CompanionAct.Drink, health.Slot, Why: $"체력 포션 {health.Name}");
        }

        if (reading.CanHeal && reading.SelfHurt && Best(sight, settings, CompanionSpells.Kind.Heal, sight.Me, reading.Empowered, reading.Mana, "자기 회복") is { } self)
        {
            return Cast(self, now, heal: true);
        }

        // 봇 마력 포션 — 마력이 낮거나, 가장 싼 회복도 못 걸어 쉬어야 할 때.
        if (reading.CanDrink && (Reckon.ManaPercent(sight.Vitals) < settings.PotionManaPercent || reading.Mana < reading.Cheapest)
            && Potion(sight.Pack, CompanionSpells.ManaRestore, Missing(sight.Vitals?.MaximumMana, sight.Vitals?.Mana)) is { } restoring)
        {
            _lastDrink = now;
            return new(CompanionAct.Drink, restoring.Slot, Why: $"마력 포션 {restoring.Name}");
        }

        return null;
    }

    /// <summary>유지 — 버프가 풀렸으면 다시 건다.</summary>
    private CompanionStep? Maintain(CompanionSight sight, CompanionSettings settings, Reading reading)
    {
        if (reading.CanCast && Buff(sight, settings, reading.Empowered, reading.Mana, reading.OwnerNear) is { } buff)
        {
            _lastCast = reading.Now;
            return buff;
        }

        return null;
    }

    /// <summary>
    /// 돕기 — 주인과 싸우는 괴물에게 저주, 주인이 치지 않는 괴물에게 나르콜리. 마력이 넉넉할 때만.
    /// 서버 스크립트는 마력부터 빼고 "이미 걸려 있나"를 본다 — 걸린 것에 다시 걸면 마력만 버리므로 그림으로 먼저 본다.
    /// </summary>
    private CompanionStep? Assist(CompanionSight sight, CompanionSettings settings, Reading reading)
    {
        TimeSpan now = reading.Now;

        foreach ((string, uint) gone in _tried.Where(pair => now - pair.Value >= BuffConfirm).Select(pair => pair.Key).ToList())
        {
            _tried.Remove(gone);
        }

        if (!reading.CanCast || sight.OwnerAt is not { } owner || Reckon.ManaPercent(sight.Vitals) < AssistManaPercent)
        {
            return null;
        }

        List<Foe> fighting = sight.Foes
            .Where(foe => Reckon.Steps(foe.At, sight.Standing) <= CastReach
                          && (foe.OwnerHits || foe.HitsOwner || Reckon.Steps(foe.At, owner) <= 1))
            .OrderByDescending(foe => foe.OwnerHits)
            .ThenBy(foe => Reckon.Steps(foe.At, owner))
            .ToList();

        if (Strongest(sight, CompanionSpells.Kind.Curse, reading.Mana, settings) is { } curse)
        {
            foreach (Foe foe in fighting.Where(foe => !foe.Cursed))
            {
                if (Fresh(curse.Name, foe.Serial, now))
                {
                    return new(CompanionAct.Cast, curse.Slot, foe.Serial, Why: $"저주 {curse.Name}");
                }
            }
        }

        if (Strongest(sight, CompanionSpells.Kind.Sleep, reading.Mana, settings) is { } sleep)
        {
            // 주인이 치는 괴물은 재워도 다음 한 대에 깬다 — 옆에서 덤비는 괴물만.
            foreach (Foe foe in fighting.Where(foe => !foe.OwnerHits && !foe.Asleep && !_sleptSeen.ContainsKey(foe.Serial)))
            {
                if (Fresh(sleep.Name, foe.Serial, now))
                {
                    return new(CompanionAct.Cast, sleep.Slot, foe.Serial, Why: $"나르콜리 {sleep.Name}");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 잠든 괴물을 틱마다 적는다 — 돕기 앞의 일(회복·버프)이 바빠 돕기가 돌지 않는 사이 잠들었다 깨도 20초 막기가 걸리게.
    /// </summary>
    private void NoteSleepers(CompanionSight sight)
    {
        foreach (Foe foe in sight.Foes.Where(foe => foe.Asleep))
        {
            _sleptSeen.TryAdd(foe.Serial, sight.Now);
        }

        foreach (uint gone in _sleptSeen.Where(pair => sight.Now - pair.Value > SleepAgain).Select(pair => pair.Key).ToList())
        {
            _sleptSeen.Remove(gone);
        }
    }

    /// <summary>이 괴물에 이 마법을 막 걸지 않았으면 걸었다고 적는다 — 그림이 둘레 알림에 오기를 <see cref="BuffConfirm" /> 만큼 기다린다.</summary>
    private bool Fresh(string spell, uint target, TimeSpan now)
    {
        if (_tried.TryGetValue((spell, target), out TimeSpan at) && now - at < BuffConfirm)
        {
            return false;
        }

        _tried[(spell, target)] = now;
        _lastCast = now;
        return true;
    }

    /// <summary>배운 것 중 주인이 체크해 둔, 이 종류에서 마력이 닿는 가장 센 것의 칸과 이름.</summary>
    private static (int Slot, string Name)? Strongest(CompanionSight sight, CompanionSpells.Kind kind, int mana, CompanionSettings settings) =>
        sight.Spells
            .Select(one => (Spell: one, Entry: CompanionSpells.Of(one.Name, empowered: false)))
            .Where(pair => pair.Entry is { } entry && entry.Kind == kind && entry.Mana <= mana
                           && settings.Allows(pair.Spell.Name))
            .OrderByDescending(pair => pair.Entry!.Power)
            .Select(pair => ((int Slot, string Name)?)(pair.Spell.Slot, CompanionSpells.Bare(pair.Spell.Name)))
            .FirstOrDefault();

    private CompanionStep Cast(CompanionStep step, TimeSpan now, bool heal)
    {
        _lastCast = now;

        if (heal)
        {
            _lastHeal = now;
        }

        return step;
    }

    private static int Missing(int? maximum, int? value) => Math.Max(0, (maximum ?? 0) - (value ?? 0));

    /// <summary>가방에 든 것 중 모자란 만큼을 채우는 가장 작은 등급 — 그런 것이 없으면 가진 것 중 가장 큰 것.</summary>
    private static InventoryItem? Potion(IReadOnlyList<InventoryItem> pack, IReadOnlyDictionary<string, int> restore, int missing)
    {
        var carried = pack
            .Where(one => restore.ContainsKey(one.Name))
            .OrderBy(one => restore[one.Name])
            .ThenBy(one => one.Slot)
            .ToList();

        return carried.FirstOrDefault(one => restore[one.Name] >= missing) ?? carried.LastOrDefault();
    }

    /// <summary>해제 — 주인 먼저 그다음 자기. 서버가 알린 디버프 중 배운 해제 마법이 푸는 것이 있으면.</summary>
    private CompanionStep? Cure(CompanionSight sight, CompanionSettings settings, bool empowered, int mana, bool ownerNear)
    {
        foreach (uint target in ownerNear ? new[] { sight.Master, sight.Me } : new[] { sight.Me })
        {
            if (sight.StatusesOf(target) is not { Count: > 0 } on)
            {
                continue;
            }

            foreach (LearnedSpell spell in sight.Spells)
            {
                string name = CompanionSpells.Bare(spell.Name);

                if (CompanionSpells.Of(name, empowered) is { Kind: CompanionSpells.Kind.Cure } entry && entry.Mana <= mana && settings.Allows(name)
                    && on.Contains(entry.State)
                    && !(_buffed.TryGetValue((name, target), out TimeSpan at) && sight.Now - at < BuffConfirm))
                {
                    _buffed[(name, target)] = sight.Now;
                    return new(CompanionAct.Cast, spell.Slot, target, Why: $"해제 {name}");
                }
            }
        }

        return null;
    }

    /// <summary>이 종류에서 마력이 닿는 가장 센 것.</summary>
    private static CompanionStep? Best(CompanionSight sight, CompanionSettings settings, CompanionSpells.Kind kind, uint target, bool empowered, int mana, string why) =>
        sight.Spells
            .Select(one => (Spell: one, Entry: CompanionSpells.Of(one.Name, empowered)))
            .Where(pair => pair.Entry is { } entry && entry.Kind == kind && entry.Mana <= mana && settings.Allows(pair.Spell.Name))
            .OrderByDescending(pair => pair.Entry!.Power)
            .Select(pair => new CompanionStep(CompanionAct.Cast, pair.Spell.Slot, target, Why: $"{why} {pair.Spell.Name}"))
            .FirstOrDefault();

    /// <summary>버프 — 마법 차례대로, 주인 먼저 그다음 자기. 서버가 알린 상태에 없을 때만(알림이 없으면 지속 시간이 다 지난 뒤).</summary>
    private CompanionStep? Buff(CompanionSight sight, CompanionSettings settings, bool empowered, int mana, bool ownerNear)
    {
        foreach (LearnedSpell spell in sight.Spells)
        {
            if (CompanionSpells.Of(spell.Name, empowered) is not { Kind: CompanionSpells.Kind.Buff } entry || entry.Mana > mana
                || !settings.Allows(spell.Name))
            {
                continue;
            }

            foreach (uint target in ownerNear ? new[] { sight.Master, sight.Me } : new[] { sight.Me })
            {
                string name = CompanionSpells.Bare(spell.Name);

                bool castLately = _buffed.TryGetValue((name, target), out TimeSpan at);

                if (sight.StatusesOf(target) is { } on)
                {
                    // 서버가 알린 상태로 — 걸려 있거나, 막 걸어 알림을 기다리는 중이면 건너뛴다.
                    if (on.Contains(entry.State) || (castLately && sight.Now - at < BuffConfirm))
                    {
                        continue;
                    }
                }
                else if (castLately && sight.Now - at < TimeSpan.FromSeconds(entry.Seconds) + BuffSlack)
                {
                    // 알림이 없으면 제 시계로(지속 시간이 다 지난 뒤).
                    continue;
                }

                _buffed[(name, target)] = sight.Now;
                return new(CompanionAct.Cast, spell.Slot, target, Why: $"버프 {name}");
            }
        }

        return null;
    }

    /// <summary>주인과 FollowFrom 칸 넘게 떨어지면 걷기 시작해 FollowTo 칸 안에 들면 선다. 길은 벽을 돌아간다.</summary>
    private CompanionStep? Follow(CompanionSight sight, CompanionSettings settings, TimeSpan now)
    {
        if (sight.OwnerAt is not { } owner)
        {
            _following = false;
            return null;
        }

        int distance = Reckon.Steps(owner, sight.Standing);

        if (distance > settings.FollowFrom)
        {
            _following = true;
        }
        else if (distance <= settings.FollowTo)
        {
            _following = false;
        }

        if (!_following)
        {
            return null;
        }

        return StepTo(sight, owner, now, "따라가기");
    }

    /// <summary>주인 쪽으로 한 칸 — 벽과 선 것, 서버가 되돌린 칸을 돌아간다. 걸음 사이·주문 뒤에는 기다린다.</summary>
    private CompanionStep StepTo(CompanionSight sight, Tile owner, TimeSpan now, string why)
    {
        if (now - _lastWalk < WalkGap || now - _lastCast < CastGap)
        {
            return new(CompanionAct.Wait, Why: "걸음 사이");
        }

        // 지난 걸음을 서버가 되돌렸으면(제자리) 그 칸을 막힌 칸으로 적는다.
        if (_stepped is { } last && sight.Standing == last.From)
        {
            _refused[last.To] = now;
        }

        _stepped = null;

        foreach (Tile old in _refused.Where(pair => now - pair.Value > RefusedFor).Select(pair => pair.Key).ToList())
        {
            _refused.Remove(old);
        }

        HashSet<Tile> occupied = [.. sight.Occupied];
        occupied.Remove(owner);
        bool Blocked(Tile tile) => tile != owner && (sight.Blocked(tile) || occupied.Contains(tile) || _refused.ContainsKey(tile));

        Direction toward = Pathing.StepTowards(sight.Standing, owner, Blocked) ?? Straight(sight.Standing, owner);
        (int dx, int dy) = Facing.TileStep(toward);

        _stepped = (sight.Standing, new Tile(sight.Standing.X + dx, sight.Standing.Y + dy));
        _lastWalk = now;
        return new(CompanionAct.Walk, Toward: toward, Why: why);
    }

    private static Direction Straight(Tile from, Tile to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
    }
}
