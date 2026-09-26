using System.Buffers.Binary;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.World;

/// <summary>동료 사이의 한쪽 — 봇에게는 주인, 사람에게는 동료 봇.</summary>
public sealed record CompanionTie(uint Serial, string Name);

/// <summary>
/// 동료 봇(성직자)을 부르고 보내는 선. <b>우리 확장이다.</b>
/// </summary>
/// <remarks>
/// <para>나가는 0xF1: 몸 한 바이트, 1 부르기 · 0 보내기 (서버 <c>ClientFormatF1</c> — 원작 클라이언트는 0x80 넘는 명령을
/// 보내지 않아 0xF0 월드맵 열기 다음 번호를 썼다).</para>
/// <para>오는 0x5E: 종류(1) · serial(4) · 이름(길이 한 바이트 + 글자). 종류 1 은 봇에게 "주인은 이 사람", 2 는 부른 사람에게
/// "동료는 이 봇". serial 0 이면 끝났다 (서버 <c>ServerFormat5E</c>).</para>
/// </remarks>
public static class Companion
{
    public const byte MasterKind = 1;
    public const byte CompanionKind = 2;

    public static byte[] Call() => [1];

    public static byte[] Dismiss() => [0];

    /// <summary>0x5E 를 읽는다. serial 0 이면 사이가 끝났다는 뜻이라 null.</summary>
    public static (byte Kind, CompanionTie? Tie) ReadTie(ReadOnlySpan<byte> body)
    {
        if (body.Length < 5)
        {
            throw new ProtocolException($"동료 안내가 5바이트보다 짧습니다 ({body.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body[1..]);
        string name = body.Length > 5 ? LegacyKoreanEncoding.DecodeStringA(body[5..], out _) : string.Empty;

        return (body[0], serial == 0 ? null : new CompanionTie(serial, name));
    }
}

/// <summary>동료 봇의 설정. 봇 프로그램의 설정 파일에서 바꿀 수 있다.</summary>
/// <param name="HealOwnerPercent">주인 체력이 이 % 아래면 회복.</param>
/// <param name="HealSelfPercent">자기 체력이 이 % 아래면 회복.</param>
/// <param name="FollowFrom">주인과 이만큼 넘게 떨어지면 따라 걷기 시작한다.</param>
/// <param name="FollowTo">따라 걷다가 이만큼 가까워지면 선다.</param>
public sealed record CompanionSettings(
    int HealOwnerPercent = 70,
    int HealSelfPercent = 50,
    int FollowFrom = 3,
    int FollowTo = 2);

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

    /// <summary>벽·맵 밖.</summary>
    public Func<Tile, bool> Blocked { get; init; } = _ => false;

    /// <summary>다른 사람·괴물이 선 칸(주인 칸 포함해도 된다 — 주인 칸은 따로 뺀다).</summary>
    public IReadOnlyCollection<Tile> Occupied { get; init; } = [];

    public required TimeSpan Now { get; init; }
}

/// <summary>
/// 5.99 성직자 회복·버프 마법의 값 — <c>성직자(비전직).txt</c> 의 SPELL_ 블록에서: 회복량은 위즈의 몇 배, 마력, 버프는
/// 몇 초. 쿠로는 신성력강화가 있으면 ×15·22마력, 없으면 ×8·15마력.
/// </summary>
public static class CompanionSpells
{
    public enum Kind
    {
        Heal,
        GroupHeal,
        Buff,
    }

    public sealed record Entry(Kind Kind, int Power, int Mana, int Seconds = 0);

    private static readonly Dictionary<string, Entry> Known = new(StringComparer.Ordinal)
    {
        ["쿠로"] = new(Kind.Heal, 8, 15),
        ["쿠라노"] = new(Kind.Heal, 20, 30),
        ["쿠라노소"] = new(Kind.Heal, 30, 70),
        ["수페라쿠라노"] = new(Kind.Heal, 40, 130),
        ["엑스쿠라노"] = new(Kind.Heal, 70, 350),
        ["쿠러스"] = new(Kind.GroupHeal, 15, 25),
        ["쿠라누스"] = new(Kind.GroupHeal, 30, 95),
        ["쿠라네라"] = new(Kind.GroupHeal, 40, 250),
        ["엑스쿠라네라"] = new(Kind.GroupHeal, 70, 430),
        ["호르라마"] = new(Kind.Buff, 0, 55, 120),
        ["에나르마"] = new(Kind.Buff, 0, 40, 150),
    };

    /// <summary>배운 마법 중 이 이름의 값. 신성력강화가 쿠로를 바꾼다.</summary>
    public static Entry? Of(string name, bool empowered)
    {
        name = Bare(name);

        return name == "쿠로" && empowered ? new Entry(Kind.Heal, 15, 22)
            : Known.TryGetValue(name, out Entry? entry) ? entry : null;
    }

    /// <summary>
    /// 서버가 마법 칸(0x17)에 붙여 보내는 숙련도 꼬리 " (Lev:1/100)" 를 뗀 이름. "리젠(Lev1)" 처럼 이름에 든 괄호는 둔다.
    /// </summary>
    public static string Bare(string name)
    {
        int tail = name.IndexOf(" (Lev:", StringComparison.Ordinal);

        return tail > 0 ? name[..tail] : name;
    }
}

/// <summary>
/// 동료 봇의 판단 — 엔진 없이. <see cref="AutoHunt" /> 처럼 우선순위 순으로 훑어 할 수 있는 첫 일 하나를 돌려준다:
/// 멈춤(혼수·죽음) &gt; 주인 회복 &gt; 자기 회복 &gt; 버프 유지 &gt; 따라가기 &gt; 쉬기 &gt; 기다림.
/// SleepHunter4 의 파티원 회복(<c>PlayerMacroState</c> 의 FlowerQueue — 체력이 기준 아래인 이를 먼저)과 버프 유지(지속 시간이
/// 끝나면 다시)를 본떴다.
/// </summary>
public sealed class CompanionBrain
{
    /// <summary>마법 하나를 쓰고 다음 무엇이든 하기까지. 서버는 걷는 중의 주문을 끊는다(CancelCastingWhenWalking).</summary>
    public static readonly TimeSpan CastGap = TimeSpan.FromSeconds(1);

    /// <summary>회복 사이 — 한 번 걸고 체력바(0x13)가 오르는 것을 본 뒤에.</summary>
    public static readonly TimeSpan HealGap = TimeSpan.FromMilliseconds(1500);

    /// <summary>걸음 사이. 서버 걷기 제한을 넉넉히 지킨다(시험들의 450ms 보다 느리게).</summary>
    public static readonly TimeSpan WalkGap = TimeSpan.FromMilliseconds(500);

    /// <summary>버프는 지속 시간이 다 지나고 이만큼 뒤에 다시 — 일찍 걸면 걸린 사람에게 "이미 걸려있습니다." 가 간다.</summary>
    public static readonly TimeSpan BuffSlack = TimeSpan.FromSeconds(1);

    /// <summary>이만큼보다 멀면 주인을 회복하지 않는다(화면 밖).</summary>
    public const int CastReach = 10;

    private static readonly TimeSpan Never = TimeSpan.FromDays(-365);

    private readonly Dictionary<(string Spell, uint Target), TimeSpan> _buffed = [];
    private TimeSpan _lastCast = Never;
    private TimeSpan _lastHeal = Never;
    private TimeSpan _lastWalk = Never;
    private uint _master;
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

        if (sight.Comatose || sight.Vitals is { MaximumHealth: > 0, Health: <= 0 })
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

        if (canCast && now - _lastHeal >= HealGap && (ownerHurt || selfHurt))
        {
            CompanionStep? heal = ownerHurt && selfHurt ? Best(sight, CompanionSpells.Kind.GroupHeal, sight.Master, empowered, mana, "파티 회복") : null;
            heal ??= ownerHurt
                ? Best(sight, CompanionSpells.Kind.Heal, sight.Master, empowered, mana, "주인 회복")
                : Best(sight, CompanionSpells.Kind.Heal, sight.Me, empowered, mana, "자기 회복");

            if (heal is not null)
            {
                _lastCast = now;
                _lastHeal = now;
                return heal;
            }
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

        int cheapest = sight.Spells
            .Select(one => CompanionSpells.Of(one.Name, empowered))
            .Where(entry => entry is { Kind: CompanionSpells.Kind.Heal })
            .Select(entry => entry!.Mana)
            .DefaultIfEmpty(0)
            .Min();

        return mana < cheapest
            ? new(CompanionAct.Rest, Why: "마력 부족")
            : new(CompanionAct.Wait, Why: "기다림");
    }

    /// <summary>이 종류에서 마력이 닿는 가장 센 것.</summary>
    private static CompanionStep? Best(CompanionSight sight, CompanionSpells.Kind kind, uint target, bool empowered, int mana, string why) =>
        sight.Spells
            .Select(one => (Spell: one, Entry: CompanionSpells.Of(one.Name, empowered)))
            .Where(pair => pair.Entry is { } entry && entry.Kind == kind && entry.Mana <= mana)
            .OrderByDescending(pair => pair.Entry!.Power)
            .Select(pair => new CompanionStep(CompanionAct.Cast, pair.Spell.Slot, target, Why: $"{why} {pair.Spell.Name}"))
            .FirstOrDefault();

    /// <summary>버프 — 마법 차례대로, 주인 먼저 그다음 자기. 지속 시간이 다 지난 것만.</summary>
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

                if (_buffed.TryGetValue((name, target), out TimeSpan at)
                    && sight.Now - at < TimeSpan.FromSeconds(entry.Seconds) + BuffSlack)
                {
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

        if (now - _lastWalk < WalkGap || now - _lastCast < CastGap)
        {
            return new(CompanionAct.Wait, Why: "걸음 사이");
        }

        HashSet<Tile> occupied = [.. sight.Occupied];
        occupied.Remove(owner);
        bool Blocked(Tile tile) => tile != owner && (sight.Blocked(tile) || occupied.Contains(tile));

        Direction toward = Pathing.StepTowards(sight.Standing, owner, Blocked) ?? Straight(sight.Standing, owner);

        _lastWalk = now;
        return new(CompanionAct.Walk, Toward: toward, Why: "따라가기");
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

    private static int Percent(Vitals? vitals) =>
        vitals is { MaximumHealth: > 0 } known ? (int)(known.Health * 100L / known.MaximumHealth) : 100;
}
