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
/// A blow has to be worth what the formulas say it is worth. "Some health came off" is not that: a swing
/// that took one point off a ninety-one point monster would pass it and so would a swing that took ninety,
/// and the spell templates spent a while bound through the wrong field with nobody noticing. So this works
/// the number out first — from the character's own attributes as the server reports them, through the
/// armour and the element steps every blow goes through — and only then goes and hits something.
/// </summary>
/// <remarks>
/// <para>
/// It is a characterization test, so where the server's arithmetic reads oddly but deliberately this
/// follows it rather than correcting it. <see cref="LevelOnUse" /> is the one that matters most: the skill
/// levels on every swing, so no two swings in a row are worth the same.
/// </para>
/// <para>
/// Both directions are here because the numbers come from opposite places. Ours come from the five
/// attributes (<c>scripts/Skills/Assail.cs</c>); a monster's come out of its definition file. The health
/// and armour it stands up with are the <c>MaximumHP</c> and <c>Ac</c> written there
/// (<c>scripts/Creations/monsters.cs</c>), and its blow is rolled between the <c>DmgMin</c> and
/// <c>DmgMax</c> written there (<c>scripts/Formulas/damage.cs</c>) — the level formulas only fill in a
/// definition that left those out, or one that grows — so the prediction reads the file rather than
/// working anything out.
/// </para>
/// </remarks>
public sealed class CombatSmokeTests : IDisposable
{
    /// <summary>
    /// 우드랜드1-1 — five monster definitions, all of level one, with 115 to 240 health and blows of one to
    /// seven. The ground a fresh character is meant to start on.
    /// </summary>
    /// <remarks>
    /// Chosen because it is the one place the numbers can be measured. This used to be 지하수로D-2, whose
    /// seven definitions were all level one too, back when the server made a monster's health and blows out
    /// of its level and threw the file's numbers away. Now the file wins, and those seven are written with
    /// 17,550 health and blows of 170 to 210 — a character with 150 health dies to the first one, and our
    /// own blow moves a bar that size by one per cent. Here a blow takes off a quarter of the smallest
    /// definition and the monster's answer leaves us standing. The zone is sixty by sixty, so rather than
    /// wait for something to wander into sight <see cref="StandOneAtTheDoor" /> puts one definition on the
    /// tile ahead of us and lets nothing else stand up.
    /// </remarks>
    private const int MonsterRoom = 20015;

    /// <summary>The entrance of 우드랜드1-1, and the tile it faces — where the one monster is stood.</summary>
    private static readonly Tile Start = new(2, 35);

    private static readonly Tile TargetTile = new(2, 34);

    /// <summary><c>SpawnQualifer.Defined</c> — stand where the definition says, not on a random tile.</summary>
    private const int SpawnDefined = 4;

    private const string Name = "smokefight";

    /// <summary>
    /// A number out of <c>LoruleConfig.json</c> that the formulas read. Changing it changes what every blow
    /// from behind in the world is worth, and this test is what says so out loud.
    /// </summary>
    private const double BehindDamageMod = 0.45;

    /// <summary>What neither side having an element is worth (<c>scripts/Formulas/elements.cs</c>).</summary>
    private const double NoElementEither = 0.50;

    /// <summary>
    /// A swing that reached nothing is still announced, as a health report about serial zero
    /// (<c>Skills/Assail.cs</c>, the <c>!success</c> branch). It counts as a use of the skill, which is why
    /// it is counted here and not thrown away.
    /// </summary>
    private const uint NothingWasHit = 0;

    /// <summary>From <c>templates/skills/Assail.json</c>. Training stops here, so the damage stops rising.</summary>
    private const int AssailMaxLevel = 100;

    /// <summary>
    /// Stops short of <see cref="AssailMaxLevel" /> so the prediction never has to sit at the ceiling, and
    /// short enough that a run that is going nowhere says so in under a minute.
    /// </summary>
    private const int MostSwings = 80;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));
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
    public async Task Our_blow_takes_off_what_the_formula_says()
    {
        (WorldClient world, IsolatedHadesServer server) = await Enter();

        // 괴물이 서는 것부터가 이식의 약속이다. 안 서면 아래는 아무 의미가 없으므로 여기서 끝난다.
        Creature first = await AnyMonster(world);
        Assert.Equal(CreatureKind.Hostile, first.Kind);
        Assert.NotEqual(0u, first.Serial);

        // The server drops anything sent while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        Vitals me = Mine(world);

        // 예측이 성립하는 전제들. 무기를 들었거나 속성이 붙었으면 다른 식이 끼어든다.
        Assert.Equal(Element.None, me.Offense);
        Assert.Equal(Element.None, me.Defense);
        Assert.Empty(world.Worn);

        (int level, int health, int armor) = TheOnlyMonsterInTheRoom(server);

        // 등 뒤에서 때렸는지는 서버가 정한다(괴물이 나와 같은 쪽을 볼 때). 우리는 고를 수 없으니 둘 다
        // 세워 두고, 앞에서 때린 값이 한 번은 나올 때까지 때린다.
        await SwingUntil(world, enough: () => FirstBlows(world, me, health, armor)
            .Any(blow => blow.Left == blow.InFront));

        (int Left, int InFront, int Behind, int Use)[] blows = FirstBlows(world, me, health, armor);

        Assert.True(blows.Length > 0,
            $"{MostSwings}번 휘둘렀는데 어느 괴물도 체력이 깎이지 않았습니다 — 닿지 않았거나 기술 스크립트가 " +
            $"안 돌았습니다.{Environment.NewLine}{Said(world)}");

        foreach ((int left, int inFront, int behind, int use) in blows)
        {
            Assert.True(left == inFront || left == behind,
                $"{use}번째 휘두름 뒤 괴물의 체력이 {left}% 입니다. 식대로라면 앞에서 {inFront}%, " +
                $"등 뒤에서 {behind}% 입니다 (괴물 수준 {level}, 체력 {health}, 방어 {armor}, " +
                $"기술 수준 {LevelOnUse(use)}, 힘 {me.Str}, 민첩 {me.Dex}).{Environment.NewLine}{Said(world)}");
        }

        // 전부 0% 이면 "등 뒤" 쪽으로 통과해 버린다 — 피해가 열 배로 어긋나도 0% 이기 때문이다.
        // 그래서 앞에서 때린 값이 적어도 한 번은 나와야 한다.
        Assert.Contains(true, blows.Select(blow => blow.Left == blow.InFront));
    }

    [Fact]
    public async Task A_monsters_blow_takes_off_what_the_formula_says()
    {
        (WorldClient world, IsolatedHadesServer server) = await Enter();
        await AnyMonster(world);
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        Vitals me = Mine(world);

        Assert.Equal(Element.None, me.Defense);
        Assert.True(me.MaximumHealth > 0, "서버가 내 최대 체력을 0 이라고 합니다.");

        (int least, int most) = TheOnlyMonstersBlow(server);

        (int Drop, Vitals Then)? hit = await HitBack(world);

        Assert.True(hit is not null,
            $"때려서 시비를 걸고 기다리기를 되풀이했는데 괴물이 한 번도 되받아치지 않았습니다 — 맞아 보지 " +
            $"않고는 괴물의 공격력을 확인할 수 없습니다.{Environment.NewLine}{Said(world)}");

        (int drop, Vitals then) = hit.Value;
        int[] possible = MonsterBlows(least, most, then.Armor);

        // 죽는 한 방은 남아 있던 체력만큼만 깎는다 — 그 값은 한 방의 값이 아니라 남은 체력이다.
        // 지하수로D-2 에서 관측된 80점이 그것이었다 (81/150 에서 170~210짜리를 맞았다).
        Assert.True(then.Health >= possible[^1],
            $"맞기 전 체력이 {then.Health}점이라 가장 센 한 방({possible[^1]}점)에 죽습니다 — 깎인 점수가 남은 " +
            $"체력에서 잘려 한 방의 값을 잴 수 없습니다.{Environment.NewLine}{Said(world)}");

        Assert.True(possible.Contains(drop),
            $"괴물에게 한 대 맞고 체력이 {drop}점 깎였습니다. 식대로라면 {string.Join(", ", possible)}점 " +
            $"가운데 하나입니다 (정의의 한 방 {least}~{most}, 맞기 전 {then.Health}/{then.MaximumHealth}, " +
            $"내 수준 {then.Level}, 내 방어 {then.Armor}, 내 방어속성 {then.Defense}).{Environment.NewLine}{Said(world)}");
    }

    /// <summary>
    /// Gets hit once, and answers with how many points that one blow took off — together with the numbers we
    /// were standing on when it landed, which is what the formula reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standing still does not work on its own: half of what spawns is aggressive (<c>MoodType 4</c> is
    /// <c>Unpredicable</c>, which is a coin toss per monster) and the aggressive half only picks a target
    /// once we are within range of it. So this provokes one — <c>CommonMonster.OnDamaged</c> turns whatever
    /// we hit aggressive and points it at us — and waits; the one at the door outlives a blow from behind now,
    /// but a few provocations kill it, and whatever stands up in its place a second later has not been
    /// provoked, so it tries again.
    /// </para>
    /// <para>
    /// Points, not the percentage, because two other things move our health and the percentage cannot tell
    /// them apart. Health regenerates back up from the forty per cent a new character wakes with, and a
    /// monster dying hands over experience, which raises the level and with it the maximum — both arrive as
    /// health reports like any other. Watching the actual figures lets each of those just move the baseline
    /// instead of ruining the reading.
    /// </para>
    /// </remarks>
    private async Task<(int Drop, Vitals Then)?> HitBack(WorldClient world)
    {
        static int Monsters(WorldClient world) =>
            world.Hurts.Count(hurt => hurt.Serial != world.Serial && hurt.Serial != NothingWasHit);

        (int Drop, Vitals Then)? caught = null;

        // Monsters swing about once every one and a half seconds and regeneration is rarer still, so looking
        // this often means two of them almost never land inside one window — and if they do, the number that
        // comes out is twice as big and the failure says so.
        Task watching = Task.Run(async () =>
        {
            Vitals seen = Mine(world);

            while (caught is null && !_deadline.IsCancellationRequested)
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
                        caught = (seen.Health - now.Health, seen);
                        return;
                    }
                }

                await Task.Delay(20, _deadline.Token);
            }
        }, _deadline.Token);

        for (int attempt = 0; attempt < 6 && caught is null; attempt++)
        {
            int angered = Monsters(world);
            await SwingUntil(world, enough: () => caught is not null || Monsters(world) > angered);

            DateTime waited = DateTime.UtcNow + TimeSpan.FromSeconds(20);

            while (caught is null && DateTime.UtcNow < waited)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(300, _deadline.Token);
            }
        }

        return caught;
    }

    /// <summary>
    /// The first thing the server said about each monster's health, with what the formula says it should
    /// have been. A report is one blow landing, so the first one about a body is one blow against full
    /// health — the only kind of report a formula can be checked against without guessing what came before.
    /// </summary>
    private static (int Left, int InFront, int Behind, int Use)[] FirstBlows(
        WorldClient world, Vitals me, int health, int armor)
    {
        List<(int, int, int, int)> blows = [];
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

            blows.Add((
                left,
                PercentLeft(health, AssailDamage(me, LevelOnUse(use), armor, fromBehind: false)),
                PercentLeft(health, AssailDamage(me, LevelOnUse(use), armor, fromBehind: true)),
                use));
        }

        return [.. blows];
    }

    /// <summary>
    /// What level the skill was at on its <paramref name="use" />th use, counting from one.
    /// </summary>
    /// <remarks>
    /// <b>It goes up on every single swing.</b> <c>GameClient.TrainSkill</c> improves the skill once
    /// <c>Uses++ >= (int)(0.10 / LevelRate)</c>, and Assail's <c>LevelRate</c> is 0.5, so that threshold
    /// truncates to zero and the comparison is true the first time and every time after. A skill meant to
    /// take a hundred swings to improve improves on all hundred, and since the level is in the damage, no
    /// two swings in a row are worth the same. That is why this test has to know which swing it is looking
    /// at, and why a fight here gets visibly stronger as it goes on.
    /// </remarks>
    private static int LevelOnUse(int use) => Math.Min(1 + use, AssailMaxLevel);

    /// <summary>
    /// What one Assail of ours must take off a monster. Every step is a step the server takes, named where
    /// it lives: a step here that the server does not take is a bug in this method, not in the server.
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

    /// <summary>
    /// Everything one of a monster's swings can take off us. It swings the same Assail script we do, but
    /// through the other half of it: a monster's blow comes out of <c>scripts/Formulas/damage.cs</c>, which
    /// rolls a whole number between the <c>DmgMin</c> and <c>DmgMax</c> its definition writes — both ends
    /// included, never below one — and only falls back to its level and the gap between that and ours for a
    /// definition that wrote neither. The roll is the server's, so the answer is every value the roll can
    /// reach through our armour rather than one.
    /// </summary>
    private static int[] MonsterBlows(int least, int most, int myArmor) =>
        [.. Enumerable.Range(least, most - least + 1)
            .Select(raw => Landed(Math.Max(1, raw), myArmor))
            .Distinct()
            .Order()];

    /// <summary>The two things every blow goes through on the way in: the target's armour, then elements.</summary>
    /// <remarks>
    /// <c>scripts/Formulas/ac.cs</c>. Armour above -2 still makes a blow hurt more, and nobody starts below
    /// that: <c>GameClient.SetAislingStartupVariables</c> hands a new character <c>100 - Level / 3</c>, so it
    /// stands there wearing +100 and takes about twice what it would at -2; a level-one monster's +69 is
    /// about 1.7 times. Armour only begins to help once gear takes it under -2, down to the -70 floor.
    ///
    /// That much is the design. What was a bug, and is now fixed, is that the script used to end by
    /// returning the larger of the raw and the armoured blow — so every reduction it worked out was handed
    /// straight back, and the best armour in the game took exactly what no armour took.
    /// </remarks>
    private static int Landed(int dmg, int armor)
    {
        int armored = Math.Max(1, dmg * (armor + 101) / 99);

        return (int)Math.Abs(armored * NoElementEither);
    }

    /// <summary>How the server states health: a whole percentage of the maximum, with the rest cut off.</summary>
    private static int PercentOf(int maximum, int health) => (int)(100.0 * Math.Max(0, health) / maximum);

    /// <summary>The same, said the other way round: what is left of a full bar after a blow.</summary>
    private static int PercentLeft(int maximum, int taken) => PercentOf(maximum, maximum - taken);

    /// <summary>
    /// The one monster the room is allowed to stand up, as its definition writes it — level, health and
    /// armour read from the file rather than worked out, because the file is where the server reads them
    /// from too. <c>scripts/Creations/monsters.cs</c> keeps a written <c>MaximumHP</c> and a written
    /// <c>Ac</c>; the level formula only fills in a definition that left the health at zero, or one that
    /// grows and so answers differently every time it spawns. Both are refused here rather than predicted.
    /// </summary>
    private static (int Level, int Health, int Armor) TheOnlyMonsterInTheRoom(IsolatedHadesServer server)
    {
        JsonNode only = TheOnlyDefinitionInTheRoom(server);

        Assert.False((bool?)only["Grow"] ?? false, "자라는 정의는 설 때마다 수준이 올라 예측이 매번 달라집니다.");

        int health = (int?)only["MaximumHP"] ?? 0;
        Assert.True(health > 0, "체력이 안 적힌 정의는 서버가 수준으로 체력을 만듭니다 — 이 시험은 적힌 값을 봅니다.");

        int? armor = (int?)only["Ac"];
        Assert.True(armor is not null, "방어가 안 적힌 정의는 서버가 수준으로 방어를 만듭니다 — 이 시험은 적힌 값을 봅니다.");

        return ((int?)only["Level"] ?? 1, health, armor.Value);
    }

    /// <summary>
    /// The least and the most one swing of the only monster is written to be worth, before our armour: its
    /// <c>DmgMin</c> and <c>DmgMax</c>, the smaller first whichever way round the file has them, which is
    /// how <c>damage.cs</c> takes them. A definition that leaves them out is refused rather than predicted,
    /// because the server would then make the blow out of the level instead.
    /// </summary>
    private static (int Least, int Most) TheOnlyMonstersBlow(IsolatedHadesServer server)
    {
        JsonNode only = TheOnlyDefinitionInTheRoom(server);

        int? least = (int?)only["DmgMin"];
        int? most = (int?)only["DmgMax"];
        Assert.True(least is not null && most is not null,
            "한 방의 세기가 안 적힌 정의는 서버가 수준으로 피해를 만듭니다 — 이 시험은 적힌 값을 봅니다.");

        return (Math.Min(least.Value, most.Value), Math.Max(least.Value, most.Value));
    }

    /// <summary>
    /// The one definition the room is allowed to stand up. The room's five differ in health and in what a
    /// blow is worth, so which one we hit decides the prediction. Only the definition
    /// <see cref="StandOneAtTheDoor" /> made can spawn — the rest are left with <c>SpawnMax</c> zero — and
    /// finding two that can is refused rather than quietly averaged.
    /// </summary>
    private static JsonNode TheOnlyDefinitionInTheRoom(IsolatedHadesServer server) =>
        Assert.Single(
            DefinitionsInTheRoom(server).Select(definition => definition.Template),
            template => ((int?)template["SpawnMax"] ?? 0) > 0);

    /// <summary>
    /// Stands one of the zone's own monsters on the tile ahead of where we come in, and lets nothing else
    /// in the zone stand up — so the body we hit is the definition the numbers were read from, and it is
    /// there before we are.
    /// </summary>
    /// <remarks>
    /// Left alone the zone is sixty by sixty and its five definitions differ in health, so whichever
    /// wandered into us first would decide the prediction. The copy keeps every number this test is about
    /// — health, armour, level — and changes only where and when it appears: <c>Defined</c> at
    /// <see cref="TargetTile" />, one at a time, tried every second. The smallest of the five is the one
    /// copied, so a blow moves its bar the most and even a blow from behind leaves it standing to be hit
    /// again. The originals stay in place with <c>SpawnMax</c> zero rather than being deleted, so the room
    /// is still made of the zone's own definitions. Same shape as <c>WoodlandHuntTests</c>.
    /// </remarks>
    private static void StandOneAtTheDoor(IsolatedHadesServer server)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        (string Path, JsonNode Template)[] room = [.. DefinitionsInTheRoom(server)];

        Assert.NotEmpty(room);

        JsonNode target = room.MinBy(definition => (int?)definition.Template["MaximumHP"] ?? 0).Template.DeepClone();

        foreach ((string path, JsonNode template) in room)
        {
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        // The name is changed so the spawner counts this one separately from the definition it was copied from.
        target["Name"] = "전투시험표적";
        target["SpawnType"] = SpawnDefined;
        target["SpawnRate"] = 1;
        target["SpawnMax"] = 1;
        target["DefinedX"] = TargetTile.X;
        target["DefinedY"] = TargetTile.Y;

        string testFolder = Path.Combine(server.ContentLocation, "templates", "monsters", "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "combat-smoke-target.json"), target.ToJsonString(indented));
    }

    /// <summary>Every monster definition the server will read for the test room, and where it lives.</summary>
    private static IEnumerable<(string Path, JsonNode Template)> DefinitionsInTheRoom(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");

        // Only this room's templates are parsed, and they are found in the text first. One template Hades
        // ships is not valid JSON at all — minions/minion.json writes its image as 0x40C5 — and the
        // server's own writer leaves trailing commas behind.
        Regex thisRoom = new($"\"AreaID\"\\s*:\\s*{MonsterRoom}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (thisRoom.IsMatch(text))
            {
                yield return (path, JsonNode.Parse(text, documentOptions: lenient)!);
            }
        }
    }

    /// <summary>
    /// Walks at the nearest monster and swings only when one is standing in the tile we are facing. Swinging
    /// at the air still trains the skill, so a swing that cannot land costs the next one its predictability.
    /// </summary>
    private async Task SwingUntil(WorldClient world, Func<bool> enough)
    {
        for (int swings = 0; swings < MostSwings && !enough();)
        {
            if (Nearest(world) is not { } goal || world.State is not { } before)
            {
                await Task.Delay(200, _deadline.Token);
                continue;
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

    private static Vitals Mine(WorldClient world) =>
        world.Vitals ?? throw new InvalidOperationException("서버가 내 수치를 말하지 않았습니다 (0x08 을 못 읽었습니다).");

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
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        _servers.Add(server);
        StandOneAtTheDoor(server);
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

    /// <summary>
    /// Waits for a monster to be standing in the room, asking again as it waits.
    /// </summary>
    /// <remarks>
    /// Two things make this slow rather than instant. The spawner sweeps once a second, but it picks the
    /// tile at random and a tile that turns out to be wall wastes the attempt — and the attempt is spent
    /// either way, so that monster's definition then sits out its twenty-second <c>SpawnRate</c>. A house
    /// interior is mostly wall. Second, a monster that stands up after we walked in is not announced to us
    /// on its own; the creature list comes when we ask for it, so waiting without asking waits for ever.
    /// </remarks>
    private async Task<Creature> AnyMonster(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromMinutes(2);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Creatures.FirstOrDefault(c => c.Kind == CreatureKind.Hostile) is { } mob)
            {
                return mob;
            }

            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        throw new TimeoutException("2분을 기다렸는데 맵에 괴물이 한 마리도 서지 않았습니다");
    }

    private async Task<T> Until<T>(Func<T?> wanted, string complaint) where T : class
    {
        // 젠은 전역 타이머가 돌린다 — 괴물 정의 565개를 훑고 지나가므로 방에 첫 놈이 서기까지
        // 40초로는 모자랄 때가 있다.
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
