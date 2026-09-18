using System.Diagnostics;
using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Chases down why a monster sometimes cannot be killed: the 노비스 play test kept standing next to one monster
/// swinging forever while everything around it died normally (2026-09-18).
/// </summary>
/// <remarks>
/// What is known before this test: the thing has a real monster drawing (16641, 팜팻), it walks about, and no
/// health bar (0x13) ever arrives for it — so our blows land on an empty tile. That leaves two possibilities, and
/// this test tells them apart by asking the server to show the map again (0x38, which clears the server's own
/// record of what we have been shown — <c>GameClient.RefreshMap</c>):
/// <list type="bullet">
///   <item>the tile moves → the server had it elsewhere all along and our copy was stale;</item>
///   <item>the tile stays and the server never mentions it again → we are holding something already gone.</item>
/// </list>
/// Runs only when <c>LOD_LIVE</c> names a live server's login port, and writes to <c>LOD_LIVE_LOG</c>.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class UnkillableMonster : IDisposable
{
    private const string Name = "nov";
    private const string Secret = "1234";
    private const string Plain = "노비스평원A";
    private const int PlainId = 20393;

    /// <summary>A step apart, so the server does not turn our walking down (WalkingSpeedLimitFactor).</summary>
    private const int Step = 300;

    /// <summary>How many swings at one monster before we call it unkillable.</summary>
    private const int Patience = 40;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(30));
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// Warps used to put a character down on its named tile whatever was already standing there, and a monster
    /// underfoot cannot be fought at all — the basic attack only reaches the tile in front. The server now looks
    /// for a free tile first (<c>Area.FreeSpotNear</c>); this lands twenty times in the busiest part of the
    /// novice plain and says nobody is ever underneath us.
    /// </summary>
    [Fact]
    public async Task Warping_never_puts_us_on_top_of_a_monster()
    {
        if (Environment.GetEnvironmentVariable("LOD_LIVE") is not { } port || !int.TryParse(port, out int loginPort))
        {
            return;
        }

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, loginPort, Name, Secret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is not null, "세계에 들어가지 못했습니다.");

        int landings = 0;
        int overlapped = 0;

        for (int fall = 0; fall < 20; fall++)
        {
            await world.SayAsync($"/tp \"{Plain}\" 30 30", _deadline.Token);
            await Task.Delay(900, _deadline.Token);

            if (world.State is not { } standing || standing.Map.Id != PlainId)
            {
                continue;
            }

            landings++;

            if (world.Creatures.Any(one => one.Kind == CreatureKind.Hostile && one.Where == standing.Where))
            {
                overlapped++;
                Say($"  {fall + 1}번째: {standing.Where} 에 괴물과 겹쳤다.");
            }

            // 다음 번에 같은 자리가 비어 있지 않도록 한 칸 물러났다 온다.
            await world.WalkAsync(Direction.South, _deadline.Token);
            await Task.Delay(Step, _deadline.Token);
        }

        Say($"워프로 {landings}번 내려앉아 괴물과 겹친 것 {overlapped}번.");

        Assert.True(landings > 0, "한 번도 사냥터에 내려앉지 못했습니다.");
        Assert.Equal(0, overlapped);
    }

    [Fact]
    public async Task A_monster_that_will_not_die_is_one_we_are_swinging_at_the_wrong_tile()
    {
        if (Environment.GetEnvironmentVariable("LOD_LIVE") is not { } port || !int.TryParse(port, out int loginPort))
        {
            return;
        }

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, loginPort, Name, Secret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is not null, "세계에 들어가지 못했습니다.");

        await world.SayAsync($"/tp \"{Plain}\" 30 30", _deadline.Token);
        await Until(() => world.State?.Map.Id == PlainId, $"{Plain} 으로 가지 못했습니다.");

        Say($"사냥터에서 한 놈을 붙잡고 한 대씩 적어 본다 — {world.State?.Where}");

        // 붙어 선 채 한 대씩 때리며, 그때마다 내 자리·괴물 자리·서버가 돌려준 것을 함께 적는다.
        // 지난 시험들은 처음 찍은 자리를 들고 비교해 스스로를 속였다 — 이번에는 매번 다시 읽는다.
        for (int swing = 0; swing < 60 && !_deadline.IsCancellationRequested; swing++)
        {
            Tile mine = world.State?.Where ?? new Tile(0, 0);

            if (Nearest(world) is not { } prey)
            {
                await world.SayAsync($"/tp \"{Plain}\" 30 30", _deadline.Token);
                await Task.Delay(Step, _deadline.Token);
                continue;
            }

            int dx = prey.Where.X - mine.X, dy = prey.Where.Y - mine.Y;

            Direction towards = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            if (Math.Abs(dx) + Math.Abs(dy) > 1)
            {
                await world.WalkAsync(towards, _deadline.Token);
                await Task.Delay(Step, _deadline.Token);
                continue;
            }

            int before = world.Hurts.Count;

            await world.TurnAsync(towards, _deadline.Token);
            await world.AttackAsync(_deadline.Token);
            await Task.Delay(600, _deadline.Token);

            string answer = string.Join(" ", world.Hurts.Skip(before).Select(hit => $"#{hit.Serial}:{hit.Left}"));

            Say($"  때림 {swing}: 나 {mine.X},{mine.Y} · 놈 {prey.Where.X},{prey.Where.Y}"
                + $" ({(dx == 0 && dy == 0 ? "같은 칸" : towards.ToString())}) · 서버 답 {(answer.Length == 0 ? "없음" : answer)}");

            if (world.Health(prey.Serial) is { } left)
            {
                Say($"  맞혔다 — #{prey.Serial} 체력 {left}. 여기까지 {swing + 1}번.");
                return;
            }
        }

        Say("예순 번을 때려도 한 번도 못 맞혔다.");
    }

    /// <summary>
    /// Measures how far the client's idea of where the monsters are drifts from the server's. Starts from a
    /// freshly shown map, waits, then asks the server to show it again and counts how far things moved. The
    /// waiting for that second showing lets the monsters take a step or two by themselves, so what matters is
    /// not the number itself but whether it grows with the waiting.
    /// </summary>
    private async Task Drift(WorldClient world, int seconds)
    {
        await world.RefreshAsync(_deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        Dictionary<uint, Tile> believed = world.Creatures
            .Where(one => one.Kind == CreatureKind.Hostile)
            .ToDictionary(one => one.Serial, one => one.Where);

        await Task.Delay(TimeSpan.FromSeconds(seconds), _deadline.Token);

        // 서 있는 동안 걸음 알림으로 따라간 값.
        Dictionary<uint, Tile> followed = world.Creatures
            .Where(one => one.Kind == CreatureKind.Hostile)
            .ToDictionary(one => one.Serial, one => one.Where);

        await world.RefreshAsync(_deadline.Token);
        await Task.Delay(400, _deadline.Token);

        var truth = world.Creatures.Where(one => one.Kind == CreatureKind.Hostile).ToList();
        var seen = truth.Where(one => followed.ContainsKey(one.Serial)).ToList();

        if (seen.Count == 0)
        {
            Say($"{seconds}초 — 견줄 괴물이 없다.");
            return;
        }

        double off = seen.Average(one =>
            Math.Abs(followed[one.Serial].X - one.Where.X) + Math.Abs(followed[one.Serial].Y - one.Where.Y));

        int wrong = seen.Count(one => followed[one.Serial] != one.Where);

        Say($"가만히 선 {seconds}초 — 괴물 {seen.Count}마리 중 자리가 틀린 것 {wrong}마리 · 평균 어긋남 {off:F2}칸"
            + $" (처음 본 것 {believed.Count}마리)");
    }

    /// <summary>Walks onto the tile a monster is standing on — which only a game master may do.</summary>
    private async Task<Creature> StandOnOne(WorldClient world)
    {
        for (int step = 0; step < 200; step++)
        {
            Tile standing = world.State?.Where ?? new Tile(0, 0);

            if (Nearest(world) is not { } prey)
            {
                // 빈 구석으로 걸어 나가면 괴물을 영영 못 만난다 — 사냥터 한가운데로 되돌아간다.
                await world.SayAsync($"/tp \"{Plain}\" 30 30", _deadline.Token);
                await Task.Delay(Step, _deadline.Token);
                continue;
            }

            int dx = prey.Where.X - standing.X, dy = prey.Where.Y - standing.Y;

            if (dx == 0 && dy == 0)
            {
                return prey;
            }

            await world.WalkAsync(
                Math.Abs(dx) >= Math.Abs(dy)
                    ? (dx >= 0 ? Direction.East : Direction.West)
                    : (dy >= 0 ? Direction.South : Direction.North),
                _deadline.Token);

            await Task.Delay(Step, _deadline.Token);
        }

        throw new TimeoutException("괴물 위에 올라서지 못했습니다.");
    }

    private static Creature? Nearest(WorldClient world) =>
        world.Creatures
            .Where(one => one.Kind == CreatureKind.Hostile)
            .OrderBy(one => Math.Abs(one.Where.X - (world.State?.Where.X ?? 0))
                            + Math.Abs(one.Where.Y - (world.State?.Where.Y ?? 0)))
            .FirstOrDefault();

    /// <summary>Asks the server to show the map again and reports whether the monster moves when it does.</summary>
    private async Task Examine(WorldClient world, Creature stuck)
    {
        // 새로 고치기 직전에 클라이언트가 믿는 자리 — 이것이 견줄 값이다.
        stuck = world.Creatures.FirstOrDefault(one => one.Serial == stuck.Serial) ?? stuck;

        Say($"안 죽는 괴물을 잡았다 — 그림 {stuck.Sprite} · #{stuck.Serial}"
            + $" · 내가 믿는 자리 {stuck.Where} · 내 자리 {world.State?.Where}");

        // 서버가 무엇을 돌려보냈나. 번호 0 은 헛친 것이고(Assail 이 앞칸에서 아무도 못 찾았다), 아무것도 없으면
        // 공격 자체가 서버에서 돌지 않은 것이다. 시스템 글월이 있으면 서버가 거절한 이유를 말해 준다.
        Say($"  받은 체력 알림 {world.Hurts.Count}개 — 마지막 열: "
            + string.Join(", ", world.Hurts.TakeLast(10).Select(hit => $"#{hit.Serial}:{hit.Left}")));
        Say($"  마지막 시스템 글월: {world.Said ?? "없음"} (모두 {world.SaidCount}개)");

        await world.RefreshAsync(_deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        Creature? after = world.Creatures.FirstOrDefault(one => one.Serial == stuck.Serial);

        Say(after is null
            ? "  화면을 새로 고치니 그 괴물이 없다 — 서버에는 이미 없는 것을 들고 있었다(사라짐 알림이 안 온 것)."
            : after.Where == stuck.Where
                ? $"  새로 고쳐도 같은 자리 {after.Where} — 서버에도 거기 있다. 자리 문제가 아니다."
                : $"  새로 고치니 자리가 {stuck.Where} → {after.Where} 로 바뀌었다"
                  + " — 우리가 들고 있던 자리가 낡은 것이었다(걸음 알림을 놓쳤다).");

        if (after is not null && after.Where == world.State?.Where)
        {
            // 같은 칸에 서 있다. 평타는 앞 칸만 때리니(Sprite.GetInfront) 이대로는 영원히 못 맞힌다.
            // 한 칸 비켜선 뒤 돌아서서 때려 본다 — 그때 체력이 오면 원인이 확정된다.
            Say("  괴물이 내 발밑 같은 칸에 서 있다 — 한 칸 비켜서서 때려 본다.");

            await world.WalkAsync(Direction.West, _deadline.Token);
            await Task.Delay(Step, _deadline.Token);
            await world.TurnAsync(Direction.East, _deadline.Token);

            for (int swing = 0; swing < 10 && world.Health(stuck.Serial) is null; swing++)
            {
                await world.AttackAsync(_deadline.Token);
                await Task.Delay(Step * 2, _deadline.Token);
            }

            Say(world.Health(stuck.Serial) is { } hurt
                ? $"  비켜서서 때리니 체력이 {hurt} 로 줄었다 — 같은 칸에 선 것이 원인이다."
                : "  비켜서서 때려도 체력이 안 온다 — 같은 칸 말고 다른 것이 막고 있다.");

            return;
        }

        if (after is null || after.Where == stuck.Where)
        {
            return;
        }

        // 제자리를 알았으니 다시 때려 본다 — 죽으면 원인이 확정된다.
        for (int swing = 0; swing < 20 && world.Health(stuck.Serial) is null; swing++)
        {
            if (world.Creatures.FirstOrDefault(one => one.Serial == stuck.Serial) is not { } now)
            {
                break;
            }

            Tile standing = world.State?.Where ?? new Tile(0, 0);
            int dx = now.Where.X - standing.X, dy = now.Where.Y - standing.Y;

            Direction towards = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            if (Math.Abs(dx) + Math.Abs(dy) > 1)
            {
                await world.WalkAsync(towards, _deadline.Token);
            }
            else
            {
                await world.TurnAsync(towards, _deadline.Token);
                await world.AttackAsync(_deadline.Token);
            }

            await Task.Delay(Step, _deadline.Token);
        }

        Say(world.Health(stuck.Serial) is { } left
            ? $"  제자리를 알고 때리니 체력이 {left} 로 줄었다 — 낡은 자리가 원인이다."
            : "  제자리를 알고 때려도 체력이 안 온다 — 자리 말고 다른 것이 막고 있다.");
    }

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
