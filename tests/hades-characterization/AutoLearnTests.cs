using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 레벨이 되면 기술·마법을 저절로 익힌다 — 사범에게 가지 않아도(사용자 결정 2026-09-26).
/// </summary>
/// <remarks>
/// 표는 노바 팩 1차 스킬상인이 가르치는 레벨과 전직 첫 기술이다(사용자 결정 2026-09-27, <c>scripts/build-auto-learn.py</c> →
/// 서버 <c>AutoLearnTable.cs</c>). 기준은 레벨·직업만. 레벨업 때 그 레벨의 것을, 로그인 때 이미 넘은 레벨의 빠진 것을 한꺼번에 준다.
/// 5.99 사범만 가르치던 것(<c>AutoLearn.Withdrawn</c> — 주먹단련·양의신권 …)은 로그인 때 그 직업 창에서 치운다.
/// 정권은 운영자 명령으로만 둔다(사용자) — 저절로 생기지 않는다.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class AutoLearnTests : IDisposable
{
    /// <summary>우드랜드1-1 — <see cref="LevelUpVitalsTests" /> 와 같은 방·같은 문 앞칸.</summary>
    private const int MonsterRoom = 20015;

    private static readonly Tile Start = new(2, 35);
    private static readonly Tile TargetTile = new(2, 34);

    private const int SpawnDefined = 4;
    private const int PathFixed = 2;
    private const int MoodIdle = 1;
    private const int MostSwings = 120;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(8));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_monk_reaching_forty_one_by_levelling_learns_its_skills_without_a_teacher()
    {
        const string name = "autolevmonk";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        StandOneAtTheDoor(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);
        Edit(server, name, "Monk", level: 40, next: 1);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        List<string> told = [];

        await Until(() => world.State is not null && world.Vitals is not null, "세계에 들어가지 못했습니다.");

        // 40레벨로 들어오면 11레벨 것(노바 무도가스킬상인: 단각·이형환위·쿠로토)을 로그인 때 받는다. 41레벨 붕각·일음지는 아직 없다.
        await Until(() => Has(world.Spells.Select(s => s.Name), "쿠로토")
                          && Has(world.Skills.Select(s => s.Name), "단각") && Has(world.Skills.Select(s => s.Name), "이형환위"),
            $"40레벨 무도가가 로그인했는데 11레벨 것이 없습니다. 기술: {Names(world.Skills.Select(s => s.Name))} · 마법: {Names(world.Spells.Select(s => s.Name))}");
        Assert.False(Has(world.Skills.Select(s => s.Name), "붕각"), "41레벨 기술을 40레벨에 벌써 받았습니다.");

        await Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "문 앞에 괴물이 서지 않았습니다.");
        await SwingUntil(world, told, enough: () => (world.Vitals?.Level ?? 0) >= 41);

        Assert.True(world.Vitals?.Level == 41, $"레벨이 {world.Vitals?.Level} 입니다. 서버: {world.Said}");
        await Until(() => Has(world.Skills.Select(s => s.Name), "붕각") && Has(world.Skills.Select(s => s.Name), "일음지"),
            $"41레벨이 됐는데 붕각·일음지가 기술창에 없습니다. 기술: {Names(world.Skills.Select(s => s.Name))}");
        await Until(() => { Drain(world, told); return told.Contains("붕각을 익히셧습니다."); },
            $"익혔다는 알림이 없습니다. 들은 말: {string.Join(" / ", told)}");

        Assert.False(Has(world.Skills.Select(s => s.Name), "정권"), "정권이 저절로 생겼습니다.");
    }

    [Fact]
    public async Task Characters_already_past_the_level_get_every_missing_one_at_login()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        server.Start(TimeSpan.FromMinutes(2));

        // 직업마다 하나씩 — 그 레벨까지의 것은 있고, 다음 레벨 것은 없다.
        (string Name, string Path, int Level, string[] Skills, string[] Spells, string[] NotYet)[] cases =
        [
            // 1레벨 것은 노바 전직 첫 기술(숏블레이드·찌르기·마레노·쿠로), 나머지는 노바 1차 스킬상인. 5.99 에만 있던 것은 없다.
            ("autowarrior", "Warrior", 41, ["숏블레이드", "윈드블레이드", "메가블레이드", "바투"], ["쿠로토"],
                ["투핸드어택", "내려치기", "파워단련"]),
            ("autorogue", "Rogue", 41, ["찌르기", "센스몬스터", "찔러휘비기", "두번찌르기", "센스", "품뒤져보기"], ["쿠로토", "하이드"],
                ["습격", "아무네지아", "마구찌르기", "명중률향상(Lev1)", "명중률향상(Lev2)"]),
            ("autowizard", "Wizard", 11, [], ["마레노", "렌토", "수페라마레나", "쿠로토"], ["나르콜리", "원소이해력", "콘푸지오"]),
            ("autopriest", "Priest", 41, [],
                ["쿠로", "벨라르모", "에나르마", "이모탈", "쿠라노", "쿠러스", "홀리볼트", "디나르콜리", "디베노모", "디소루마",
                    "수페라벨라르모", "콜라마", "쿠라노소", "쿠라누스"],
                ["리베라토", "신성력강화"]),
            ("automonk", "Monk", 99,
                ["단각", "이형환위", "붕각", "일음지", "발경", "선풍각", "구양신공", "달마신공"],
                ["쿠로토", "금강불괴", "장풍", "다라밀공"], ["정권", "주먹단련", "쿠라노토", "양의신권", "일루메나"]),
        ];

        foreach ((string name, string path, int level, string[] skills, string[] spells, string[] notYet) in cases)
        {
            LoginFlow.TryCreateAccount(server, name);
            Edit(server, name, path, level, next: 1000);

            using WorldSession session = await HadesLoginClient.LoginAsync(
                IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
            WorldClient world = new(session);
            using CancellationTokenSource pump = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token);
            _ = world.PumpAsync(pump.Token);

            await Until(() => world.State is not null, $"{name} 이 세계에 들어가지 못했습니다.");
            await Until(() => skills.All(s => Has(world.Skills.Select(k => k.Name), s)) && spells.All(s => Has(world.Spells.Select(k => k.Name), s)),
                $"{path} {level}레벨이 로그인했는데 빠진 것이 있습니다. 기술: {Names(world.Skills.Select(s => s.Name))} · 마법: {Names(world.Spells.Select(s => s.Name))}");

            string[] all = [.. world.Skills.Select(s => s.Name), .. world.Spells.Select(s => s.Name)];
            foreach (string later in notYet)
            {
                Assert.False(Has(all, later), $"{path} {level}레벨에 {later} 가 생겼습니다.");
            }

            pump.Cancel();
        }
    }

    /// <summary>
    /// 이미 배운 캐릭터 — 5.99 사범만 가르치던 것(주먹단련·양의신권)은 다음 로그인 때 창에서 치운다. 그 목록에 없는 것은
    /// 남는다: 운영자 명령으로 받은 정권, 다른 직업의 5.99 전용(성직자 신성력강화 — 무도가 목록엔 없다).
    /// </summary>
    [Fact]
    public async Task Withdrawn_599_only_ones_are_cleared_at_login_and_nothing_else()
    {
        const string name = "autowithdraw";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        MakeGameMaster(server, name);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, name);
        Edit(server, name, "Monk", level: 99, next: 1000);

        WorldClient first = await Enter(server, name);
        foreach (string skill in new[] { "양의신권", "정권" })
        {
            await first.SayAsync($"/skill \"{skill}\" 1", _deadline.Token);
            await Until(() => Has(first.Skills.Select(s => s.Name), skill), $"운영자 명령으로 {skill} 을 받지 못했습니다: {first.Said}");
        }

        foreach (string spell in new[] { "주먹단련", "신성력강화" })
        {
            await first.SayAsync($"/spell \"{spell}\" 1", _deadline.Token);
            await Until(() => Has(first.Spells.Select(s => s.Name), spell), $"운영자 명령으로 {spell} 을 받지 못했습니다: {first.Said}");
        }

        await first.LogOutAsync(_deadline.Token);
        await Task.Delay(TimeSpan.FromSeconds(2), _deadline.Token);

        WorldClient again = await Enter(server, name);
        List<string> told = [];
        await Until(() => again.State is not null && Has(again.Skills.Select(s => s.Name), "단각"), "다시 들어오지 못했습니다.");
        await Until(() => !Has(again.Skills.Select(s => s.Name), "양의신권") && !Has(again.Spells.Select(s => s.Name), "주먹단련"),
            $"5.99 전용이 남았습니다. 기술: {Names(again.Skills.Select(s => s.Name))} · 마법: {Names(again.Spells.Select(s => s.Name))}");
        Assert.True(Has(again.Skills.Select(s => s.Name), "정권"), "운영자 명령으로 받은 정권까지 지웠습니다.");
        Assert.True(Has(again.Spells.Select(s => s.Name), "신성력강화"), "다른 직업의 5.99 전용(신성력강화)을 무도가에게서 지웠습니다.");
        await Until(() => { Drain(again, told); return told.Contains("주먹단련을 잊었습니다.") && told.Contains("양의신권을 잊었습니다."); },
            $"치웠다는 알림이 없습니다. 들은 말: {string.Join(" / ", told)}");
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string name)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State is not null, $"{name} 이 세계에 들어가지 못했습니다.");
        return world;
    }

    private static void MakeGameMaster(IsolatedHadesServer server, string name)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Edit(IsolatedHadesServer server, string name, string path, int level, int next)
    {
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["Path"] = path;
        character["ExpLevel"] = level;
        character["ExpNext"] = next;
        File.WriteAllText(saved, character.ToJsonString());
    }

    /// <summary>서버는 이름 뒤에 레벨을 붙여 보낸다(`이형환위 (Lev:1/100)`).</summary>
    private static bool Has(IEnumerable<string> names, string wanted) =>
        names.Any(n => n == wanted || n.StartsWith(wanted + " ", StringComparison.Ordinal));

    private static string Names(IEnumerable<string> names) => string.Join(", ", names);

    private static void Drain(WorldClient world, List<string> told)
    {
        while (world.TakeTold(out _, out string text))
        {
            told.Add(text);
        }
    }

    /// <summary>방의 정의 하나만 문 앞칸에 가만히 세운다 — <see cref="LevelUpVitalsTests" /> 와 같은 모양.</summary>
    private static void StandOneAtTheDoor(IsolatedHadesServer server)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");

        (string Path, JsonNode Template)[] room =
        [
            .. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Select(path => (path, text: File.ReadAllText(path)))
                .Where(file => file.text.Contains($"\"AreaID\": {MonsterRoom}", StringComparison.Ordinal))
                .Select(file => (file.path, JsonNode.Parse(file.text, documentOptions: lenient)!))
        ];

        Assert.NotEmpty(room);

        JsonNode target = room.MinBy(definition => (int?)definition.Template["MaximumHP"] ?? 0).Template.DeepClone();

        foreach ((string path, JsonNode template) in room)
        {
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        target["Name"] = "자동습득시험표적";
        target["SpawnType"] = SpawnDefined;
        target["SpawnRate"] = 60;
        target["SpawnMax"] = 1;
        target["DefinedX"] = TargetTile.X;
        target["DefinedY"] = TargetTile.Y;
        target["PathQualifer"] = PathFixed;
        target["MoodType"] = MoodIdle;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "auto-learn-target.json"), target.ToJsonString(indented));
    }

    private async Task SwingUntil(WorldClient world, List<string> told, Func<bool> enough)
    {
        for (int swings = 0; swings < MostSwings && !enough();)
        {
            Drain(world, told);

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

    private Task Until(Func<bool> condition, string failure) =>
        Waiting.Until(condition, failure, _deadline.Token, TimeSpan.FromSeconds(15));
}
