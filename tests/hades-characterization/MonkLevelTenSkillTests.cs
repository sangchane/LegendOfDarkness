using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
