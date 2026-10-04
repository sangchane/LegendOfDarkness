using System.Diagnostics;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.CompanionBot;

/// <summary>
/// 판단(<see cref="CompanionBrain" />, 알맹이)을 접속 하나에 잇는다 — 0.1초마다 보고, 하나 하고.
/// </summary>
/// <remarks>
/// 서버는 걸음이 된 것을 걸은 이에게 알리지 않는다(<c>Sprite.Walk</c> 는 곁의 사람에게만 0x0C). 막혔을 때만 자리를
/// 다시 보낸다(0x04). 그래서 앱처럼 제 칸을 스스로 옮기고, 서버가 자리를 보내면 그것으로 바로잡는다.
/// </remarks>
public sealed class CompanionRunner(WorldClient world, MapWalls walls, CompanionSettings settings, Action<string>? log = null)
{
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    /// <summary>서버는 10초마다 심장박동을 보낸다 — 이만큼 아무것도 안 오면 한 줄 남긴다.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(30);

    /// <summary>이만큼 아무것도 안 오면 접속이 죽은 것으로 보고 끊고 다시 들어간다(소켓이 닫히지 않은 채 조용해지면 받기가 끝나지 않는다).</summary>
    public static readonly TimeSpan Dead = TimeSpan.FromSeconds(90);

    /// <summary>주인이 이만큼 안 보이거나(다른 맵·시야 밖) 멀면 한 줄 남긴다.</summary>
    public static readonly TimeSpan Away = TimeSpan.FromSeconds(15);

    /// <summary>이만큼 안에 친(맞은) 것을 "싸우는 중" 으로 본다 — 저주·나르콜리 고르기.</summary>
    public static readonly TimeSpan Fighting = TimeSpan.FromSeconds(3);

    /// <summary>이만큼마다 한 줄 요약.</summary>
    public static readonly TimeSpan Summary = TimeSpan.FromMinutes(5);

    private readonly CompanionBrain _brain = new();

    // 괴물 serial → 주인이 마지막으로 친 때. 0x5D 는 마지막으로 친 이 하나만 남겨, 파티원이 뒤에 치면 주인 몫이 지워진다.
    private readonly Dictionary<uint, TimeSpan> _ownerHit = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Tile _tile;
    private int _reports = -1;
    private int _map = -1;
    private string _said = string.Empty;
    private uint _master;

    // 기록용(상태가 바뀔 때만 적는다).
    private readonly Dictionary<CompanionAct, int> _done = [];
    private TimeSpan _nextSummary = Summary;
    private TimeSpan? _awaySince;
    private bool _awayTold;
    private bool _quietTold;
    private int _unreadSeen;
    private int _errors;
    private string _lastError = string.Empty;
    private TimeSpan _lastErrorAt = TimeSpan.MinValue;
    private int _loggedMap = -1;

    /// <summary>판단 루프가 스스로 멈춘 까닭(서버 소식이 끊김 등). 돌고 있으면 null.</summary>
    public string? Stopped { get; private set; }

    /// <summary>접속이 끊기거나 멈추라 할 때까지 돈다.</summary>
    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !world.IsDisposed && world.Broke is null)
        {
            try
            {
                CompanionStep? step = await Once(token);

                if (step is { } did)
                {
                    _done[did.Act] = _done.GetValueOrDefault(did.Act) + 1;
                }
            }
            catch (Exception failed) when (!token.IsCancellationRequested)
            {
                // 한 번 틀려도 판단은 계속 돈다 — 전에는 예외 하나가 말없이 루프를 끝냈다.
                _errors++;

                string what = BotLogin.Describe(failed);
                if (what != _lastError || _clock.Elapsed - _lastErrorAt > TimeSpan.FromMinutes(1))
                {
                    log?.Invoke($"판단 중 예외({_errors}번째): {what}{Environment.NewLine}{failed.StackTrace}");
                    _lastError = what;
                    _lastErrorAt = _clock.Elapsed;
                }
            }

            if (Watch() is { } why)
            {
                Stopped = why;
                return;
            }

            await Task.Delay(Tick, token);
        }
    }

    /// <summary>먹통을 뒤에 가려낼 수 있게 — 상태가 바뀔 때와 <see cref="Summary" /> 마다만 적는다. 끊을 까닭이 있으면 돌려준다.</summary>
    private string? Watch()
    {
        TimeSpan now = _clock.Elapsed;
        TimeSpan silent = DateTime.UtcNow - world.LastHeard;

        if (silent >= Dead)
        {
            return $"서버 소식이 {silent.TotalSeconds:0}초째 없어 다시 접속합니다";
        }

        if (silent >= Quiet && !_quietTold)
        {
            _quietTold = true;
            log?.Invoke($"서버 소식이 {silent.TotalSeconds:0}초째 없습니다(심장박동 10초).");
        }
        else if (silent < Quiet && _quietTold)
        {
            _quietTold = false;
            log?.Invoke("서버 소식이 다시 옵니다.");
        }

        if (world.UnreadCount != _unreadSeen)
        {
            _unreadSeen = world.UnreadCount;
            log?.Invoke($"못 읽은 패킷(누적 {_unreadSeen}): {world.Unread}");
        }

        if (world.State is not { } state)
        {
            return null;
        }

        if (state.Map.Id != _loggedMap)
        {
            log?.Invoke(_loggedMap < 0
                ? $"자리 잡음: 맵 {state.Map.Id} ({state.Where.X},{state.Where.Y}) · 내 serial {world.Serial}"
                : $"맵 이동 {_loggedMap} → {state.Map.Id} ({state.Where.X},{state.Where.Y})");
            _loggedMap = state.Map.Id;
        }

        CompanionTie? master = world.Master;
        Character? owner = master is null ? null : world.Others.FirstOrDefault(one => one.Serial == master.Serial);
        int? distance = owner is null ? null : Reckon.Steps(owner.Where, _tile);
        bool away = master is not null && (owner is null || distance > 12);

        if (away)
        {
            _awaySince ??= now;

            if (!_awayTold && now - _awaySince >= Away)
            {
                _awayTold = true;
                log?.Invoke(owner is null
                    ? $"주인 {master!.Name}(serial {master.Serial})이 {(now - _awaySince.Value).TotalSeconds:0}초째 안 보입니다 — 나는 맵 {state.Map.Id} ({_tile.X},{_tile.Y}), 보이는 사람 {world.Others.Count}명"
                    : $"주인 {master!.Name}이 {(now - _awaySince.Value).TotalSeconds:0}초째 {distance}칸 떨어져 있습니다 — 맵 {state.Map.Id}");
            }
        }
        else if (_awaySince is { } since)
        {
            if (_awayTold)
            {
                log?.Invoke($"주인이 다시 곁에 있습니다({(now - since).TotalSeconds:0}초 만).");
            }

            _awaySince = null;
            _awayTold = false;
        }

        if (now >= _nextSummary)
        {
            _nextSummary = now + Summary;
            Vitals? me = world.Vitals;
            string did = string.Join(" ", _done.Where(p => p.Value > 0).Select(p => $"{p.Key}{p.Value}"));
            log?.Invoke(
                $"요약: 주인 {(master is null ? "없음" : $"{master.Name}({master.Serial}) {(owner is null ? "안 보임" : $"{distance}칸")}")} · " +
                $"맵 {state.Map.Id} ({_tile.X},{_tile.Y}) · 체력 {me?.Health}/{me?.MaximumHealth} 마력 {me?.Mana}/{me?.MaximumMana} · " +
                $"마지막 패킷 {silent.TotalSeconds:0}초 전 · 한 일 [{did}] · 예외 {_errors}");
            _done.Clear();
        }

        return null;
    }

    /// <summary>한 번 보고 하나 한다. 무엇을 했는지 돌려준다(시험용).</summary>
    public async Task<CompanionStep?> Once(CancellationToken token)
    {
        if (world.State is not { } state || world.Serial == 0)
        {
            return null;
        }

        if (world.PositionReports != _reports || state.Map.Id != _map)
        {
            _reports = world.PositionReports;
            _map = state.Map.Id;
            _tile = state.Where;
        }

        CompanionTie? master = world.Master;

        if ((master?.Serial ?? 0) != _master)
        {
            _master = master?.Serial ?? 0;
            Say(master is null ? "주인이 없습니다 — 기다립니다." : $"주인 {master.Name} 님을 따릅니다(serial {master.Serial}).");
        }

        Character? owner = master is null ? null : world.Others.FirstOrDefault(one => one.Serial == master.Serial);
        uint ownerSerial = master?.Serial ?? 0;
        uint hitsOwner = ownerSerial == 0 ? 0 : world.StruckBy(ownerSerial, Fighting);
        TimeSpan now = _clock.Elapsed;

        foreach (Creature one in world.Creatures)
        {
            if (ownerSerial != 0 && world.StruckBy(one.Serial, Fighting) == ownerSerial)
            {
                _ownerHit[one.Serial] = now;
            }
        }

        foreach (uint gone in _ownerHit.Where(pair => now - pair.Value > Fighting).Select(pair => pair.Key).ToList())
        {
            _ownerHit.Remove(gone);
        }

        CompanionSight sight = new()
        {
            Me = world.Serial,
            Master = master?.Serial ?? 0,
            Standing = _tile,
            Vitals = world.Vitals,
            Comatose = Overhead.InComa(world.Ailments),
            OwnerAt = owner?.Where,
            HealthOf = world.Health,
            Spells = world.Spells,
            Pack = world.Pack,
            StatusesOf = serial => world.StatusesOf(serial)?.Select(one => one.Name).ToHashSet(),
            Foes =
            [
                .. world.Creatures.Where(one => one.Kind == CreatureKind.Hostile).Select(one =>
                {
                    var icons = world.AilmentsOf(one.Serial).Select(seen => seen.Icon).ToHashSet();
                    return new Foe(one.Serial, one.Where,
                        icons.Contains(CompanionSpells.CurseIcon), icons.Contains(CompanionSpells.SleepIcon),
                        OwnerHits: _ownerHit.ContainsKey(one.Serial),
                        HitsOwner: hitsOwner == one.Serial);
                }),
            ],
            Blocked = walls.For(state.Map.Id),
            Occupied =
            [
                .. world.Others.Where(one => one.Serial != world.Serial).Select(one => one.Where),
                .. world.Creatures.Where(one => one.Kind != CreatureKind.Passable).Select(one => one.Where),
            ],
            Now = _clock.Elapsed,
        };

        // 쓸 마법(저주·나르콜리·해제·버프·회복)과 따라가기 거리는 주인이 앱 봇 탭에서 고른다(0x5E 종류 1 꼬리).
        CompanionStep step = _brain.Next(sight, master is null
            ? settings
            : settings with
            {
                Magic = master.Magic, Priest = master.Priest, Heal = master.Heal, GroupHeal = master.GroupHeal,
                FollowFrom = master.Follow > 0 ? master.Follow : settings.FollowFrom,
                FollowTo = master.Follow > 0 ? Math.Max(1, master.Follow - 1) : settings.FollowTo,
                HealOwnerPercent = master.HealPercent > 0 ? master.HealPercent : settings.HealOwnerPercent,
            });

        switch (step.Act)
        {
            case CompanionAct.Cast:
                await world.UseSpellAsync(step.Slot, step.Target, token);
                Say(step.Why);
                break;

            case CompanionAct.WakeOwner:
                await world.WakeMasterAsync(token);
                Say(step.Why);
                break;

            case CompanionAct.Drink:
                await world.UseAsync(step.Slot, token);
                Say(step.Why);
                break;

            case CompanionAct.Walk:
                await world.WalkAsync(step.Toward, token);
                (int dx, int dy) = Facing.TileStep(step.Toward);
                _tile = new Tile(_tile.X + dx, _tile.Y + dy);
                break;

            case CompanionAct.Stop:
            case CompanionAct.Rest:
                Say(step.Why);
                break;
        }

        return step;
    }

    /// <summary>같은 말을 거듭하지 않는다.</summary>
    private void Say(string what)
    {
        if (what == _said)
        {
            return;
        }

        _said = what;
        log?.Invoke(what);
    }
}
