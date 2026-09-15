using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// A character has to grow, or the only zone anything can be tested in is the first one.
/// </summary>
/// <remarks>
/// <para>
/// Everything else here fights at level one, where a character stands on the 150 health it woke with and
/// hits for what strength ten is worth. Woodland runs from novice to the level cap in zones — 115 health
/// and a one-to-seven blow at the entrance, 1,150 and fifty-to-a-hundred three zones up, 9,000 at the
/// goblins — so a character that never grows can only ever be tested against the first of them.
/// </para>
/// <para>
/// Growth has three separate parts and this proves each one rather than assuming it: a level arrives, it
/// hands out <c>StatsPerLevel</c> points, and spending a point raises the attribute it names. What makes
/// it worth anything is that two of those attributes feed the next level — maximum health grows by
/// <c>HpGainFactor × Con × 0.65</c> and maximum mana by <c>MpGainFactor × Wis × 0.45</c>, both read at the
/// moment the level lands — so points spent early are worth more than the same points spent late, and a
/// character that banks them grows slower than one that spends them.
/// </para>
/// <para>
/// Where the points go follows the build the original settled on for a Monk: <c>Str 64 · Con 65 · Int 43 ·
/// Wis 36 · Dex 3</c>. That build is also the proof of the numbers around it. The original started a
/// character at <c>3/3/3/3/3</c>, and climbing from there to that build costs <b>196</b> points —
/// exactly the 2 per level that ninety-eight levels hand out, to the point. Four things would each have
/// to be wrong in a way that cancelled for that to land on zero, so the starting spread, the two points a
/// level, and the build itself all stand together.
/// </para>
/// <para>
/// <b>Hades does not start a character where the original did.</b> <c>Aisling.cs</c> hands out
/// <c>Str 10 · Int 5 · Wis 5 · Con 5 · Dex 5</c>, thirteen points ahead of <c>3/3/3/3/3</c>. That start is
/// kept and the thirteen are spent on strength, which puts the build at <c>Str 77</c> and back to using
/// every one of the 196 points exactly. Nothing here depends on that choice: it reads whatever the server
/// hands out and checks that spending works, so it goes on holding whichever start is settled on.
/// </para>
/// </remarks>
public sealed class WoodlandProgressionTests : IDisposable
{
    private const string Name = "woodgrow";

    /// <summary>우드랜드1-1 — 새 캐릭터가 버티는 유일한 존.</summary>
    private const int WoodlandOneOne = 20015;

    private static readonly Tile Start = new(2, 35);

    private static readonly Tile TargetTile = new(2, 34);

    /// <summary><c>SpawnQualifer.Defined</c>.</summary>
    private const int SpawnDefined = 4;

    /// <summary>
    /// 무도가가 99레벨에 이르는 분배. 하데스의 시작값 <c>10/5/5/5/5</c> 에서 정확히 196점 —
    /// 98레벨 × 2점 — 이 든다. Dex 는 5 에서 움직이지 않으므로 여기 없다.
    /// </summary>
    /// <remarks>
    /// 원작의 분배는 <c>Str 64 · Con 65 · Int 43 · Wis 36 · Dex 3</c> 이고 원작의 시작값
    /// <c>3/3/3/3/3</c> 에서 그것도 정확히 196점이다. 하데스는 13점 앞선 자리에서 시작하므로 그 차이를
    /// 힘에 얹어 <c>Str 77</c> 로 둔다 — 시작값을 원작으로 되돌리는 대신 남는 점수를 쓰기로 한 결정이다.
    /// </remarks>
    private static readonly (Stat Which, int To)[] MonkBuild =
    [
        (Stat.Con, 65),
        (Stat.Str, 77),
        (Stat.Int, 43),
        (Stat.Wis, 36),
    ];

    private const int MostSwings = 400;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(9));
    private readonly List<IsolatedHadesServer> _servers = [];

    public void Dispose()
    {
        _deadline.Dispose();

        foreach (IsolatedHadesServer server in _servers)
        {
            server.Dispose();
        }
    }

    [Fact]
    public async Task Levels_hand_out_points_and_spending_them_makes_the_next_level_worth_more()
    {
        WorldClient world = await Enter();
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        Vitals born = Mine(world);

        Assert.Equal(1, born.Level);
        Assert.Equal(0, born.Unspent);

        // 한 번 잡으면 레벨이 두 단계 오른다 — 우드랜드1-1 한 마리가 3,400~4,200 인데 1→2 에 필요한
        // 경험치가 600 뿐이다. 그래서 오른 레벨 수로 나눠 **레벨 하나당** 값을 본다.
        Vitals grown = await Climb(world);

        int levels = grown.Level - born.Level;
        int gained = grown.MaximumHealth - born.MaximumHealth;

        Assert.True(
            gained == Health(born.Con) * levels,
            $"레벨이 {levels} 올랐는데 최대 체력이 {gained} 늘었습니다. Con {born.Con} 이면 레벨당 " +
            $"{Health(born.Con)}, 모두 {Health(born.Con) * levels} 이어야 합니다 " +
            $"({born.MaximumHealth} → {grown.MaximumHealth}).");

        int perLevel = gained / levels;

        // 레벨 하나에 점수가 몇 개 들어오는지. 이것이 성장의 화폐다.
        Assert.True(
            grown.Unspent > 0,
            $"레벨이 1 에서 {grown.Level} 이 되었는데 쓸 점수가 {grown.Unspent} 개입니다.");

        int purse = grown.Unspent;

        // 점수를 전부 Con 에 쓴다. 다음 레벨이 읽는 것이 Con 이므로, 성장이 성장을 키우는지 보려면
        // 여기여야 한다. 무도가 분배대로라면 이 점수는 힘으로 갔을 것이다 — 그 선택은 아래 딴 시험이 본다.
        Vitals spent = await SpendEverythingOn(world, Stat.Con);

        Assert.True(
            spent.Con == grown.Con + purse && spent.Unspent == 0,
            $"점수 {purse} 개를 Con 에 썼는데 Con 이 {grown.Con} → {spent.Con} 이고 " +
            $"남은 점수가 {spent.Unspent} 개입니다.");

        // 그리고 다음 레벨이 더 값어치가 있어야 한다 — 같은 식이 더 큰 Con 을 읽기 때문이다.
        // 점수는 이 사이에 쓰지 않는다. 오르는 도중에 Con 이 바뀌면 무엇을 읽고 계산했는지 갈리지 않는다.
        Vitals again = await Climb(world);

        int moreLevels = again.Level - spent.Level;
        int moreGained = again.MaximumHealth - spent.MaximumHealth;

        Assert.True(
            moreGained == Health(spent.Con) * moreLevels,
            $"레벨이 {moreLevels} 올랐는데 최대 체력이 {moreGained} 늘었습니다. Con {spent.Con} 이면 " +
            $"레벨당 {Health(spent.Con)}, 모두 {Health(spent.Con) * moreLevels} 이어야 합니다.");

        Assert.True(
            moreGained / moreLevels > perLevel,
            $"점수 {purse} 개를 Con 에 썼는데 레벨당 체력 증가가 {perLevel} → {moreGained / moreLevels} " +
            $"로 늘지 않았습니다. 점수를 쓰는 것이 성장에 아무 값도 하지 않는다는 뜻입니다.");
    }

    /// <summary>
    /// The build is reachable and the order it is reached in starts where it has to.
    /// </summary>
    /// <remarks>
    /// The arithmetic is the evidence for the build rather than a consequence of it. The original started
    /// a character at <c>3/3/3/3/3</c> and its Monk ended at <c>Str 64 · Con 65 · Int 43 · Wis 36</c>,
    /// which costs exactly the 196 points ninety-eight levels hand out. Hades starts thirteen points
    /// ahead, and those thirteen go on strength, so the same 196 are spent to the point here too.
    /// </remarks>
    [Fact]
    public void The_monk_build_spends_every_point_ninety_eight_levels_hand_out()
    {
        const int Levels = 99 - 1;
        const int PerLevel = 2;

        (int Str, int Int, int Wis, int Con, int Dex) born = (10, 5, 5, 5, 5);

        int cost = MonkBuild.Sum(want => want.To - want.Which switch
        {
            Stat.Str => born.Str,
            Stat.Int => born.Int,
            Stat.Wis => born.Wis,
            Stat.Con => born.Con,
            _ => born.Dex
        });

        Assert.Equal(Levels * PerLevel, cost);

        // 그리고 첫 점수는 힘으로 간다 — 77 까지 67 이 모자라 Con 의 60 보다 멀다.
        Assert.Equal(Stat.Str, NextToRaise(born.Str, born.Int, born.Wis, born.Con, born.Dex));
    }

    /// <summary>
    /// <c>Formulas/monsterexp.cs Levelup</c> — what one level adds to maximum health, read from the
    /// constitution standing at the moment it lands.
    /// </summary>
    private static int Health(int con) => (int)(HpGainFactor * con * 0.65);

    /// <summary><c>LoruleConfig.json</c>.</summary>
    private const int HpGainFactor = 5;

    /// <summary>
    /// Fights until the level goes up, without spending anything on the way.
    /// </summary>
    /// <remarks>
    /// Spending mid-climb would make the health that arrives unattributable: the level reads constitution
    /// at the moment it lands, so a point spent between two of them changes what the second is worth and
    /// there is no way afterwards to say which value was read.
    /// </remarks>
    private async Task<Vitals> Climb(WorldClient world)
    {
        int was = Mine(world).Level;

        await SwingUntil(world, enough: () => Mine(world).Level > was);

        Vitals now = Mine(world);

        Assert.True(
            now.Level > was,
            $"{MostSwings}번 휘둘렀는데 레벨이 {was} 그대로입니다 — 경험치가 들어오지 않았거나 " +
            $"괴물을 끝내지 못했습니다.");

        return now;
    }

    /// <summary>Spends every point in hand on one attribute, one packet each.</summary>
    private async Task<Vitals> SpendEverythingOn(WorldClient world, Stat which)
    {
        for (int guard = 0; guard < 64 && Mine(world).Unspent > 0; guard++)
        {
            await world.RaiseAsync(which, _deadline.Token);
            await Task.Delay(120, _deadline.Token);
        }

        return Mine(world);
    }

    /// <summary>
    /// Which attribute the next point goes on, if the character is being grown toward the Monk build:
    /// whichever of them is furthest from where it has to end up.
    /// </summary>
    /// <remarks>
    /// One packet raises one attribute and the server takes one point for it whatever the packet names, so
    /// nothing is gained by naming two. What the order buys is compounding — constitution feeds every
    /// later level's health, strength feeds every later blow — so the points are worth more the earlier
    /// they are spent, which is the whole reason a character that banks them falls behind.
    /// </remarks>
    private static Stat? NextToRaise(int str, int intellect, int wis, int con, int dex)
    {
        (Stat Which, int Standing)[] standing =
        [
            (Stat.Str, str), (Stat.Int, intellect), (Stat.Wis, wis), (Stat.Con, con), (Stat.Dex, dex)
        ];

        return MonkBuild
            .Join(standing, want => want.Which, has => has.Which,
                (want, has) => (want.Which, Short: want.To - has.Standing))
            .Where(gap => gap.Short > 0)
            .OrderByDescending(gap => gap.Short)
            .Select(gap => (Stat?)gap.Which)
            .FirstOrDefault();
    }

    private async Task SwingUntil(WorldClient world, Func<bool> enough)
    {
        for (int swings = 0; swings < MostSwings && !enough();)
        {
            if (Nearest(world) is not { } goal || world.State is not { } before)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(200, _deadline.Token);
                continue;
            }

            int dx = goal.X - before.Where.X, dy = goal.Y - before.Where.Y;
            Direction step = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            await world.WalkAsync(step, _deadline.Token);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(120, _deadline.Token);

            if (world.State is not { } after || !IsThere(world, Ahead(after.Where, step)))
            {
                continue;
            }

            await world.AttackAsync(_deadline.Token);
            swings++;

            await Task.Delay(600, _deadline.Token);
        }
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
    /// Stands the zone's smallest monster next to the entrance and stops the zone standing up its own.
    /// </summary>
    /// <remarks>
    /// Which monster dies has to be the same one every time: this test counts levels, a level is a count
    /// of experience, and the five definitions here are worth between 3,300 and 4,200 apiece. Letting the
    /// zone fill would make the number of kills to a level depend on which of them wandered past.
    /// </remarks>
    private static void StandOneMonsterAtTheEntrance(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        JsonDocumentOptions lenient = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };
        JsonSerializerOptions indented = new() { WriteIndented = true };

        JsonNode? smallest = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (!text.Contains($"\"AreaID\": {WoodlandOneOne}", StringComparison.Ordinal))
            {
                continue;
            }

            JsonNode node = JsonNode.Parse(text, documentOptions: lenient)!;

            if (smallest is null
                || ((int?)node["MaximumHP"] ?? int.MaxValue) < ((int?)smallest["MaximumHP"] ?? int.MaxValue))
            {
                smallest = JsonNode.Parse(node.ToJsonString(), documentOptions: lenient);
            }

            node["SpawnMax"] = 0;
            File.WriteAllText(path, node.ToJsonString(indented));
        }

        Assert.NotNull(smallest);

        smallest["Name"] = "우드랜드성장시험";
        smallest["SpawnType"] = SpawnDefined;
        smallest["SpawnRate"] = 1;
        smallest["SpawnMax"] = 1;
        smallest["DefinedX"] = TargetTile.X;
        smallest["DefinedY"] = TargetTile.Y;
        smallest["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(
            Path.Combine(testFolder, "woodland-progression-target.json"),
            smallest.ToJsonString(indented));
    }

    private async Task<WorldClient> Enter()
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        _servers.Add(server);
        StandOneMonsterAtTheEntrance(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is not null, "세계에 들어가지 못했습니다.");
        await Until(() => world.Vitals is not null, "서버가 내 수치를 말하지 않았습니다.");
        return world;
    }

    private async Task Until(Func<bool> wanted, string complaint)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);

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
