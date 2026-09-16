using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 서버팩의 기술·마법 스크립트를 C# 으로 옮겨(`scripts/build-pack-abilities.py`) 하데스에서 돌린 것.
/// 직업마다 한 가지씩, 옮긴 문장과 통역(`Pack599.cs`)이 실제 게임에서 이어지는지를 본다.
/// </summary>
public sealed class Pack599AbilityTests : IDisposable
{
    private const string Name = "packten";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Pack_599_skills_and_spells_of_every_class_run_as_their_scripts_say()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        MakeGameMaster(server);
        PutStationaryTargetAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        // 메테오는 마력 20000 이상에서만 나간다. 체력은 쿠라노가 채울 자리를 남긴다.
        Save(server, saved =>
        {
            saved["ExpLevel"] = 99;
            saved["_MaximumHp"] = 1000;
            saved["CurrentHp"] = 500;
            saved["_MaximumMp"] = 30000;
            saved["CurrentMp"] = 30000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");
        Creature target = await FindTarget(world);

        // 전사 · 도적 — 앞칸 한 방.
        foreach (string skill in new[] { "내려치기", "찌르기" })
        {
            await world.SayAsync($"/skill \"{skill}\" 1", _deadline.Token);
            int slot = await Slot(() => world.Skills.FirstOrDefault(s => s.Name.StartsWith(skill + " ("))?.Slot, skill);
            int before = world.Hurts.Count;
            await world.UseSkillAsync(slot, _deadline.Token);
            await Until(() => world.Hurts.Skip(before).Any(h => h.Serial == target.Serial),
                $"{skill}이 앞칸 표적을 치지 않았습니다.");
        }

        // 성직자 쿠라노 — 나에게 `set_vita @target, get_vita(@target) + 지혜×20`.
        {
            int slot = await LearnSpell(world, "쿠라노");
            int health = world.Vitals!.Health;
            await world.UseSpellAsync(slot, world.Serial, _deadline.Token);
            await Until(() => world.Vitals!.Health > health, $"쿠라노가 체력 {health} 을 채우지 않았습니다.");
        }

        // 법사 플레어 — 고른 표적에. (프라보는 피해가 아니라 120초 저주다 — `magic 1`.)
        {
            int slot = await LearnSpell(world, "플레어");
            int before = world.Hurts.Count;
            await world.UseSpellAsync(slot, target.Serial, _deadline.Token);
            await Until(() => world.Hurts.Skip(before).Any(h => h.Serial == target.Serial),
                "플레어가 고른 표적을 치지 않았습니다.");
        }

        // 메테오 — 내 둘레 ±7 칸을 `for` 두 겹으로 돌며 치고, 마력을 비운다.
        {
            int slot = await LearnSpell(world, "메테오");
            int before = world.Hurts.Count;
            await world.UseSpellAsync(slot, 0, _deadline.Token);
            await Until(() => world.Hurts.Skip(before).Any(h => h.Serial == target.Serial),
                "메테오가 둘레의 표적을 치지 않았습니다.");
            // `set_manal 0` 으로 비운다. 곧 자연 회복이 채우므로 0 이 아니라 쓰는 조건(20000) 아래로 본다.
            Assert.True(world.Vitals!.Mana < 20000, $"메테오 뒤 마력이 비지 않았습니다: {world.Vitals.Mana}");
        }
    }

    private async Task<int> LearnSpell(WorldClient world, string spell)
    {
        await world.SayAsync($"/spell \"{spell}\" 1", _deadline.Token);
        return await Slot(() => world.Spells.FirstOrDefault(s => s.Name.StartsWith(spell))?.Slot, spell);
    }

    private async Task<int> Slot(Func<int?> find, string name)
    {
        int? slot = null;
        await Until(() => (slot = find()) is not null, $"{name}이 창에 오지 않았습니다.");
        return slot!.Value;
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void MakeGameMaster(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(Name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void PutStationaryTargetAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string source = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .First(path => woodland.IsMatch(File.ReadAllText(path)));
        JsonNode target = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;

        target["Name"] = "5.99기술시험표적";
        target["BaseName"] = "5.99기술시험표적";
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
        File.WriteAllText(Path.Combine(testFolder, "pack-599-target.json"),
            target.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<Creature> FindTarget(WorldClient world)
    {
        Creature? found = null;
        await Until(() => (found = world.Creatures.FirstOrDefault(c => c.Where == Ahead)) is not null,
            "우드랜드1-1 입구 앞칸에 시험 표적이 나타나지 않았습니다.");
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
