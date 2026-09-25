using Lod.Mobile.Core.Art;

namespace Lod.Mobile.Core.World;

/// <summary>자동 사냥의 두 설정 — 켠 자리에서 몇 칸까지 쫓나, 체력 몇 % 이하에서 회복 기술을 쓰나.</summary>
public sealed record AutoHuntSettings(int Radius = AutoHuntSettings.DefaultRadius, int HealPercent = AutoHuntSettings.DefaultHealPercent)
{
    public const int DefaultRadius = 12;
    public const int DefaultHealPercent = 50;

    /// <summary>이 아래로 떨어졌는데 포션도 회복 기술도 없으면 멈춘다.</summary>
    public const int DangerPercent = 20;

    /// <summary>설정 파일 한 줄("12 50")로. potion.cfg 와 같은 결 — 한 줄에 숫자 둘.</summary>
    public string ToLine() => $"{Radius} {HealPercent}";

    /// <summary>한 줄을 읽는다. 틀리거나 범위를 벗어나면 기본값.</summary>
    public static AutoHuntSettings Parse(string? line)
    {
        string[] parts = (line ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 2
               && int.TryParse(parts[0], out int radius) && radius is >= 1 and <= 30
               && int.TryParse(parts[1], out int heal) && heal is > 0 and < 100
            ? new AutoHuntSettings(radius, heal)
            : new AutoHuntSettings();
    }
}

/// <summary>자동 사냥이 이번 틱에 하려는 일 하나.</summary>
public enum HuntAct
{
    /// <summary>할 일이 없다 — 가만히 있는다.</summary>
    Wait,

    /// <summary>자동 사냥을 끈다. <see cref="HuntStep.Why"/> 가 한 줄 알림이다.</summary>
    Stop,

    /// <summary>회복 마법을 자기에게 쓴다(<see cref="HuntStep.Slot"/> 은 마법 칸).</summary>
    Heal,

    /// <summary>한 칸 걷는다(<see cref="HuntStep.Toward"/>).</summary>
    Walk,

    /// <summary>제자리에서 돌아선다(<see cref="HuntStep.Toward"/>).</summary>
    Face,

    /// <summary>평타 한 번.</summary>
    Strike,

    /// <summary>기술 칸 하나를 쓴다(<see cref="HuntStep.Slot"/> 은 기술 칸).</summary>
    Skill,
}

/// <param name="Why">무엇 때문에 — 멈출 때는 사람에게 보일 한 줄, 그 밖에는 시험·기록용.</param>
public sealed record HuntStep(HuntAct Act, Direction Toward = Direction.South, int Slot = 0, uint Target = 0, string Why = "");

/// <summary>한 틱에 자동 사냥이 보는 세상. 앱이 WorldClient·화면에서 모아 넘긴다.</summary>
public sealed record HuntSight
{
    public required Tile Standing { get; init; }
    public required Direction Facing { get; init; }
    public required int MapId { get; init; }
    public required Vitals? Vitals { get; init; }
    public bool Comatose { get; init; }
    public required IReadOnlyCollection<Creature> Creatures { get; init; }

    /// <summary>괴물 체력 백분율(0x13). 모르면 null.</summary>
    public Func<uint, int?> HealthOf { get; init; } = _ => null;

    /// <summary>다른 사람이 요즘 치고 있는 괴물인가.</summary>
    public Func<uint, bool> FoughtByOthers { get; init; } = _ => false;

    /// <summary>기술 부채꼴에 놓인 기술.</summary>
    public IReadOnlyList<LearnedSkill> Skills { get; init; } = [];

    /// <summary>배운 마법 전부 — 회복 계열을 여기서 찾는다.</summary>
    public IReadOnlyList<LearnedSpell> Spells { get; init; } = [];

    /// <summary>(기술인가, 칸) → 남은 초. 0 이면 쓸 수 있다.</summary>
    public Func<bool, int, int> Cooling { get; init; } = (_, _) => 0;

    /// <summary>자동 포션이 켜져 있고 그 체력 포션이 가방에 있다.</summary>
    public bool PotionReady { get; init; }

    public bool AutoLoot { get; init; }

    /// <summary>벽·맵 밖.</summary>
    public Func<Tile, bool> Blocked { get; init; } = _ => false;

    /// <summary>다른 사람이 선 칸 — 지나갈 수 없다.</summary>
    public IReadOnlyCollection<Tile> People { get; init; } = [];

    public required TimeSpan Now { get; init; }
}

/// <summary>
/// 자동 사냥의 판단 — 엔진 없이. 봇 참고 저장소(ETDA BotCore <c>GameStateEngine</c>)처럼 우선순위 순으로 훑어 할 수
/// 있는 첫 일 하나를 돌려준다: 멈춤 &gt; 회복 &gt; 줍기 &gt; 공격 &gt; 다가가기 &gt; 돌아가기 &gt; 기다림.
/// </summary>
/// <remarks>
/// 걷는 빠르기는 여기서 재지 않는다 — 앱이 한 걸음이 끝난 뒤에만 <see cref="Next"/> 를 부르고, 그 한 걸음의 길이
/// (<c>WorldView.StepSeconds</c>)가 서버 걷기 제한을 이미 지킨다. 평타·기술 간격은 여기서 잰다.
/// </remarks>
public sealed class AutoHunt
{
    /// <summary>평타 사이. 서버 GlobalBaseSkillDelay 가 500ms — 수동 사냥(<c>SwingFrames</c> 40프레임)과 같게.</summary>
    public static readonly TimeSpan StrikeGap = TimeSpan.FromMilliseconds(670);

    /// <summary>무엇이든 하나 쓰고 다음 것을 쓰기까지. 서버가 한꺼번에 온 것을 거절한다.</summary>
    public static readonly TimeSpan ActGap = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 쓴 기술에 대기 안내(0x3F)가 끝내 안 오면 서버가 거절한 것이다(마력 부족 등). 그 기술은 이만큼 쉬고 평타를 친다 —
    /// 안 그러면 거절될 기술만 되풀이하느라 평타를 한 번도 못 친다(격리 서버에서 실제로 2분에 평타 2번).
    /// </summary>
    public static readonly TimeSpan SkillRetry = TimeSpan.FromSeconds(6);

    /// <summary>회복 마법 사이 — 한 번 쓰고 체력이 오르는 것을 보고 다시.</summary>
    public static readonly TimeSpan HealGap = TimeSpan.FromMilliseconds(1500);

    /// <summary>갈 수 없던 괴물을 다시 고르지 않는 시간.</summary>
    public static readonly TimeSpan Shunned = TimeSpan.FromSeconds(10);

    /// <summary>잡은 괴물이 떨군 것을 이만큼 기다린다. 그 안에 바닥에 아무것도 없으면 잊는다.</summary>
    public static readonly TimeSpan DropGrace = TimeSpan.FromMilliseconds(1500);

    /// <summary>떨어진 칸을 이만큼 지나면 잊는다(주우러 가다 못 가면).</summary>
    public static readonly TimeSpan DropForget = TimeSpan.FromSeconds(12);

    /// <summary>한 대상에게 걷는데 자리가 이만큼 그대로면 막혔다고 본다.</summary>
    public const int StuckSteps = 6;

    private const int Reach = 30;

    // TimeSpan.MinValue 에서 빼면 넘친다 — "한 번도 안 했다"는 충분히 먼 옛날로.
    private static readonly TimeSpan Never = TimeSpan.FromDays(-365);

    private readonly Dictionary<uint, TimeSpan> _shunned = [];
    private readonly Dictionary<int, (TimeSpan At, bool Cooled)> _skillUsed = [];
    private readonly List<(Tile Where, TimeSpan At)> _drops = [];

    private int _map;
    private uint _target;
    private Tile _targetWhere;
    private bool _struckTarget;
    private TimeSpan _lastStrike = Never;
    private TimeSpan _lastAct = Never;
    private TimeSpan _lastHeal = Never;
    private TimeSpan _pausedUntil = Never;
    private Tile? _walkedFrom;
    private int _stuck;

    public bool On { get; private set; }

    /// <summary>켠 자리 — 반경을 재는 중심.</summary>
    public Tile Home { get; private set; }

    /// <summary>지금 노리는 괴물, 없으면 0.</summary>
    public uint Target => _target;

    public void Start(Tile home, int mapId)
    {
        On = true;
        Home = home;
        _map = mapId;
        _target = 0;
        _drops.Clear();
        _shunned.Clear();
        _stuck = 0;
        _walkedFrom = null;
        _pausedUntil = Never;
    }

    public void Stop()
    {
        On = false;
        _target = 0;
    }

    /// <summary>
    /// 사람이 방향판을 눌렀다 — 그 동안과 뒤 <paramref name="seconds"/> 초는 손이 이긴다. 옮긴 자리가 새 중심이다
    /// (사냥터를 옮긴 것이지 자동 사냥을 그만둔 것이 아니다).
    /// </summary>
    public void Steered(Tile standing, TimeSpan now, double seconds = 3)
    {
        Home = standing;
        Pause(now, seconds);
    }

    /// <summary>사람이 공격 단추를 눌렀다 — 잠시 손에 맡긴다. 중심은 그대로.</summary>
    public void Pause(TimeSpan now, double seconds = 3)
    {
        _pausedUntil = now + TimeSpan.FromSeconds(seconds);
        _target = 0;
        _stuck = 0;
    }

    public bool Paused(TimeSpan now) => now < _pausedUntil;

    public HuntStep Next(HuntSight sight, AutoHuntSettings settings)
    {
        if (!On)
        {
            return new(HuntAct.Wait, Why: "꺼짐");
        }

        if (Stopping(sight) is { } why)
        {
            Stop();
            return new(HuntAct.Stop, Why: why);
        }

        TimeSpan now = sight.Now;

        if (Paused(now))
        {
            return new(HuntAct.Wait, Why: "손이 조작 중");
        }

        Remember(sight);

        return Heal(sight, settings)
               ?? Loot(sight, settings)
               ?? Fight(sight, settings)
               ?? GoHome(sight)
               ?? new HuntStep(HuntAct.Wait, Why: "기다림");
    }

    /// <summary>회복 마법인가 — 쿠로·쿠로토, ioc 계열, nuadhaich.</summary>
    public static bool IsHealing(string name) =>
        name.StartsWith("쿠로", StringComparison.Ordinal)
        || name.Contains("ioc", StringComparison.OrdinalIgnoreCase)
        || name.Contains("nuadhaich", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 싸움에 쓸 기술인가. 회복·몸 옮기기(이형환위 — 서버가 우리를 괴물 뒤로 옮겨 화면이 믿는 칸이 틀어진다)·
    /// 감정·지식 기술은 뺀다.
    /// </summary>
    public static bool IsForFighting(string name) =>
        !IsHealing(name)
        && !name.StartsWith("이형환위", StringComparison.Ordinal)
        && !NotFighting.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] NotFighting =
        ["lore", "appraise", "analyze", "evaluate", "hairstyle", "sense", "mend", "study"];

    private string? Stopping(HuntSight sight)
    {
        if (sight.MapId != _map)
        {
            return "맵이 바뀌어 자동 사냥을 껐습니다.";
        }

        if (sight.Comatose || sight.Vitals is { MaximumHealth: > 0, Health: <= 0 })
        {
            return "쓰러져 자동 사냥을 멈췄습니다.";
        }

        if (Percent(sight.Vitals) < AutoHuntSettings.DangerPercent
            && !sight.PotionReady
            && !sight.Spells.Any(spell => IsHealing(spell.Name)))
        {
            return "체력이 낮은데 포션도 회복 기술도 없어 자동 사냥을 멈췄습니다.";
        }

        return null;
    }

    private static int Percent(Vitals? vitals) =>
        vitals is { MaximumHealth: > 0 } known ? (int)(known.Health * 100L / known.MaximumHealth) : 100;

    /// <summary>노리던 괴물이 사라졌으면 그 자리를 떨어진 칸으로 적어 둔다.</summary>
    private void Remember(HuntSight sight)
    {
        foreach (uint gone in _shunned.Where(pair => pair.Value <= sight.Now).Select(pair => pair.Key).ToArray())
        {
            _shunned.Remove(gone);
        }

        if (_target == 0)
        {
            return;
        }

        if (sight.Creatures.FirstOrDefault(one => one.Serial == _target && one.Kind == CreatureKind.Hostile) is { } alive)
        {
            _targetWhere = alive.Where;
            return;
        }

        if (_struckTarget && sight.AutoLoot)
        {
            _drops.Add((_targetWhere, sight.Now));
        }

        _target = 0;
        _stuck = 0;
    }

    private HuntStep? Heal(HuntSight sight, AutoHuntSettings settings)
    {
        if (Percent(sight.Vitals) > settings.HealPercent
            || sight.Now - _lastHeal < HealGap
            || sight.Now - _lastAct < ActGap)
        {
            return null;
        }

        LearnedSpell? spell = sight.Spells.FirstOrDefault(one => IsHealing(one.Name) && sight.Cooling(false, one.Slot) == 0);

        if (spell is null)
        {
            return null;
        }

        _lastHeal = sight.Now;
        _lastAct = sight.Now;
        return new(HuntAct.Heal, Slot: spell.Slot, Why: spell.Name);
    }

    private HuntStep? Loot(HuntSight sight, AutoHuntSettings settings)
    {
        if (!sight.AutoLoot)
        {
            _drops.Clear();
            return null;
        }

        HashSet<Tile> lying = [.. sight.Creatures.Where(one => one.Kind == CreatureKind.Passable).Select(one => one.Where)];

        _drops.RemoveAll(drop =>
            sight.Now - drop.At > DropForget
            || drop.Where == sight.Standing
            || Distance(drop.Where, Home) > settings.Radius
            || (sight.Now - drop.At > DropGrace && !lying.Contains(drop.Where)));

        Func<Tile, bool> blocked = Blocking(sight);

        foreach ((Tile where, _) in _drops.Where(drop => lying.Contains(drop.Where)).OrderBy(drop => Distance(drop.Where, sight.Standing)).ToArray())
        {
            if (Pathing.Way(sight.Standing, where, blocked, Reach) is { Count: > 0 } way)
            {
                return Walk(sight, way[0], "줍기");
            }

            _drops.RemoveAll(drop => drop.Where == where);
        }

        return null;
    }

    private HuntStep? Fight(HuntSight sight, AutoHuntSettings settings)
    {
        Creature? prey = Choose(sight, settings);

        if (prey is null)
        {
            return null;
        }

        if (prey.Serial != _target)
        {
            _target = prey.Serial;
            _struckTarget = false;
            _stuck = 0;
            _walkedFrom = null;
        }

        _targetWhere = prey.Where;

        if (Distance(prey.Where, sight.Standing) == 1)
        {
            _stuck = 0;
            Direction toward = TabMap.StepOf(sight.Standing, prey.Where);

            if (sight.Facing != toward)
            {
                return new(HuntAct.Face, toward, Target: prey.Serial, Why: "돌아서기");
            }

            if (sight.Now - _lastAct < ActGap)
            {
                return new(HuntAct.Wait, Target: prey.Serial, Why: "다음 공격 기다림");
            }

            foreach ((int slot, (TimeSpan at, bool cooled)) in _skillUsed.ToArray())
            {
                if (!cooled && sight.Cooling(true, slot) > 0)
                {
                    _skillUsed[slot] = (at, true);
                }
            }

            LearnedSkill? skill = sight.Skills.FirstOrDefault(one =>
                IsForFighting(one.Name)
                && sight.Cooling(true, one.Slot) == 0
                && (!_skillUsed.TryGetValue(one.Slot, out (TimeSpan At, bool Cooled) used)
                    || used.Cooled
                    || sight.Now - used.At >= SkillRetry));

            if (skill is not null)
            {
                _skillUsed[skill.Slot] = (sight.Now, false);
                _lastAct = sight.Now;
                _struckTarget = true;
                return new(HuntAct.Skill, Slot: skill.Slot, Target: prey.Serial, Why: skill.Name);
            }

            if (sight.Now - _lastStrike >= StrikeGap)
            {
                _lastStrike = sight.Now;
                _lastAct = sight.Now;
                _struckTarget = true;
                return new(HuntAct.Strike, Target: prey.Serial, Why: "평타");
            }

            return new(HuntAct.Wait, Target: prey.Serial, Why: "다음 공격 기다림");
        }

        // 다가가기 — 괴물 옆의 빈 칸 중 가장 가까운 곳으로. 벽은 돌아가고, 길이 없거나 막혀 제자리면 다른 대상.
        _stuck = _walkedFrom == sight.Standing ? _stuck + 1 : 0;

        if (_stuck >= StuckSteps)
        {
            return Shun(prey, sight.Now, "막힘");
        }

        Func<Tile, bool> blocked = Blocking(sight);
        IReadOnlyList<Tile>? best = null;

        foreach (Tile beside in Around(prey.Where))
        {
            if (blocked(beside))
            {
                continue;
            }

            if (Pathing.Way(sight.Standing, beside, blocked, Reach) is { Count: > 0 } way && (best is null || way.Count < best.Count))
            {
                best = way;
            }
        }

        if (best is null)
        {
            return Shun(prey, sight.Now, "길 없음");
        }

        _walkedFrom = sight.Standing;
        return Walk(sight, best[0], "다가가기") with { Target = prey.Serial };
    }

    private HuntStep Shun(Creature prey, TimeSpan now, string why)
    {
        _shunned[prey.Serial] = now + Shunned;
        _target = 0;
        _stuck = 0;
        _walkedFrom = null;
        return new(HuntAct.Wait, Target: prey.Serial, Why: why);
    }

    /// <summary>
    /// 반경 안의 괴물 중 가장 가까운 것, 같으면 체력 낮은 것. 다른 사람이 치고 있는 것은 다른 게 있으면 피한다.
    /// 노리던 것이 아직 쓸 만하면 바꾸지 않는다 — 한 대 치고 옆으로 옮겨 다니면 아무것도 못 잡는다.
    /// </summary>
    private Creature? Choose(HuntSight sight, AutoHuntSettings settings)
    {
        Creature[] inRange = sight.Creatures
            .Where(one => one.Kind == CreatureKind.Hostile
                          && Distance(one.Where, Home) <= settings.Radius
                          && !_shunned.ContainsKey(one.Serial))
            .ToArray();

        Creature[] free = inRange.Where(one => !sight.FoughtByOthers(one.Serial)).ToArray();
        Creature[] pool = free.Length > 0 ? free : inRange;

        if (_target != 0 && pool.FirstOrDefault(one => one.Serial == _target) is { } kept)
        {
            return kept;
        }

        return pool
            .OrderBy(one => Distance(one.Where, sight.Standing))
            .ThenBy(one => sight.HealthOf(one.Serial) ?? 100)
            .ThenBy(one => one.Serial)
            .FirstOrDefault();
    }

    private HuntStep? GoHome(HuntSight sight)
    {
        if (Distance(sight.Standing, Home) <= 1)
        {
            return null;
        }

        return Pathing.Way(sight.Standing, Home, Blocking(sight), Reach) is { Count: > 0 } way
            ? Walk(sight, way[0], "돌아가기")
            : null;
    }

    private static HuntStep Walk(HuntSight sight, Tile next, string why) =>
        new(HuntAct.Walk, TabMap.StepOf(sight.Standing, next), Why: why);

    /// <summary>벽, 그리고 괴물·NPC·사람이 선 칸. 바닥의 물건은 밟고 지나간다.</summary>
    private static Func<Tile, bool> Blocking(HuntSight sight)
    {
        HashSet<Tile> taken = [.. sight.Creatures.Where(one => one.Kind != CreatureKind.Passable).Select(one => one.Where)];
        taken.UnionWith(sight.People);
        return tile => sight.Blocked(tile) || taken.Contains(tile);
    }

    private static int Distance(Tile one, Tile other) => Math.Abs(one.X - other.X) + Math.Abs(one.Y - other.Y);

    private static IEnumerable<Tile> Around(Tile tile)
    {
        yield return new Tile(tile.X, tile.Y - 1);
        yield return new Tile(tile.X + 1, tile.Y);
        yield return new Tile(tile.X, tile.Y + 1);
        yield return new Tile(tile.X - 1, tile.Y);
    }
}
