using System.Diagnostics;
using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Plays the novice road the way a person would: a new character fights in 노비스평원 until level 21, which is where
/// 5.99 says the novice town is done ("노비스마을에서는 레벨 21까지 육성하게 될것이며" — 멜로린). What it is really
/// for is finding what stops a player — no monsters, no drops, experience too slow, deaths with no way back.
/// </summary>
/// <remarks>
/// Runs against a server that is already up, only when <c>LOD_LIVE</c> names its login port, and writes what happens
/// to <c>LOD_LIVE_LOG</c> as it goes so a long run can be watched. The name must be in the server's
/// <c>GameMasters</c> list — the first thing it does is ask to be put in the hunting ground.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class NovicePlay : IDisposable
{
    private const string Name = "nov";
    private const string Secret = "1234";

    /// <summary>Where the novice hunting ground is, and the map it lives on.</summary>
    private const string Plain = "노비스평원A";
    private const int PlainId = 20393;

    /// <summary>Where 5.99 says the novice town is done.</summary>
    private const int Graduation = 21;

    /// <summary>A step apart, so the server does not turn our walking down (WalkingSpeedLimitFactor).</summary>
    private const int Step = 300;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromHours(3));
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_new_character_fights_its_way_to_the_novice_graduation()
    {
        if (Environment.GetEnvironmentVariable("LOD_LIVE") is not { } port || !int.TryParse(port, out int loginPort))
        {
            return;
        }

        LoginFlow.TryCreateAccount(loginPort, Name, Secret);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, loginPort, Name, Secret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        Task pump = world.PumpAsync(_deadline.Token);
        _ = pump.ContinueWith(
            stopped => Say($"듣기가 멎었다 — {stopped.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);

        await Until(() => world.State is not null && world.Vitals is not null, "세계에 들어가지 못했습니다.");

        Vitals born = Mine(world);
        Say($"깨어났다 — {born.Level}레벨 · 체력 {born.MaximumHealth} · 다음 레벨까지 {born.ExperienceToGo}"
            + $" · 맵 {world.State?.Map.Id}");

        await ToTheHuntingGround(world);

        int kills = 0;
        int deaths = 0;
        int drinks = 0;
        int level = Mine(world).Level;
        long gold = Mine(world).Gold;

        while (level < Graduation && !_deadline.IsCancellationRequested)
        {
            long experience = Mine(world).Experience;

            // 죽으면 서버가 마을로 보낸다(뮤레칸 → 레벨 21 미만은 노비스마을). 거기서는 사냥할 것이 없다.
            // 운영자는 벽과 맵 밖도 그냥 지나간다(Sprite.Walk 의 ghost walk) — 맵을 벗어나면 그것도 되돌린다.
            if (world.State?.Map.Id != PlainId || OffTheMap(world))
            {
                deaths++;
                Say($"마을로 돌아와 있다({deaths}번째) — 다시 사냥터로 · {kills}마리 · {_clock.Elapsed:hh\\:mm\\:ss}");
                await ToTheHuntingGround(world);
            }

            await Drink(world, () => drinks++);
            await OneMonster(world);

            Vitals now = Mine(world);

            if (now.Experience > experience)
            {
                kills++;

                if (kills % 10 == 0)
                {
                    int insight = world.Creatures.Count(one => one.Kind == CreatureKind.Hostile);

                    Say($"  {kills}마리 · 경험치 {now.Experience} (+{now.Experience - experience})"
                        + $" · 금 {now.Gold} · 체력 {now.Health}/{now.MaximumHealth}"
                        + $" · 보이는 괴물 {insight} · {_clock.Elapsed:hh\\:mm\\:ss}");
                }
            }

            if (now.Level > level)
            {
                Say($"레벨 {level} → {now.Level} · 체력 {now.MaximumHealth} · 다음까지 {now.ExperienceToGo}"
                    + $" · {kills}마리 · {_clock.Elapsed:hh\\:mm\\:ss}");
                level = now.Level;
            }

        }

        Vitals grown = Mine(world);
        Say($"끝 — {grown.Level}레벨 · {kills}마리 · 죽음 {deaths}번 · 쿠룸 {drinks}번 · "
            + $"{_clock.Elapsed:hh\\:mm\\:ss} · 금 {grown.Gold - gold} 벌었다");

        foreach (InventoryItem carried in world.Pack)
        {
            Say($"  가방: {carried.Name} ×{carried.Stacks}");
        }

        Assert.True(grown.Level >= Graduation, $"{Graduation}레벨에 이르지 못했습니다 — {grown.Level} 레벨입니다.");
    }

    /// <summary>Asks to be put in the hunting ground. A game master may do that from anywhere.</summary>
    private async Task ToTheHuntingGround(WorldClient world)
    {
        if (world.State?.Map.Id == PlainId && !OffTheMap(world))
        {
            return;
        }

        await world.SayAsync($"/tp \"{Plain}\" 30 30", _deadline.Token);
        await Until(() => world.State?.Map.Id == PlainId, $"{Plain} 으로 가지 못했습니다.");
        Say($"사냥터로 갔다 — {world.State?.Where}");
    }

    /// <summary>
    /// Drinks 쿠룸 when hurt, the way a person would — which is also what says whether the drops keep a novice alive.
    /// </summary>
    private async Task Drink(WorldClient world, Action drank)
    {
        Vitals mine = Mine(world);

        if (mine.MaximumHealth <= 0 || mine.Health > mine.MaximumHealth * 0.4)
        {
            return;
        }

        if (world.Pack.FirstOrDefault(one => one.Name == "쿠룸") is not { } potion)
        {
            return;
        }

        await world.UseAsync(potion.Slot, _deadline.Token);
        await Task.Delay(Step, _deadline.Token);
        drank();
    }

    /// <summary>Walks to the nearest monster and hits it until the experience moves or it is gone.</summary>
    private async Task OneMonster(WorldClient world)
    {
        if (Nearest(world) is not { } prey)
        {
            await Roam(world);
            return;
        }

        // 같은 놈만 오래 붙들고 있으면 놓아 준다 — 한 번은 이것 때문에 20분을 한 자리에서 허공만 때렸다.
        _swings = prey.Serial == _engaged ? _swings + 1 : 0;
        _engaged = prey.Serial;

        if (_swings > Patience)
        {
            // 서버에 그놈이 실제로 있는지 묻는다 — 괴물을 누르면 서버가 이름을 말해 준다(CommonMonster.OnClick).
            // 대답이 없으면 클라이언트만 들고 있는 허깨비이고, 대답이 오면 있는데 맞지 않는 것이다.
            int heard = world.SaidCount;
            await world.ClickAsync(prey.Serial, _deadline.Token);
            await Task.Delay(Step * 2, _deadline.Token);

            Say($"안 죽는 괴물을 놓아 준다 — 그림 {prey.Sprite} · 남은 체력 {world.Health(prey.Serial)?.ToString() ?? "모름"}"
                + $" · {prey.Where} 내 자리 {world.State?.Where} · #{prey.Serial}"
                + $" · 눌러 본 대답 {(world.SaidCount > heard ? world.Said : "없음")}");
            _shunned.Add(prey.Serial);
            _swings = 0;
            await Roam(world);
            return;
        }

        Tile standing = world.State?.Where ?? new Tile(0, 0);
        int dx = prey.Where.X - standing.X, dy = prey.Where.Y - standing.Y;

        // 괴물이 내가 선 칸에 겹쳐 있으면 평타로는 영영 못 때린다 — 평타는 앞 칸만 훑는다(Sprite.GetInfront).
        // 가장 가까운 놈은 늘 그놈(거리 0)이라 비켜서지 않으면 그 자리에서 허공만 친다. 실제로 20분을 그랬다.
        if (dx == 0 && dy == 0)
        {
            await world.WalkAsync(_heading, _deadline.Token);
            await Task.Delay(Step, _deadline.Token);
            return;
        }

        Direction towards = Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? Direction.East : Direction.West)
            : (dy >= 0 ? Direction.South : Direction.North);

        if (Math.Abs(dx) + Math.Abs(dy) > 1)
        {
            await world.WalkAsync(towards, _deadline.Token);
            await Task.Delay(Step, _deadline.Token);
            return;
        }

        await world.TurnAsync(towards, _deadline.Token);
        await world.AttackAsync(_deadline.Token);
        await Task.Delay(Step, _deadline.Token);
    }

    /// <summary>
    /// Walks on looking for something to fight, but stays in the middle of the zone. A game master walks through walls
    /// and off the map (Sprite.Walk ghost walk), so nothing stops it — left to itself it walked into the town warp
    /// seven times in seven minutes.
    /// </summary>
    private async Task Roam(WorldClient world)
    {
        Tile here = world.State?.Where ?? new Tile(Middle, Middle);

        // 가장자리로 나가면 가운데로 돌아선다. 마을로 가는 워프가 가장자리에 있다.
        if (!InTheBox(here))
        {
            _heading = Math.Abs(here.X - Middle) >= Math.Abs(here.Y - Middle)
                ? (here.X > Middle ? Direction.West : Direction.East)
                : (here.Y > Middle ? Direction.North : Direction.South);
        }
        else if (Random.Shared.Next(6) == 0)
        {
            _heading = (Direction)Random.Shared.Next(4);
        }

        await world.WalkAsync(_heading, _deadline.Token);
        await Task.Delay(Step, _deadline.Token);
    }

    /// <summary>The box the hunting stays in — the middle of the zone, away from the warps on its edges.</summary>
    private const int Middle = 30;
    private const int Reach = 12;
    private const int Inside = Middle - Reach;

    private Direction _heading = Direction.South;

    /// <summary>How many swings at one monster before we decide it will not die (300ms each).</summary>
    private const int Patience = 60;

    private uint _engaged;
    private int _swings;
    private readonly HashSet<uint> _shunned = [];

    /// <summary>Whether we have walked off the map — a game master may, and then there is nothing out there.</summary>
    private static bool OffTheMap(WorldClient world) =>
        world.State is { } standing
        && (standing.Where.X < 0 || standing.Where.Y < 0
            // 맵 크기를 서버가 말해 줄 때만 견준다 — 0 이면 아직 모르는 것이지 밖에 있는 것이 아니다.
            || (standing.Map.Columns > 0 && standing.Where.X >= standing.Map.Columns)
            || (standing.Map.Rows > 0 && standing.Where.Y >= standing.Map.Rows));

    /// <summary>Whether a tile is inside the hunting box. 사람도 마을 워프까지 쫓아가지는 않는다.</summary>
    private static bool InTheBox(Tile where) =>
        where.X >= Inside && where.X <= Middle + Reach && where.Y >= Inside && where.Y <= Middle + Reach;

    private Creature? Nearest(WorldClient world) =>
        world.Creatures
            // 상자 밖의 괴물은 쫓지 않는다 — 쫓아가면 가장자리의 마을 워프를 밟는다(30초마다 마을로 끌려갔다).
            .Where(one => one.Kind == CreatureKind.Hostile && InTheBox(one.Where) && !_shunned.Contains(one.Serial))
            .OrderBy(one => Math.Abs(one.Where.X - (world.State?.Where.X ?? 0))
                            + Math.Abs(one.Where.Y - (world.State?.Where.Y ?? 0)))
            .FirstOrDefault();

    private static Vitals Mine(WorldClient world) => world.Vitals ?? Vitals.Unknown;

    private void Say(string line)
    {
        if (Environment.GetEnvironmentVariable("LOD_LIVE_LOG") is { } path)
        {
            File.AppendAllText(path, $"[{_clock.Elapsed:hh\\:mm\\:ss}] {line}{Environment.NewLine}");
        }
    }

    private async Task Until(Func<bool> wanted, string complaint)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (wanted())
            {
                return;
            }

            await Task.Delay(100, _deadline.Token);
        }

        throw new TimeoutException(complaint);
    }
}
