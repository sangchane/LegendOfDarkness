using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// A blow has to be worth what the numbers say it is worth. "Some health came off" is not that: a swing
/// that took one point off and a swing that took ninety pass it alike, which is how spells sat bound through
/// the wrong field for weeks without anybody noticing. So this works the number out first — from the
/// character's attributes and the monster's own definition file, through armour and elements — and only then
/// goes and hits something.
/// </summary>
/// <remarks>
/// <para>
/// The numbers come from the definition files now. Each monster's file states its health, its hardest and
/// softest blow, its armour and what killing it is worth, and the server reads them
/// (<c>Creations/monsters.cs</c> · <c>Formulas/damage.cs</c> · <c>Formulas/monsterexp.cs</c>). It used to
/// work all four out from <c>Level</c> instead and write the result over what the file said — so every
/// monster in the world stood up with ninety-one health however the file was written. Monsters that state
/// nothing still fall back to the level, which is what the three Hades ships do.
/// </para>
/// <para>
/// It is still a characterization test where the server's arithmetic is odd but deliberate.
/// <see cref="LevelOnUse" /> is the one that matters most: the skill levels on every swing, so no two swings
/// in a row are worth the same.
/// </para>
/// </remarks>
public sealed class CombatSmokeTests : IDisposable
{
    /// <summary>
    /// 노비스지하던전A1 — 40x40 with four kinds of monster on it, none of them armoured and none of them
    /// hitting for more than twelve. Chosen so a fresh character can survive the measuring.
    /// </summary>
    private const int MonsterRoom = 20380;

    private const string Name = "smokefight";

    /// <summary>From <c>LoruleConfig.json</c>. Change it and every blow in the world changes with it.</summary>
    private const double BehindDamageMod = 0.45;

    /// <summary>What neither side having an element is worth (<c>scripts/Formulas/elements.cs</c>).</summary>
    private const double NoElementEither = 0.50;

    /// <summary>
    /// A swing that reached nothing is still announced, as a health report about serial zero
    /// (<c>Skills/Assail.cs</c>, the <c>!success</c> branch). It counts as a use of the skill, which is why
    /// it is counted here rather than thrown away.
    /// </summary>
    private const uint NothingWasHit = 0;

    /// <summary>From <c>templates/skills/Assail.json</c>. Training stops here, so the damage stops rising.</summary>
    private const int AssailMaxLevel = 100;

    /// <summary>Short of <see cref="AssailMaxLevel" />, and short enough that a run going nowhere says so.</summary>
    private const int MostSwings = 60;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(9));
    private readonly List<IsolatedHadesServer> _servers = [];

    /// <summary>
    /// Which drawing each body on the floor is using, remembered as we go. The server names a monster's kind
    /// only by that number, and it takes the body off the list when it dies — so a blow landed on something
    /// that is dead by the time we check would otherwise have nothing to be compared against.
    /// </summary>
    private readonly Dictionary<uint, int> _whatItWas = [];

    public void Dispose()
    {
        _deadline.Dispose();

        foreach (IsolatedHadesServer server in _servers)
        {
            server.Dispose();
        }
    }

    /// <summary>One kind of monster exactly as its definition file states it.</summary>
    private sealed record Kind(int Sprite, string Name, int Health, int Armor, int DmgMin, int DmgMax);

    [Fact]
    public async Task Our_blow_takes_off_what_the_monsters_own_numbers_say()
    {
        (WorldClient world, IsolatedHadesServer server) = await Enter();

        // 괴물이 서는 것부터가 이식의 약속이다. 안 서면 아래는 아무 의미가 없으므로 여기서 끝난다.
        Creature first = await AnyMonster(world, server);
        Assert.Equal(CreatureKind.Hostile, first.Kind);
        Assert.NotEqual(0u, first.Serial);

        // The server drops anything sent while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        Vitals me = Mine(world);

        // 예측이 성립하는 전제들. 무기를 들었거나 속성이 붙었으면 다른 식이 끼어든다.
        Assert.Equal(Element.None, me.Offense);
        Assert.Equal(Element.None, me.Defense);
        Assert.Empty(world.Worn);

        Kind[] kinds = KindsInTheRoom(server);

        // 등 뒤에서 때렸는지는 서버가 정한다(괴물이 나와 같은 쪽을 볼 때). 우리는 고를 수 없으니 둘 다
        // 세워 두고, 흔한 쪽인 "앞에서" 가 한 번 나올 때까지 때린다.
        await SwingUntil(world, enough: () => Landings(world, me, kinds).Any(hit => hit.Left == hit.InFront));

        (Kind Kind, int Left, int InFront, int Behind, int Use)[] hits = Landings(world, me, kinds);

        Assert.True(hits.Length > 0,
            $"{MostSwings}번 휘둘렀는데 어느 괴물도 체력이 깎이지 않았습니다 — 닿지 않았거나 기술 스크립트가 " +
            $"안 돌았습니다.{Environment.NewLine}{Said(world)}");

        foreach ((Kind kind, int left, int inFront, int behind, int use) in hits)
        {
            Assert.True(left == inFront || left == behind,
                $"{use}번째 휘두름이 {kind.Name} 을 때렸고 체력이 {left}% 가 됐습니다. 그 놈의 정의대로라면 " +
                $"앞에서 {inFront}%, 등 뒤에서 {behind}% 입니다 (정의: 체력 {kind.Health} · 방어 {kind.Armor}, " +
                $"기술 수준 {LevelOnUse(use)}, 내 힘 {me.Str} · 민첩 {me.Dex})." +
                $"{Environment.NewLine}{Said(world)}");
        }

        // 전부 0% 이면 "등 뒤" 쪽으로 통과해 버리므로, 앞에서 때린 값이 적어도 한 번은 나와야 한다.
        Assert.Contains(true, hits.Select(hit => hit.Left == hit.InFront));
    }

    [Fact]
    public async Task A_monsters_blow_takes_off_what_its_own_numbers_say()
    {
        (WorldClient world, IsolatedHadesServer server) = await Enter();
        await AnyMonster(world, server);
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        Vitals me = Mine(world);

        Assert.Equal(Element.None, me.Defense);
        Assert.True(me.MaximumHealth > 0, "서버가 내 최대 체력을 0 이라고 합니다.");

        Kind[] kinds = KindsInTheRoom(server);

        // 이 방의 어느 놈이 때렸는지는 알 수 없다 — 내 체력 보고는 때린 놈을 말하지 않는다. 그래서
        // 방 안 모든 정의의 최소~최대를 합집합으로 두고, 들어온 값이 그 안에 드는지 본다.
        int[] allowed = [.. kinds
            .SelectMany(kind => Enumerable.Range(kind.DmgMin, Math.Max(1, kind.DmgMax - kind.DmgMin + 1)))
            .Select(raw => Landed(raw, me.Armor))
            .Distinct()
            .Order()];

        int[] drops = await HitBack(world);

        Assert.True(drops.Length > 0,
            $"때려서 시비를 걸고 기다리기를 되풀이했는데 괴물이 한 번도 되받아치지 않았습니다 — 맞아 보지 " +
            $"않고는 괴물의 공격력을 확인할 수 없습니다.{Environment.NewLine}{Said(world)}");

        foreach (int drop in drops)
        {
            Assert.True(allowed.Contains(drop),
                $"괴물에게 맞고 체력이 {drop}점 깎였습니다. 이 방 정의대로라면 {string.Join(", ", allowed)} " +
                $"중 하나여야 합니다 (내 방어 {me.Armor}, 방 안 정의: " +
                $"{string.Join(" · ", kinds.Select(k => $"{k.Name} {k.DmgMin}~{k.DmgMax}"))})." +
                $"{Environment.NewLine}{Said(world)}");
        }

        // 값이 하나로 고정돼 있으면 정의를 안 읽고 레벨로 만든 것이다 — 레벨 쪽 식은 상수를 낸다.
        // 정의를 읽으면 최소~최대 사이를 매번 굴리므로 여러 대 맞으면 값이 갈린다.
        Assert.True(drops.Length >= 3,
            $"세 대는 맞아 봐야 굴림인지 상수인지 가릅니다. {drops.Length}대만 맞았습니다.{Environment.NewLine}{Said(world)}");
        Assert.True(drops.Distinct().Count() > 1,
            $"{drops.Length}대를 맞았는데 전부 {drops[0]}점입니다 — 정의의 최소~최대를 굴리는 게 아니라 " +
            $"레벨에서 만든 상수를 쓰고 있습니다.{Environment.NewLine}{Said(world)}");
    }

    /// <summary>
    /// Every monster we have landed a first blow on, with what its own definition says that blow should have
    /// been worth. The first report about a body is one blow against full health — the only kind of report a
    /// formula fits without guessing what came before it.
    /// </summary>
    private (Kind Kind, int Left, int InFront, int Behind, int Use)[] Landings(
        WorldClient world, Vitals me, Kind[] kinds)
    {
        List<(Kind, int, int, int, int)> hits = [];
        HashSet<uint> already = [];
        int use = 0;

        // 내 체력 보고는 괴물이 때린 것이므로 휘두른 횟수에 들지 않는다. 그 밖의 보고는 하나가 한 번의
        // 휘두름이다 — 맞으면 그 놈의 체력, 헛치면 serial 0.
        foreach ((uint serial, int left) in world.Hurts.Where(hurt => hurt.Serial != world.Serial))
        {
            use++;

            if (serial == NothingWasHit || !already.Add(serial))
            {
                continue;
            }

            if (!_whatItWas.TryGetValue(serial, out int sprite)
                || kinds.FirstOrDefault(kind => kind.Sprite == sprite) is not { } kind)
            {
                continue;
            }

            hits.Add((
                kind,
                left,
                PercentLeft(kind.Health, AssailDamage(me, LevelOnUse(use), kind.Armor, fromBehind: false)),
                PercentLeft(kind.Health, AssailDamage(me, LevelOnUse(use), kind.Armor, fromBehind: true)),
                use));
        }

        return [.. hits];
    }

    /// <summary>What level the skill was at on its <paramref name="use" />th use, counting from one.</summary>
    /// <remarks>
    /// <b>It goes up on every single swing.</b> <c>GameClient.TrainSkill</c> improves the skill once
    /// <c>Uses++ >= (int)(0.10 / LevelRate)</c>, and Assail's <c>LevelRate</c> is 0.5, so that threshold
    /// truncates to zero and the comparison is true the first time and every time after. A skill meant to
    /// take a hundred swings to improve improves on all hundred, and since the level is in the damage, no two
    /// swings in a row are worth the same. That is why this test has to know which swing it is looking at.
    /// </remarks>
    private static int LevelOnUse(int use) => Math.Min(1 + use, AssailMaxLevel);

    /// <summary>
    /// What one Assail of ours must take off a monster. Every step is a step the server takes, named where it
    /// lives: a step here that the server does not take is a bug in this method, not in the server.
    /// </summary>
    private static int AssailDamage(Vitals me, int skillLevel, int monsterArmor, bool fromBehind)
    {
        // scripts/Skills/Assail.cs — imp is ten plus the skill's level, and the division truncates.
        int dmg = me.Str * 4 + me.Dex * 2;
        dmg += dmg * (10 + skillLevel) / 100;

        // Sprite.ApplyDamage — standing behind the target adds most of the blow again. Players only.
        if (fromBehind)
        {
            dmg += (int)((dmg + BehindDamageMod) / 1.99);
        }

        return Landed(dmg, monsterArmor);
    }

    /// <summary>The two things every blow goes through on the way in: the target's armour, then elements.</summary>
    /// <remarks>
    /// <c>scripts/Formulas/ac.cs</c>. Armour above -2 still makes a blow hurt more, and a character starts
    /// above it: <c>GameClient.SetAislingStartupVariables</c> hands out <c>100 - Level / 3</c>, so a new one
    /// stands there wearing +100 and takes about twice what it would at -2. Gear is what takes it under.
    /// This room's monsters state 0, which is very nearly neutral.
    /// </remarks>
    private static int Landed(int dmg, int armor)
    {
        int armored = Math.Max(1, dmg * (armor + 101) / 99);

        return (int)Math.Abs(armored * NoElementEither);
    }

    /// <summary>How the server states health: a whole percentage of the maximum, with the rest cut off.</summary>
    private static int PercentLeft(int maximum, int taken) =>
        (int)(100.0 * Math.Max(0, maximum - taken) / maximum);

    /// <summary>
    /// The kinds of monster this room holds, straight out of their definition files. Every one of them has to
    /// state all four numbers, because that is what the prediction reads; a kind that states nothing would be
    /// worked out from its level instead and this test would be measuring the wrong thing.
    /// </summary>
    private static Kind[] KindsInTheRoom(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");

        // Only this room's definitions are parsed, and they are found in the text first. One of the files
        // Hades ships is not valid JSON at all — minions/minion.json writes its image as 0x40C5 — and the
        // server's own writer leaves trailing commas behind.
        Regex thisRoom = new($"\"AreaID\"\\s*:\\s*{MonsterRoom}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        Kind[] kinds =
        [
            .. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Where(text => thisRoom.IsMatch(text))
                .Select(text => JsonNode.Parse(text, documentOptions: lenient)!)
                .Select(one => new Kind(
                    (int)one["Image"]!,
                    (string)one["Name"]! ?? "?",
                    (int?)one["MaximumHP"] ?? 0,
                    (int?)one["Ac"] ?? int.MinValue,
                    (int?)one["DmgMin"] ?? 0,
                    (int?)one["DmgMax"] ?? 0))
        ];

        Assert.NotEmpty(kinds);

        foreach (Kind kind in kinds)
        {
            Assert.True(kind.Health > 0 && kind.Armor != int.MinValue && kind.DmgMin > 0 && kind.DmgMax > 0,
                $"{kind.Name} 의 정의가 네 수치를 다 적어 두지 않았습니다: {kind}");
        }

        // 그림 번호가 겹치면 때린 놈이 어느 정의인지 가릴 수 없다.
        Assert.Equal(kinds.Length, kinds.Select(kind => kind.Sprite).Distinct().Count());

        return kinds;
    }

    /// <summary>
    /// Walks at the nearest monster and swings only when one is standing in the tile we are facing. Swinging
    /// at the air still trains the skill, so a swing that cannot land costs the next one its predictability.
    /// </summary>
    private async Task SwingUntil(WorldClient world, Func<bool> enough)
    {
        for (int swings = 0; swings < MostSwings && !enough(); )
        {
            Remember(world);

            if (Nearest(world) is not { } goal || world.State is not { } before)
            {
                await Task.Delay(200, _deadline.Token);
                continue;
            }

            // 맞다가 죽으면 더 휘두를 수 없다. 그때는 여기서 멈춰야 실패 메시지가 식에 대한 이야기가 된다.
            if (world.Vitals is { } mine && mine.Health * 4 < mine.MaximumHealth)
            {
                return;
            }

            int dx = goal.X - before.Where.X, dy = goal.Y - before.Where.Y;
            Direction step = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            // A step is a turn as well, and a step into an occupied tile is refused while the turn stands —
            // so this is also how we come to be facing the thing we are about to hit.
            await world.WalkAsync(step, _deadline.Token);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(120, _deadline.Token);
            Remember(world);

            if (world.State is not { } after || !IsThere(world, Ahead(after.Where, step)))
            {
                continue;
            }

            await world.AttackAsync(_deadline.Token);
            swings++;

            // GlobalBaseSkillDelay 는 500ms 다. 그보다 빨리 휘두르면 서버가 그냥 버린다
            // (AssailIsReady). 빨리 치는 것이 아니라 제때 치는 것이 필요하다.
            await Task.Delay(600, _deadline.Token);
        }
    }

    /// <summary>
    /// Gets hit several times and answers with how many points each single blow took off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standing still does not work on its own: half of what spawns is aggressive (<c>MoodType 4</c> is
    /// <c>Unpredicable</c>, a coin toss per monster) and the aggressive half only picks a target once we are
    /// within range of it. So this provokes one — <c>CommonMonster.OnDamaged</c> turns whatever we hit
    /// aggressive and points it at us — and waits.
    /// </para>
    /// <para>
    /// Points, not the percentage, because two other things move our health and the percentage cannot tell
    /// them apart. Health regenerates back up from the forty per cent a new character wakes with, and a
    /// monster dying hands over experience, which raises the level and with it the maximum — both arrive as
    /// health reports like any other. Watching the figures lets each of those move the baseline instead of
    /// ruining the reading.
    /// </para>
    /// </remarks>
    private async Task<int[]> HitBack(WorldClient world)
    {
        static int Monsters(WorldClient world) =>
            world.Hurts.Count(hurt => hurt.Serial != world.Serial && hurt.Serial != NothingWasHit);

        List<int> drops = [];

        // 괴물은 1.5초에 한 번쯤 때리고 재생은 그보다 드물므로, 20ms 마다 보면 두 사건이 한 창에 겹칠
        // 일이 거의 없다 — 겹치면 깎인 점수가 두 배로 나오고 메시지가 그렇게 말해 준다.
        Task watching = Task.Run(async () =>
        {
            Vitals seen = Mine(world);

            while (drops.Count < 6 && !_deadline.IsCancellationRequested)
            {
                if (world.Vitals is { } now)
                {
                    if (now.Level != seen.Level || now.MaximumHealth != seen.MaximumHealth
                        || now.Health > seen.Health)
                    {
                        seen = now;
                    }
                    else if (now.Health < seen.Health)
                    {
                        drops.Add(seen.Health - now.Health);
                        seen = now;
                    }
                }

                await Task.Delay(20, _deadline.Token);
            }
        }, _deadline.Token);

        for (int attempt = 0; attempt < 8 && drops.Count < 3; attempt++)
        {
            int angered = Monsters(world);
            await SwingUntil(world, enough: () => drops.Count >= 3 || Monsters(world) > angered);

            DateTime waited = DateTime.UtcNow + TimeSpan.FromSeconds(20);

            while (drops.Count < 3 && DateTime.UtcNow < waited)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(300, _deadline.Token);
            }
        }

        return [.. drops];
    }

    private static Vitals Mine(WorldClient world) =>
        world.Vitals ?? throw new InvalidOperationException("서버가 내 수치를 말하지 않았습니다 (0x08 을 못 읽었습니다).");

    /// <summary>Notes which drawing each body is using, so a blow can be traced to a definition later.</summary>
    private void Remember(WorldClient world)
    {
        foreach (Creature creature in world.Creatures.Where(c => c.Kind == CreatureKind.Hostile))
        {
            _whatItWas[creature.Serial] = creature.Sprite;
        }
    }

    /// <summary>Whatever the server last said in words — the only place it names a skill's new level.</summary>
    private static string Said(WorldClient world) =>
        $"서버가 마지막으로 한 말: \"{world.Said}\" ({world.SaidCount}번 말했습니다). " +
        $"체력 보고 순서: {string.Join(", ", world.Hurts.Select(hurt => $"{hurt.Serial}={hurt.Left}%"))}";

    private static Tile Ahead(Tile from, Direction facing) => facing switch
    {
        Direction.North => new Tile(from.X, from.Y - 1),
        Direction.South => new Tile(from.X, from.Y + 1),
        Direction.East => new Tile(from.X + 1, from.Y),
        _ => new Tile(from.X - 1, from.Y)
    };

    private static bool IsThere(WorldClient world, Tile tile) =>
        world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == tile);

    private async Task<(WorldClient World, IsolatedHadesServer Server)> Enter()
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, 20, 20));
        _servers.Add(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State, "세계에 들어가지 못했습니다");
        return (world, server);
    }

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

    /// <summary>Waits for a monster to be standing in the room, asking again as it waits.</summary>
    /// <remarks>
    /// Two things make this slow rather than instant. The spawner tries each definition once and then makes
    /// that definition wait out its twenty-second <c>SpawnRate</c> — <b>even when the attempt failed</b>,
    /// because it picks the tile at random and spends the attempt whether or not the tile is wall. Second, a
    /// monster that stands up after we walked in is not announced to us on its own; the creature list comes
    /// when we ask for it, so waiting without asking waits for ever.
    /// </remarks>
    private async Task<Creature> AnyMonster(WorldClient world, IsolatedHadesServer server)
    {
        Direction[] around = [Direction.East, Direction.South, Direction.West, Direction.North];
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(2);

        for (int step = 0; DateTime.UtcNow < giveUp; step++)
        {
            if (world.Creatures.FirstOrDefault(c => c.Kind == CreatureKind.Hostile) is { } mob)
            {
                Remember(world);
                return mob;
            }

            // Standing in the middle of forty tiles by forty and waiting is the slow way: we can only be told
            // about bodies within about a dozen tiles, so most of the room is out of earshot. Walking a rough
            // box brings the rest of it into range, and turning every few steps means a wall stops us for a
            // moment rather than for good.
            await world.WalkAsync(around[step / 6 % around.Length], _deadline.Token);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(120, _deadline.Token);
        }

        string console = server.ConsoleOutput;
        string[] lines = console.Split('\n');

        throw new TimeoutException(
            $"2분을 기다렸는데 맵에 괴물이 한 마리도 서지 않았습니다. 보이는 것 {world.Creatures.Count}가지, "
            + $"내 자리 {world.State?.Where}. 서버가 찍은 마지막 20줄:{Environment.NewLine}"
            + string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 20)))
            + Environment.NewLine + "Loaded 줄: "
            + string.Join(" | ", lines.Where(l => l.Contains("Loaded") || l.Contains("Error") || l.Contains("rror"))));
    }

    private async Task<T> Until<T>(Func<T?> wanted, string complaint) where T : class
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);

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
