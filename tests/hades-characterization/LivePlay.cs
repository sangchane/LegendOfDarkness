using System.Diagnostics;
using System.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Plays a Monk from the first level to the tenth on a server someone is watching, rather than on a
/// throwaway one that disappears when the run ends.
/// </summary>
/// <remarks>
/// <para>
/// This is a driver, not a characterization test. It asserts almost nothing: what it is for is that the
/// character it grows is still there afterwards, so the iPad can log in and stand in front of it. Every
/// other test here builds its own server, plays inside it and throws it away, which proves the rules but
/// leaves nothing anyone can look at.
/// </para>
/// <para>
/// <b>It fights for the levels.</b> <c>MonkLevelTenSkillTests</c> writes <c>ExpLevel = 10</c> into the
/// saved character because what it is checking is which skills a tenth-level Monk may hold, and how the
/// level arrived does not bear on that. Here it does: the point is the climb.
/// </para>
/// <para>
/// It is skipped unless <c>LOD_LIVE</c> names the login port, because it reaches a server outside its own
/// run — the one thing the rest of this suite is built never to do. Nothing starts or stops that server
/// either; it has to already be up.
/// </para>
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class LivePlay : IDisposable
{
    /// <summary>The Monk this grows. Short and typeable — a person enters it on a tablet.</summary>
    private const string Monk = "monk";

    /// <summary>The second character, so someone on the tablet can stand in the zone and watch.</summary>
    private const string Watcher = "watch";

    private const string Secret = "1234";

    /// <summary>Where the climb stops.</summary>
    private const int Tenth = 10;

    /// <summary>
    /// 무도가가 99레벨에 이르는 분배. 하데스의 시작값 <c>10/5/5/5/5</c> 에서 196점이 든다.
    /// <c>WoodlandProgressionTests</c> 가 근거를 들고 있다.
    /// </summary>
    private static readonly (Stat Which, int To)[] MonkBuild =
    [
        (Stat.Con, 65),
        (Stat.Str, 77),
        (Stat.Int, 43),
        (Stat.Wis, 36),
    ];

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(55));
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>Which way it walks while nothing is in sight.</summary>
    private Direction _heading = Direction.North;

    private int _turns;

    /// <summary>쫓는 중에 자리가 이만큼 그대로면 그 괴물은 갈 수 없는 곳에 있다고 본다.</summary>
    private const int StuckSteps = 6;

    /// <summary>쫓기 시작한 자리. 서버가 말한 칸이다.</summary>
    private Tile _chasedFrom;

    private int _stuck;

    /// <summary>
    /// 한 걸음 사이. <c>WalkingSpeedLimitFactor</c> 가 275 라 그보다 빨리 보내면 서버가 걸음을 물리고
    /// 제자리로 되돌린다 — 보는 쪽에서는 순간이동으로 보인다.
    /// </summary>
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(300);

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_monk_fights_its_way_from_the_first_level_to_the_tenth()
    {
        if (Environment.GetEnvironmentVariable("LOD_LIVE") is not { } port
            || !int.TryParse(port, out int loginPort))
        {
            return;
        }

        LoginFlow.TryCreateAccount(loginPort, Watcher, Secret);
        LoginFlow.TryCreateAccount(loginPort, Monk, Secret);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, loginPort, Monk, Secret, progress: null, _deadline.Token);

        WorldClient world = new(session);

        // 펌프를 버리고 두면 안 된다. 이것이 멎으면 우리는 읽기를 그만두고, 서버가 우리에게 보낼 것이
        // 쌓이다가 끝내 **우리 쪽 보내기까지 막힌다** — 캐릭터는 그 자리에 굳고 프로세스는 멀쩡히 살아
        // 있다. 그 모양으로 스무 분을 보내고서야 알았다. 무엇 때문에 멎었는지는 여기서만 알 수 있다.
        Task pump = world.PumpAsync(_deadline.Token);
        _ = pump.ContinueWith(
            stopped => Say($"펌프가 멎었다 — {stopped.Exception?.GetBaseException()}"),
            TaskContinuationOptions.OnlyOnFaulted);

        await Until(() => world.State is not null, "세계에 들어가지 못했습니다.");
        await Until(() => world.Vitals is not null, "서버가 내 수치를 말하지 않았습니다.");

        await BecomeAMonk(world);

        Vitals born = Mine(world);
        Say($"깨어났다 — {born.Level}레벨 · 체력 {born.MaximumHealth} · 마력 {born.MaximumMana} · " +
            $"힘 {born.Str} 지 {born.Int} 지혜 {born.Wis} 건 {born.Con} 민 {born.Dex} · " +
            $"다음 레벨까지 {born.ExperienceToGo}");

        int level = born.Level;
        int kills = 0;

        while (level < Tenth)
        {
            int before = level;
            long experience = Mine(world).Experience;

            await OneMonster(world);

            Vitals now = Mine(world);

            if (now.Experience > experience)
            {
                kills++;
                Say($"  잡았다 #{kills} — 경험치 +{now.Experience - experience} " +
                    $"(모두 {now.Experience}) · 금 {now.Gold} · 체력 {now.Health}/{now.MaximumHealth}");
            }

            level = now.Level;

            if (level <= before)
            {
                continue;
            }

            Say($"레벨 {before} → {level} · 체력 {now.MaximumHealth} · 마력 {now.MaximumMana} · " +
                $"쓸 점수 {now.Unspent} · 다음까지 {now.ExperienceToGo}");

            Vitals spent = await SpendTowardTheBuild(world);

            Say($"  점수를 썼다 — 힘 {spent.Str} 지 {spent.Int} 지혜 {spent.Wis} 건 {spent.Con} " +
                $"민 {spent.Dex} (남은 점수 {spent.Unspent})");
        }

        Vitals grown = Mine(world);

        Say($"{Tenth}레벨 도달 — {kills}마리 · {_clock.Elapsed:mm\\:ss} · " +
            $"체력 {grown.MaximumHealth} · 마력 {grown.MaximumMana} · 금 {grown.Gold} · " +
            $"힘 {grown.Str} 지 {grown.Int} 지혜 {grown.Wis} 건 {grown.Con} 민 {grown.Dex}");

        // 서버는 SaveRate 마다 저장한다. 끊기 전에 그 한 번을 기다려야 아이패드가 10레벨을 본다.
        await Task.Delay(TimeSpan.FromSeconds(15), _deadline.Token);

        Assert.True(grown.Level >= Tenth, $"{Tenth}레벨에 이르지 못했습니다 — {grown.Level} 레벨입니다.");
    }

    /// <summary>
    /// Walks to the nearest hostile and hits it until it is gone or the experience moves. Coming back with
    /// neither is a normal outcome — the zone stands its monsters up on its own clock, so an empty screen
    /// means waiting, not failing.
    /// </summary>
    /// <remarks>
    /// <b>Standing still does not work here.</b> 우드랜드1-1 은 60×60 — 3,600칸이고, 정의 다섯이 각각
    /// 열 마리까지 그 안 아무 데나 선다. 사람이 보는 것은 열두 칸 안이라, 입구에 서서 기다리면 한 마리가
    /// 지나갈 때까지 몇 분이고 아무 일도 일어나지 않는다. 그래서 보이지 않을 때는 걸어서 찾는다.
    /// </remarks>
    private async Task OneMonster(WorldClient world)
    {
        long experience = Mine(world).Experience;

        for (int swings = 0; swings < 400; swings++)
        {
            if (_deadline.IsCancellationRequested || Mine(world).Experience > experience)
            {
                return;
            }

            if (Nearest(world) is not { } goal || world.State is not { } before)
            {
                _stuck = 0;
                await Roam(world);
                continue;
            }

            // 벽 너머의 괴물은 보이기는 해도 갈 수 없다. 가장 가까운 한 마리만 보고 걸으면 그 벽에 대고
            // 영원히 걷는다 — 이 운전대는 (14,49) 에서 그렇게 굳어 열 몇 분을 아무것도 못 했다.
            _stuck = before.Where == _chasedFrom ? _stuck + 1 : 0;
            _chasedFrom = before.Where;

            if (_stuck > StuckSteps)
            {
                _stuck = 0;
                _heading = (Direction)((((int)_heading) + 1) % 4);

                // Roam 을 부르면 안 된다 — 그쪽은 보이는 괴물이 하나라도 있으면 한 걸음도 걷지 않는데,
                // 여기 온 이유가 바로 "보이는데 못 간다" 이다. 둘이 물려 제자리에서 아홉 분을 보냈다.
                // 그래서 방향만 틀고 여기서 곧장 한 걸음 뗀다.
                if (!LeavesTheZone(before.Where, _heading))
                {
                    await world.WalkAsync(_heading, _deadline.Token);
                    await Task.Delay(Step, _deadline.Token);
                }

                continue;
            }

            int dx = goal.X - before.Where.X, dy = goal.Y - before.Where.Y;
            Direction step = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            if (LeavesTheZone(before.Where, step))
            {
                _stuck = 0;
                await Roam(world);
                continue;
            }

            await world.WalkAsync(step, _deadline.Token);
            await Task.Delay(Step, _deadline.Token);

            if (world.State is not { } after || !IsThere(world, Ahead(after.Where, step)))
            {
                continue;
            }

            await world.AttackAsync(_deadline.Token);
            await Task.Delay(600, _deadline.Token);
        }
    }

    /// <summary>
    /// Takes a few steps in one direction to look for something to fight, changing direction when the way
    /// is blocked. The zone's edges are walls, so a fixed heading walks into one and stops; what keeps it
    /// moving is noticing that the last step did not land and turning.
    /// </summary>
    /// <summary>
    /// Whether stepping that way from here would walk off the zone. 우드랜드1-1 의 출구는 <c>x=0</c> 의
    /// 세 칸(34·35·36)뿐이고, 밟는 순간 우드랜드입구로 넘어간다.
    /// </summary>
    /// <remarks>
    /// 넘어가면 그 맵에는 이 존의 괴물이 없어서 성장이 그대로 멈추는데, 서버도 클라이언트도 아무 말을
    /// 하지 않는다 — 걷고 있으니 멀쩡해 보인다. 실제로 이 운전대가 두 번 그렇게 빠져나가 그 뒤 몇 분을
    /// 빈 맵에서 돌았다. 한 칸 앞을 보고 그 열을 밟지 않는 것이 가장 작은 손이다.
    /// </remarks>
    private static bool LeavesTheZone(Tile from, Direction step) =>
        step == Direction.West && from.X <= 1;

    private async Task Roam(WorldClient world)
    {
        for (int step = 0; step < 8 && Nearest(world) is null; step++)
        {
            Tile? was = world.State?.Where;

            if (was is { } here && LeavesTheZone(here, _heading))
            {
                _heading = (Direction)((((int)_heading) + 1) % 4);
                continue;
            }

            await world.WalkAsync(_heading, _deadline.Token);
            await Task.Delay(Step, _deadline.Token);

            if (world.State?.Where is { } now && was is { } then && now == then)
            {
                _heading = (Direction)((((int)_heading) + 1) % 4);
            }
        }

        // 한 바퀴에 한 번만 F5 를 친다. 걸음마다 치면 서버가 모든 것을 다시 내려보내고, 보는 쪽에서는
        // 사람이 걷는 것이 아니라 한 칸씩 튀는 것으로 보인다.
        await world.RefreshAsync(_deadline.Token);
        await Task.Delay(Step, _deadline.Token);

        _turns++;

        // 같은 곳을 맴돌지 않게 가끔 방향을 튼다. 벽에 막히는 것만으로는 한 줄기 길을 오갈 뿐이다.
        if (_turns % 5 == 0)
        {
            _heading = (Direction)((((int)_heading) + 1) % 4);
        }
    }

    /// <summary>
    /// Walks to the class chooser standing at the zone's entrance and takes the Monk's path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A character wakes a <c>Peasant</c> and stays one until somebody asks. Nothing else here depends on
    /// the answer — experience, health and points all arrive the same way — but the skills do: every Monk
    /// skill's template names <c>Class_Required</c>, so a peasant is offered none of them and a run that
    /// wants to see one swing is looking at a character that cannot hold it.
    /// </para>
    /// <para>
    /// The conversation is two questions: the NPC asks whether we are ready (choice 1 of two), then which
    /// path (choice 5 of five). Both are answered by number, which is what <c>0x39</c> carries.
    /// </para>
    /// </remarks>
    private async Task BecomeAMonk(WorldClient world)
    {
        // 돌아다니며 찾지 않는다. 60×60 에 고정된 한 사람을 우연히 마주치기를 기다리는 것이라 몇 분이
        // 걸리고, 그 사이 한 마리도 잡지 못한다. 서 있는 자리를 아는 이상 곧장 걸어가는 쪽이 짧다.
        for (int step = 0; step < 200 && Chooser(world) is null; step++)
        {
            if (world.State is not { } standing || Beside(standing.Where, TeacherTile))
            {
                break;
            }

            int toX = TeacherTile.X - standing.Where.X, toY = TeacherTile.Y - standing.Where.Y;
            Direction towards = Math.Abs(toX) >= Math.Abs(toY)
                ? (toX >= 0 ? Direction.East : Direction.West)
                : (toY >= 0 ? Direction.South : Direction.North);

            // 벽에 막히면 한 번 비껴 간다. 이 존은 3,600칸에 벽이 192칸뿐이라 이것으로 충분히 닿는다.
            if (LeavesTheZone(standing.Where, towards))
            {
                break;
            }

            await world.WalkAsync(towards, _deadline.Token);
            await Task.Delay(Step, _deadline.Token);

            if (world.State?.Where == standing.Where)
            {
                await world.WalkAsync(Math.Abs(toX) >= Math.Abs(toY)
                    ? (toY >= 0 ? Direction.South : Direction.North)
                    : (toX >= 0 ? Direction.East : Direction.West), _deadline.Token);
                await Task.Delay(Step, _deadline.Token);
            }
        }

        for (int tries = 0; tries < 20; tries++)
        {
            if (Chooser(world) is { } teacher)
            {
                await world.ClickAsync(teacher, _deadline.Token);
                await Task.Delay(800, _deadline.Token);
                Say($"  눌렀다 — {world.Talking?.What ?? "대답 없음"}");

                // 이미 고른 사람에게 다시 물으면 안 된다. 스크립트는 길을 고른 자리에서 레벨을 1 로
                // 되돌리고 점수 196 을 다시 주므로(ClassChooser.cs:76-77), 한 번 더 물으면 성장 기록이
                // 통째로 어그러진다. 그것을 가르는 것은 사범이 하는 말뿐이다 — 직업은 서버가 우리에게
                // 따로 말해 주지 않는다.
                if (world.Talking?.What.Contains(AlreadyChosen, StringComparison.Ordinal) == true)
                {
                    Say("  이미 무도가다 — 다시 묻지 않는다.");
                    return;
                }

                await world.AnswerAsync(teacher, Ready, _deadline.Token);
                await Task.Delay(800, _deadline.Token);
                Say($"  준비됐다고 답했다({Ready}) — {world.Talking?.What ?? "대답 없음"}");

                await world.AnswerAsync(teacher, MonksPath, _deadline.Token);
                await Task.Delay(1200, _deadline.Token);

                Say($"전직을 물었다 — 사범 {teacher} · {world.Talking?.What ?? "대답 없음"}");
                return;
            }

            await Roam(world);
        }

        Say("전직사범을 찾지 못했다 — 무도가가 아니라 주민인 채로 사냥한다.");
    }

    /// <summary>The class chooser's serial, if one is in sight.</summary>
    private static uint? Chooser(WorldClient world) =>
        world.Creatures
            .Where(one => one.Kind == CreatureKind.Merchant && one.Name.Contains(ChooserName, StringComparison.Ordinal))
            .Select(one => (uint?)one.Serial)
            .FirstOrDefault();

    /// <summary>What the NPC standing at the entrance is called.</summary>
    private const string ChooserName = "전직사범";

    /// <summary><c>ClassChooser.OnClick</c> 이 이미 길을 고른 사람에게 하는 말.</summary>
    private const string AlreadyChosen = "already chosen";

    /// <summary>전직사범이 서 있는 칸. 템플릿에 적어 세운 자리다.</summary>
    private static readonly Tile TeacherTile = new(3, 35);

    /// <summary>한 칸 안에 들어왔나. 상인은 밟고 설 수 없으므로 옆에 서는 것이 도착이다.</summary>
    private static bool Beside(Tile here, Tile there) =>
        Math.Abs(here.X - there.X) + Math.Abs(here.Y - there.Y) <= 1;

    /// <summary>
    /// "I'm ready to choose a Path," — <c>ClassChooser.OnClick</c> numbers that line <c>0x06</c>, and what
    /// goes back is the number beside the line, not its place in the list.
    /// </summary>
    private const ushort Ready = 6;

    /// <summary>Warrior · Rogue · Wizard · Priest · <b>Monk</b> — the fifth.</summary>
    private const ushort MonksPath = 5;

    /// <summary>Spends every point in hand on whichever attribute is furthest from the Monk build.</summary>
    private async Task<Vitals> SpendTowardTheBuild(WorldClient world)
    {
        for (int guard = 0; guard < 64; guard++)
        {
            Vitals now = Mine(world);

            if (now.Unspent <= 0 || NextToRaise(now) is not { } which)
            {
                return now;
            }

            await world.RaiseAsync(which, _deadline.Token);
            await Task.Delay(150, _deadline.Token);
        }

        return Mine(world);
    }

    private static Stat? NextToRaise(Vitals now)
    {
        (Stat Which, int Standing)[] standing =
        [
            (Stat.Str, now.Str), (Stat.Int, now.Int), (Stat.Wis, now.Wis),
            (Stat.Con, now.Con), (Stat.Dex, now.Dex)
        ];

        return MonkBuild
            .Join(standing, want => want.Which, has => has.Which,
                (want, has) => (want.Which, Short: want.To - has.Standing))
            .Where(gap => gap.Short > 0)
            .OrderByDescending(gap => gap.Short)
            .Select(gap => (Stat?)gap.Which)
            .FirstOrDefault();
    }

    private static Tile Ahead(Tile from, Direction facing) => facing switch
    {
        Direction.North => new Tile(from.X, from.Y - 1),
        Direction.South => new Tile(from.X, from.Y + 1),
        Direction.East => new Tile(from.X + 1, from.Y),
        _ => new Tile(from.X - 1, from.Y)
    };

    private static bool IsThere(WorldClient world, Tile tile) =>
        world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == tile);

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

    private static Vitals Mine(WorldClient world) =>
        world.Vitals ?? throw new InvalidOperationException("서버가 내 수치를 말하지 않았습니다 (0x08 을 못 읽었습니다).");

    /// <summary>
    /// Writes the climb where it can be watched while it happens. xUnit holds a test's output until the
    /// test ends, and this one runs for the better part of an hour.
    /// </summary>
    private void Say(string line)
    {
        if (Environment.GetEnvironmentVariable("LOD_LIVE_LOG") is { } path)
        {
            File.AppendAllText(path, $"[{_clock.Elapsed:mm\\:ss}] {line}{Environment.NewLine}");
        }
    }

    private async Task Until(Func<bool> wanted, string complaint)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);

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
