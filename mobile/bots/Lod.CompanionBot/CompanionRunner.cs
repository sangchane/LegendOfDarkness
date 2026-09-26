using System.Diagnostics;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

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

    private readonly CompanionBrain _brain = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Tile _tile;
    private int _reports = -1;
    private int _map = -1;
    private string _said = string.Empty;
    private uint _master;

    /// <summary>접속이 끊기거나 멈추라 할 때까지 돈다.</summary>
    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !world.IsDisposed && world.Broke is null)
        {
            await Once(token);
            await Task.Delay(Tick, token);
        }
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
            Say(master is null ? "주인이 없습니다 — 기다립니다." : $"주인 {master.Name} 님을 따릅니다.");
        }

        Character? owner = master is null ? null : world.Others.FirstOrDefault(one => one.Serial == master.Serial);

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
            Blocked = walls.For(state.Map.Id),
            Occupied =
            [
                .. world.Others.Where(one => one.Serial != world.Serial).Select(one => one.Where),
                .. world.Creatures.Where(one => one.Kind != CreatureKind.Passable).Select(one => one.Where),
            ],
            Now = _clock.Elapsed,
        };

        CompanionStep step = _brain.Next(sight, settings);

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
