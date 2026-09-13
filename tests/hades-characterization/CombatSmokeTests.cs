using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// A spell that loads is not a spell that does anything. The templates bound their scripts through the
/// wrong field for a while — SpellTemplate reads <c>ScriptKey</c>, not <c>ScriptName</c> — and nothing
/// said so: the count was right, the server logged no error, and casting simply did nothing. The only
/// way to know is to point one at a monster and watch its health.
/// </summary>
public sealed class CombatSmokeTests : IDisposable
{
    /// <summary>신죽마집안5-2 — 20x20 with twenty monsters in it, so one is never far.</summary>
    private const int MonsterRoom = 20670;

    private const string Name = "smokefight";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_monster_is_standing_there_and_has_health()
    {
        WorldClient world = await Enter();
        Creature mob = await AnyMonster(world);

        // The server only reports health once something happens to it, so ask by attacking the air first
        // is not enough — this checks the spawn itself, which is what the monster templates promise.
        Assert.NotEqual(0u, mob.Serial);
        Assert.Equal(CreatureKind.Hostile, mob.Kind);
    }

    [Fact]
    public async Task Hitting_a_monster_takes_its_health_down()
    {
        WorldClient world = await Enter();
        await AnyMonster(world);

        // The server drops anything sent while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        // Assail comes with every fresh character (GiveAssailOnCreate) and is one of the twelve skills
        // that carry a script. It hits whatever stands in front, and monsters wander — so turn as well
        // as swing. Twenty of them share a twenty-tile room, so one walks into reach soon enough.
        (uint Serial, int Left)? hurt = null;

        for (int swing = 0; swing < 150 && hurt is null; swing++)
        {
            // 가만히 서서 돌기만 하면 괴물이 다가와 주기를 기다리는 셈이라 될 때도 있고 안 될
            // 때도 있다. 가까운 놈 쪽으로 한 발씩 간다 — 걸음은 방향도 바꾸므로 앞칸을 치는
            // Assail 이 그놈을 향하게 된다.
            if (Nearest(world) is { } goal && world.State is { } me)
            {
                int dx = goal.X - me.Where.X, dy = goal.Y - me.Where.Y;
                Direction step = Math.Abs(dx) >= Math.Abs(dy)
                    ? (dx >= 0 ? Direction.East : Direction.West)
                    : (dy >= 0 ? Direction.South : Direction.North);

                await world.WalkAsync(step, _deadline.Token);
                await world.RefreshAsync(_deadline.Token);
            }

            await world.AttackAsync(_deadline.Token);

            // GlobalBaseSkillDelay 는 500ms 다. 그보다 빨리 휘두르면 서버가 그냥 버린다
            // (AssailIsReady). 빨리 치는 것이 아니라 제때 치는 것이 필요하다.
            await Task.Delay(600, _deadline.Token);

            foreach (Creature mob in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
            {
                // 체력은 가득 찬 것에 대한 백분율이고, 서버는 **변할 때만** 알려준다. 그래서
                // 처음 받은 값이 이미 100 아래면 그 사이에 피해가 들어갔다는 뜻이다 — 첫 값
                // 뒤로 더 줄기를 기다리면 이미 일어난 일을 놓친다.
                if (world.Health(mob.Serial) is { } left and < 100)
                {
                    hurt = (mob.Serial, left);
                    break;
                }
            }
        }

        Assert.True(hurt is not null,
            "백쉰 번 휘둘렀는데 어느 괴물도 체력이 깎이지 않았습니다 — 때린 것이 닿지 않았거나 " +
            "스크립트가 안 돌았습니다.");

        // 내 체력도 준다면 괴물이 되받아치고 있다는 뜻이고, 그것까지 돌아야 전투다.
        Assert.True(world.Health(world.Serial) is null or <= 100, "내 체력 보고가 이상합니다.");
    }

    private async Task<WorldClient> Enter()
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, 10, 10));
        _servers.Add(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State, "세계에 들어가지 못했습니다");
        return world;
    }

    private readonly List<IsolatedHadesServer> _servers = [];

    /// <summary>The tile of the closest monster, so the swing has something to reach.</summary>
    private static Tile? Nearest(WorldClient world)
    {
        if (world.State is not { } me)
        {
            return null;
        }

        return world.Creatures
            .Where(c => c.Kind == CreatureKind.Hostile)
            .OrderBy(c => Math.Abs(c.Where.X - me.Where.X) + Math.Abs(c.Where.Y - me.Where.Y))
            .Select(c => (Tile?)c.Where)
            .FirstOrDefault();
    }

    private async Task<Creature> AnyMonster(WorldClient world) =>
        await Until(() => world.Creatures.FirstOrDefault(c => c.Kind == CreatureKind.Hostile),
            "맵에 괴물이 한 마리도 없습니다");

    private async Task<T> Until<T>(Func<T?> wanted, string complaint) where T : class
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(40);

        while (DateTime.UtcNow < giveUp)
        {
            if (wanted() is { } got)
            {
                return got;
            }

            await Task.Delay(100, _deadline.Token);
        }

        throw new TimeoutException(complaint);
    }
}
