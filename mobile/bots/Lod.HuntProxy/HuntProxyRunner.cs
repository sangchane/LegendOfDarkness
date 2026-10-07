using System.Diagnostics;
using Lod.CompanionBot;
using Lod.Mobile.Core;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace Lod.HuntProxy;

/// <summary>
/// 대신 사냥 하나 — 앱의 자동 사냥(<c>WorldView.AutoHuntTick</c>)과 같은 판단(<see cref="AutoHunt" />·<see cref="HuntDriver" />)을
/// 접속 하나에 잇는다. 끝 시각 · 자동 사냥 멈춤(위험) · 혼수 · 끊김 중 먼저 오는 것에서 끝난다.
/// </summary>
/// <remarks>
/// 서버는 된 걸음을 걸은 이에게 알리지 않는다 — 동료 봇(<c>CompanionRunner</c>)처럼 제 칸을 스스로 옮기고 서버가 자리(0x04)를
/// 보내면 그것으로 바로잡는다. 한 걸음 뒤에는 앱처럼 <see cref="Tuning.StepSeconds" /> 를 쉰다(서버 걷기 제한).
/// </remarks>
public sealed class HuntProxyRunner(
    WorldClient world, MapWalls walls, MapGuide guide, ProxyOrders orders, DateTime until, Action<string>? log = null)
{
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan Summary = TimeSpan.FromMinutes(5);

    /// <summary>이만큼 아무것도 안 오면 접속이 죽은 것으로 본다.</summary>
    public static readonly TimeSpan Dead = TimeSpan.FromSeconds(90);

    private readonly AutoHunt _hunt = new();
    private readonly AutoPotion _potion = new();
    private readonly AutoLootGate _loot = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Tile _tile;
    private Direction _facing = Direction.South;
    private int _reports = -1;
    private int _map = -1;
    private TimeSpan _busyUntil;
    private TimeSpan _nextSummary = Summary;
    private readonly Dictionary<string, int> _done = [];

    /// <summary>끝난 까닭. 돌고 있으면 null.</summary>
    public string? Stopped { get; private set; }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !world.IsDisposed && world.Broke is null && Stopped is null)
        {
            try
            {
                await Once(DateTime.UtcNow, token);
            }
            catch (Exception failed) when (!token.IsCancellationRequested)
            {
                log?.Invoke($"판단 중 예외: {BotLogin.Describe(failed)}");
            }

            if (DateTime.UtcNow - world.LastHeard >= Dead)
            {
                Stopped ??= "서버 소식이 끊김";
            }

            await Task.Delay(Tick, token);
        }
    }

    /// <summary>사냥 중심을 옮긴다(<see cref="AutoHunt.Recenter" />).</summary>
    public void Recenter(Tile home) => _hunt.Recenter(home);

    /// <summary>한 번 보고 하나 한다. 무엇을 했는지 돌려준다(시험용).</summary>
    public async Task<HuntStep?> Once(DateTime utcNow, CancellationToken token)
    {
        if (utcNow >= until)
        {
            Stopped = "맡긴 시간이 끝남";
            return null;
        }

        if (world.State is not { } state || world.Serial == 0)
        {
            return null;
        }

        if (world.PositionReports != _reports || state.Map.Id != _map)
        {
            _reports = world.PositionReports;
            _tile = state.Where;

            if (state.Map.Id != _map)
            {
                // 처음, 또는 맵이 바뀌었다(죽어서·밀려서) — 앱이 켠 자리와 같은 맵이면 그 자리가 중심, 아니면 선 자리.
                _hunt.Start(state.Map.Id == orders.Map ? new Tile(orders.X, orders.Y) : _tile, state.Map.Id);
                log?.Invoke($"사냥 시작: 맵 {state.Map.Id} 중심 ({_hunt.Home.X},{_hunt.Home.Y}) 반경 {orders.Radius} · {until:HH:mm} UTC 까지");
                _map = state.Map.Id;
            }
        }

        TimeSpan now = _clock.Elapsed;
        Report(now, state.Map.Id);

        bool comatose = Overhead.InComa(world.Ailments);
        if (comatose)
        {
            Stopped = "혼수(죽음)";
            return null;
        }

        if (now < _busyUntil || world.Vitals is not { } vitals)
        {
            return null;
        }

        // 앱이 프레임마다 하는 것 — 레벨업 점수 · 물약 · 발밑 줍기. 각자 서버 답을 기다리며 스스로 거른다.
        if (StatPlan.Next(world.Path, vitals) is { } stat)
        {
            await world.RaiseAsync(stat, token);
        }

        if (_potion.Next(vitals, world.Pack, orders.Hp, orders.Mp, now) is { } slot)
        {
            await world.UseAsync(slot, token);
        }

        if (_loot.Next(orders.Loot, _tile, world.Creatures) is { } where)
        {
            await world.PickUpAsync(where, token);
        }

        List<LearnedSkill> skills = [.. world.Skills.Where(one => orders.Skills.Contains(one.Name))];
        List<LearnedSpell> spells = [.. world.Spells.Where(one => orders.Spells.Contains(one.Name))];
        List<LearnedSpell> enemy = [.. world.Spells.Where(one => orders.EnemySpells.Contains(one.Name))];
        Func<Tile, bool> blocked = walls.For(state.Map.Id);

        HuntSight sight = HuntDriver.Sight(world, _tile, _facing, state.Map.Id, comatose, skills, spells, enemy,
            orders.Hp, orders.Loot, tile => tile.X < 0 || tile.Y < 0 || blocked(tile),
            [.. guide.ExitsOn(state.Map.Id).SelectMany(exit => exit.Tiles)], now);

        HuntStep step = _hunt.Next(sight, orders.Hunt);
        // 기다림은 까닭별로 센다 — 멈춰 선 봇이 왜 서 있는지 요약에 보이게.
        string done = step.Act == HuntAct.Wait ? $"{step.Act}({step.Why})" : step.Act.ToString();
        _done[done] = _done.GetValueOrDefault(done) + 1;

        switch (step.Act)
        {
            case HuntAct.Stop:
                Stopped = $"자동 사냥이 멈춤 — {step.Why}";
                break;
            case HuntAct.Heal:
                await world.UseSpellAsync(step.Slot, 0, token);
                break;
            case HuntAct.Walk:
                await world.WalkAsync(step.Toward, token);
                (int dx, int dy) = Facing.TileStep(step.Toward);
                _tile = new Tile(_tile.X + dx, _tile.Y + dy);
                _facing = step.Toward;
                _busyUntil = now + TimeSpan.FromSeconds(Tuning.StepSeconds);
                break;
            case HuntAct.Face:
                await world.TurnAsync(step.Toward, token);
                _facing = step.Toward;
                break;
            case HuntAct.Strike:
                await world.AttackAsync(token);
                break;
            case HuntAct.Skill:
                await world.UseSkillAsync(step.Slot, token);
                break;
            case HuntAct.Cast:
                await world.UseSpellAsync(step.Slot, step.Target, token);
                break;
        }

        return step;
    }

    private void Report(TimeSpan now, int map)
    {
        if (now < _nextSummary)
        {
            return;
        }

        _nextSummary = now + Summary;
        Vitals? me = world.Vitals;
        log?.Invoke($"요약: 맵 {map} ({_tile.X},{_tile.Y}) · 레벨 {me?.Level} · 체력 {me?.Health}/{me?.MaximumHealth} 마력 {me?.Mana}/{me?.MaximumMana} · " +
                    $"한 일 [{string.Join(" ", _done.Where(pair => pair.Value > 0).Select(pair => $"{pair.Key}{pair.Value}"))}]");
        _done.Clear();
    }
}
