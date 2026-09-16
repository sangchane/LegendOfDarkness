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
/// Proves the complete novice Monk attack line rather than merely proving that its templates can be loaded.
/// The target stands at the Woodland 1-1 entrance so every use crosses the same mobile protocol and live
/// script path a player uses there.
/// </summary>
public sealed class MonkLevelTenSkillTests : IDisposable
{
    private const string Name = "monkten";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    public static TheoryData<string, string, int, string?, int> Progression => new()
    {
        { "Kick", "Kick", 1, null, 0 },
        { "High Kick", "High Kick", 4, "Kick", 5 },
        { "Double Punch", "Double Punch", 6, null, 0 },
        { "Poison Punch", "Poison Punch", 10, null, 0 },
        { "Sting", "Sting", 0, null, 0 },
    };

    [Theory]
    [MemberData(nameof(Progression))]
    public void Every_novice_monk_attack_is_bound_without_changing_its_learning_line(
        string name, string script, int level, string? required, int requiredLevel)
    {
        JsonNode template = ReadSkillTemplate(name);
        JsonNode prerequisites = template["Prerequisites"]!;

        Assert.Equal(5, (int?)prerequisites["Class_Required"]);
        Assert.Equal(level, (int?)prerequisites["ExpLevel_Required"] ?? 0);
        Assert.Equal(required, (string?)prerequisites["Skill_Required"]);
        Assert.Equal(requiredLevel, (int?)prerequisites["Skill_Level_Required"] ?? 0);
        Assert.Equal(script, (string?)template["ScriptName"]);
    }

    [Fact]
    public async Task A_level_ten_monk_can_land_every_novice_attack_in_woodland()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        MakeGameMaster(server);
        PutStationaryTargetAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        MakeLevelTenMonk(server);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback,
            server.LoginPort,
            Name,
            LoginFlow.SyntheticSecret,
            progress: null,
            _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");
        Creature target = await FindTarget(world);

        foreach ((string skill, int level) in new[]
                 {
                     ("Kick", 5),
                     ("High Kick", 4),
                     ("Double Punch", 6),
                     ("Poison Punch", 10),
                     ("Sting", 1),
                 })
        {
            await world.SayAsync($"/skill \"{skill}\" {level}", _deadline.Token);
            LearnedSkill learned = await FindSkill(world, skill);
            int before = world.Hurts.Count;

            await world.UseSkillAsync(learned.Slot, _deadline.Token);
            await Until(
                () => world.Hurts.Skip(before).Any(hurt => hurt.Serial == target.Serial),
                $"{skill}이 앞칸 표적에 체력 보고를 만들지 않았습니다.");
        }
    }

    /// <summary>
    /// The 5.99 pack's Monk skills that are not a plain multiple of the attack. Each is held to what its pack
    /// script does that a player can see: whose health moved and to what, and where we now stand. A curse laid
    /// on a monster never reaches the client, so for those the swing is all there is to see.
    /// </summary>
    [Fact]
    public async Task Every_599_monk_skill_that_is_not_a_plain_blow_does_what_its_pack_script_says()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        MakeGameMaster(server);
        PutStationaryTargetAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        MakeLevelTenMonk(server);
        // 새 캐릭터는 체력이 60 이라 `60 / 100 * 30` 이 0 이 된다. 30% 와 31% 가 갈리도록 키운다.
        // 마력도 — 일음지 하나가 80 을 쓴다.
        SetHealthAndMana(server, 1000);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback,
            server.LoginPort,
            Name,
            LoginFlow.SyntheticSecret,
            progress: null,
            _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");
        Creature target = await FindTarget(world);
        Assert.Equal(1000, world.Vitals?.Health);
        Assert.Equal(1000, world.Vitals?.Mana);

        foreach (string skill in new[] { "늑대의위상", "마구때리기" })
        {
            int before = world.Hurts.Count;
            await world.UseSkillAsync(await Learn(world, skill), _deadline.Token);
            await Until(
                () => world.Hurts.Skip(before).Any(hurt => hurt.Serial == target.Serial),
                $"{skill}이 앞칸 표적에 체력 보고를 만들지 않았습니다.");
        }

        // 마력은 5.99 의 `manal_del` 그대로: 일음지 80 · 발경 0 · 소수신공 120.
        foreach ((string skill, int mana) in new[] { ("일음지", 80), ("발경", 0), ("소수신공", 120) })
        {
            int slot = await Learn(world, skill);
            while (world.TakeMotion(out _))
            {
            }

            int before = world.Hurts.Count;
            int manaBefore = world.Vitals!.Mana;
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(() => Swung(world), $"{skill}을 써도 몸이 움직이지 않았습니다.");
            Assert.Equal(manaBefore - mana, world.Vitals!.Mana);

            if (skill == "소수신공")
            {
                Assert.Equal("소수신공을 외웠습니다.", world.Said);
            }

            // 걸 곳이 없으면 서버는 동작보다 먼저 헛손질 소리를 보낸다 — 누구의 것도 아닌 체력 보고(일련번호 0)다.
            if (skill != "소수신공")
            {
                Assert.False(
                    world.Hurts.Skip(before).Any(hurt => hurt.Serial == 0),
                    $"{skill}이 앞칸 표적에 걸리지 않았습니다.");
            }
        }

        // 달마신공 — `set_vital get_vita(@myid) / 100 * 30`: 내 체력을 **지금의** 30% 로 맞춘다.
        {
            int slot = await Learn(world, "달마신공");
            int health = world.Vitals!.Health;
            int before = world.Hurts.Count;
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(
                () => world.Hurts.Skip(before).Any(hurt => hurt.Serial == target.Serial)
                      && world.Vitals!.Health == health / 100 * 30,
                $"달마신공 뒤 체력이 {health} 의 30% 로 맞춰지지 않았습니다.");
        }

        // 구양신공 — `set_vital get_basevita(@myid) / 100 * 2`: 내 체력을 **최대의** 2% 로 맞춘다.
        {
            int slot = await Learn(world, "구양신공");
            int before = world.Hurts.Count;
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(
                () => world.Hurts.Skip(before).Any(hurt => hurt.Serial == target.Serial)
                      && world.Vitals!.Health == Math.Max(1, world.Vitals.MaximumHealth / 100 * 2),
                "구양신공 뒤 체력이 최대의 2% 로 맞춰지지 않았습니다.");
        }

        // 이형환위 — 앞의 표적을 넘어 두 칸 앞에 서서 돌아선다.
        await world.UseSkillAsync(await Learn(world, "이형환위"), _deadline.Token);
        await Until(
            () => world.State?.Where == Start with { Y = Start.Y - 2 },
            $"이형환위 뒤 {Start.X},{Start.Y - 2} 에 서지 않았습니다: {world.State?.Where}");

        // 허공답보 — 같은 건너뛰기에 한 방이 붙는다. 돌아서 있으니 표적을 다시 넘어 출발 칸으로 온다.
        {
            int slot = await Learn(world, "허공답보");
            int before = world.Hurts.Count;
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(
                () => world.State?.Where == Start
                      && world.Hurts.Skip(before).Any(hurt => hurt.Serial == target.Serial),
                $"허공답보 뒤 출발 칸에서 표적을 치지 않았습니다: {world.State?.Where}");
        }

        // 선풍각 — 둘레 네 칸을 친다. 표적을 등지고 서도 맞아야 한다.
        {
            await world.TurnAsync(Direction.South, _deadline.Token);
            int slot = await Learn(world, "선풍각");
            int before = world.Hurts.Count;
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(
                () => world.Hurts.Skip(before).Any(hurt => hurt.Serial == target.Serial),
                "선풍각이 등 뒤의 표적을 치지 않았습니다.");
        }

        // 백보신권 — 보는 쪽 앞 세 칸을 친다. 표적에서 세 칸 떨어져 서도 맞아야 한다.
        {
            // 걷는 사람에게는 위치를 알려 주지 않아 물어봐야 하고, 너무 빨리 걸으면 되돌려 보낸다.
            foreach (int step in new[] { 1, 2 })
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);
                await world.WalkAsync(Direction.South, _deadline.Token);
                await world.RefreshAsync(_deadline.Token);
                await Until(() => world.State?.Where == Start with { Y = Start.Y + step }, $"{step}칸 물러서지 못했습니다.");
            }

            await world.TurnAsync(Direction.North, _deadline.Token);

            int slot = await Learn(world, "백보신권");
            int before = world.Hurts.Count;
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(
                () => world.Hurts.Skip(before).Any(hurt => hurt.Serial == target.Serial),
                "백보신권이 세 칸 앞의 표적을 치지 않았습니다.");
        }
    }

    private async Task<int> Learn(WorldClient world, string skill)
    {
        await world.SayAsync($"/skill \"{skill}\" 1", _deadline.Token);
        return (await FindSkill(world, skill)).Slot;
    }

    private static bool Swung(WorldClient world)
    {
        while (world.TakeMotion(out Motion? motion))
        {
            if (motion.Serial == world.Serial)
            {
                return true;
            }
        }

        return false;
    }

    private static JsonNode ReadSkillTemplate(string name)
    {
        string path = Path.Combine(
            HadesWorkspace.ServerDataDirectory, "templates", "skills", $"{name}.json");
        return JsonNode.Parse(File.ReadAllText(path))!;
    }

    private static void MakeGameMaster(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(Name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void MakeLevelTenMonk(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        saved["ExpLevel"] = 10;
        saved["Path"] = 5;
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void SetHealthAndMana(IsolatedHadesServer server, int amount)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        saved["_MaximumHp"] = amount;
        saved["CurrentHp"] = amount;
        saved["_MaximumMp"] = amount;
        saved["CurrentMp"] = amount;
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void PutStationaryTargetAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions options = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };
        string source = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .First(path => woodland.IsMatch(File.ReadAllText(path)));
        JsonNode target = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;

        target["Name"] = "무도가기술시험표적";
        target["BaseName"] = "무도가기술시험표적";
        target["AreaID"] = WoodlandOneOne;
        target["SpawnMax"] = 1;
        target["SpawnType"] = 4;
        target["SpawnRate"] = 1;
        target["DefinedX"] = Ahead.X;
        target["DefinedY"] = Ahead.Y;
        target["MaximumHP"] = 1_000_000;
        target["Ac"] = 0;
        target["MoodType"] = 1;
        target["PathQualifer"] = 2;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(
            Path.Combine(testFolder, "monk-level-ten-target.json"),
            target.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<Creature> FindTarget(WorldClient world)
    {
        Creature? found = null;
        await Until(
            () => (found = world.Creatures.FirstOrDefault(creature => creature.Where == Ahead)) is not null,
            "우드랜드1-1 입구 앞칸에 시험 표적이 나타나지 않았습니다.");
        return found!;
    }

    private async Task<LearnedSkill> FindSkill(WorldClient world, string name)
    {
        LearnedSkill? found = null;
        await Until(
            () => (found = world.Skills.FirstOrDefault(skill =>
                skill.Name.StartsWith(name + " (", StringComparison.Ordinal))) is not null,
            $"기술 창에 {name}이 오지 않았습니다.");
        return found!;
    }

    private async Task Until(Func<bool> condition, string failure)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(failure);
    }
}
