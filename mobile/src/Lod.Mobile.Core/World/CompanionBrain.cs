using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.World;

/// <summary>동료 봇의 설정. 봇 프로그램의 설정 파일에서 바꿀 수 있다.</summary>
/// <param name="HealOwnerPercent">주인 체력이 이 % 아래면 회복.</param>
/// <param name="HealSelfPercent">자기 체력이 이 % 아래면 회복.</param>
/// <param name="FollowFrom">주인과 이만큼 넘게 떨어지면 따라 걷기 시작한다.</param>
/// <param name="FollowTo">따라 걷다가 이만큼 가까워지면 선다.</param>
/// <param name="PotionHealthPercent">자기 체력이 이 % 아래면 체력 포션.</param>
/// <param name="PotionManaPercent">자기 마력이 이 % 아래면 마력 포션(가장 싼 회복도 못 걸 마력이면 그 전에라도).</param>
public sealed record CompanionSettings(
    int HealOwnerPercent = 70,
    int HealSelfPercent = 50,
    int FollowFrom = 3,
    int FollowTo = 2,
    int PotionHealthPercent = 40,
    int PotionManaPercent = 30);

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

    /// <summary>다른 사람·괴물이 선 칸(주인 칸 포함해도 된다 — 주인 칸은 따로 뺀다).</summary>
    public IReadOnlyCollection<Tile> Occupied { get; init; } = [];

    public required TimeSpan Now { get; init; }
}

/// <summary>
/// 동료 봇의 판단 — 엔진 없이. <see cref="AutoHunt" /> 처럼 우선순위 순으로 훑어 할 수 있는 첫 일 하나를 돌려준다:
/// 멈춤(혼수·죽음·유령) &gt; 주인 혼수 깨우기 &gt; 해제(수면·빙결) &gt; 주인 회복 &gt; 봇 체력 포션 &gt; 자기 회복 마법 &gt; 봇 마력 포션 &gt; 버프 유지 &gt; 따라가기 &gt; 쉬기 &gt; 기다림.
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

    private static readonly TimeSpan Never = TimeSpan.FromDays(-365);

    private readonly Dictionary<(string Spell, uint Target), TimeSpan> _buffed = [];
    private TimeSpan _lastCast = Never;
    private TimeSpan _lastHeal = Never;
    private TimeSpan _lastWalk = Never;
    private TimeSpan _lastDrink = Never;
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

        TimeSpan now = sight.Now;
        bool canCast = now - _lastCast >= CastGap;
        bool ownerNear = sight.OwnerAt is { } at && Distance(at, sight.Standing) <= CastReach;
        int ownerHealth = ownerNear ? sight.HealthOf(sight.Master) ?? 100 : 100;
        bool ownerHurt = ownerNear && ownerHealth > 0 && ownerHealth < settings.HealOwnerPercent;
        bool selfHurt = Percent(sight.Vitals) < settings.HealSelfPercent;
        bool empowered = sight.Spells.Any(one => CompanionSpells.Bare(one.Name) == "신성력강화");
        int mana = sight.Vitals?.Mana ?? 0;

        bool canHeal = canCast && now - _lastHeal >= HealGap;
        bool canDrink = now - _lastDrink >= DrinkGap;
        int cheapest = sight.Spells
            .Select(one => CompanionSpells.Of(one.Name, empowered))
            .Where(entry => entry is { Kind: CompanionSpells.Kind.Heal })
            .Select(entry => entry!.Mana)
            .DefaultIfEmpty(0)
            .Min();

        // 주인이 혼수면 가장 먼저 — 옆 칸으로 가서 깨운다(사용자 결정 2026-09-26, 서버 0xF1 5 — 코마디움과 같은 효과, 아무것도 안 쓴다).
        if (sight.OwnerAt is { } fallen && sight.StatusesOf(sight.Master)?.Contains("skulled") == true)
        {
            if (Distance(fallen, sight.Standing) > 1)
            {
                return StepTo(sight, fallen, now, "주인 깨우러 가기");
            }

            if (now - _lastCast >= CastGap)
            {
                _lastCast = now;
                return new(CompanionAct.WakeOwner, Target: sight.Master, Why: "주인 깨우기");
            }

            return new(CompanionAct.Wait, Why: "주인 깨우기 사이");
        }

        // 해제가 가장 먼저 — 수면(나르콜리)·빙결이면 주인은 아무것도 못 한다(사용자, 2026-09-26). 주문 사이(1초)를 다 기다리지
        // 않는다(CureGap) — 막 버프를 걸었어도 곧 푼다.
        if (now - _lastCast >= CureGap && Cure(sight, empowered, mana, ownerNear) is { } cure)
        {
            return Cast(cure, now, heal: false);
        }

        // 주인 회복(둘 다 아프면 파티 회복).
        if (canHeal && ownerHurt)
        {
            CompanionStep? heal = (selfHurt ? Best(sight, CompanionSpells.Kind.GroupHeal, sight.Master, empowered, mana, "파티 회복") : null)
                                  ?? Best(sight, CompanionSpells.Kind.Heal, sight.Master, empowered, mana, "주인 회복");

            if (heal is not null)
            {
                return Cast(heal, now, heal: true);
            }
        }

        // 봇 체력 포션 — 회복 마법보다 먼저(마력을 아낀다).
        if (canDrink && Percent(sight.Vitals) < settings.PotionHealthPercent
            && Potion(sight.Pack, CompanionSpells.HealthRestore, Missing(sight.Vitals?.MaximumHealth, sight.Vitals?.Health)) is { } health)
        {
            _lastDrink = now;
            return new(CompanionAct.Drink, health.Slot, Why: $"체력 포션 {health.Name}");
        }

        if (canHeal && selfHurt && Best(sight, CompanionSpells.Kind.Heal, sight.Me, empowered, mana, "자기 회복") is { } self)
        {
            return Cast(self, now, heal: true);
        }

        // 봇 마력 포션 — 마력이 낮거나, 가장 싼 회복도 못 걸어 쉬어야 할 때.
        if (canDrink && (ManaPercent(sight.Vitals) < settings.PotionManaPercent || mana < cheapest)
            && Potion(sight.Pack, CompanionSpells.ManaRestore, Missing(sight.Vitals?.MaximumMana, sight.Vitals?.Mana)) is { } restoring)
        {
            _lastDrink = now;
            return new(CompanionAct.Drink, restoring.Slot, Why: $"마력 포션 {restoring.Name}");
        }

        if (canCast && Buff(sight, empowered, mana, ownerNear) is { } buff)
        {
            _lastCast = now;
            return buff;
        }

        if (Follow(sight, settings, now) is { } step)
        {
            return step;
        }

        return mana < cheapest
            ? new(CompanionAct.Rest, Why: "마력 부족")
            : new(CompanionAct.Wait, Why: "기다림");
    }

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
    private CompanionStep? Cure(CompanionSight sight, bool empowered, int mana, bool ownerNear)
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

                if (CompanionSpells.Of(name, empowered) is { Kind: CompanionSpells.Kind.Cure } entry && entry.Mana <= mana
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
    private static CompanionStep? Best(CompanionSight sight, CompanionSpells.Kind kind, uint target, bool empowered, int mana, string why) =>
        sight.Spells
            .Select(one => (Spell: one, Entry: CompanionSpells.Of(one.Name, empowered)))
            .Where(pair => pair.Entry is { } entry && entry.Kind == kind && entry.Mana <= mana)
            .OrderByDescending(pair => pair.Entry!.Power)
            .Select(pair => new CompanionStep(CompanionAct.Cast, pair.Spell.Slot, target, Why: $"{why} {pair.Spell.Name}"))
            .FirstOrDefault();

    /// <summary>버프 — 마법 차례대로, 주인 먼저 그다음 자기. 서버가 알린 상태에 없을 때만(알림이 없으면 지속 시간이 다 지난 뒤).</summary>
    private CompanionStep? Buff(CompanionSight sight, bool empowered, int mana, bool ownerNear)
    {
        foreach (LearnedSpell spell in sight.Spells)
        {
            if (CompanionSpells.Of(spell.Name, empowered) is not { Kind: CompanionSpells.Kind.Buff } entry || entry.Mana > mana)
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

        int distance = Distance(owner, sight.Standing);

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

    private static int Distance(Tile a, Tile b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static int ManaPercent(Vitals? vitals) =>
        vitals is { MaximumMana: > 0 } known ? (int)(known.Mana * 100L / known.MaximumMana) : 100;

    private static int Percent(Vitals? vitals) =>
        vitals is { MaximumHealth: > 0 } known ? (int)(known.Health * 100L / known.MaximumHealth) : 100;
}
