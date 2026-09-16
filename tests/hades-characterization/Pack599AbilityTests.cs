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

        // 공통 기본공격(= 하데스 Assail) · 전사 · 도적 — 앞칸 한 방.
        foreach (string skill in new[] { "기본공격", "내려치기", "찌르기" })
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

            // `effect @target, 0, 102, 75` · `game_sound 75` — 102 는 **맞는 쪽** 그림이다. 0x29 는 첫 그림을 첫
            // 번호(맞는 쪽)에 그리므로, 거꾸로 보내면 시전자에게 그려진다.
            List<Effect> flashes = [];
            await Until(() =>
            {
                while (world.TakeEffect(out Effect? flash))
                {
                    flashes.Add(flash);
                }

                return flashes.Any(f => f.Target == target.Serial && f.TargetAnimation == 102);
            }, $"플레어 그림(102)이 표적 위로 오지 않았습니다: {string.Join(", ", flashes)}");

            List<int> sounds = [];
            await Until(() =>
            {
                while (world.TakeSound(out int sound))
                {
                    sounds.Add(sound);
                }

                return sounds.Contains(75);
            }, $"플레어 소리(75)가 오지 않았습니다: {string.Join(", ", sounds)}");
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

    /// <summary>
    /// 5.99 괴물 마법(`Mob_Spell.txt` 의 `Monster_이름`). 괴물 템플릿의 `SpellScripts` 에 붙으면 하데스 괴물 AI 가
    /// 표적에게 쓴다. 스크립트는 맞는 사람 쪽에서 돌고(`get_myid` = 맞는 사람), 괴물 이름은 `object_name` 이다.
    /// </summary>
    [Fact]
    public async Task A_monster_casts_its_599_spell_on_whoever_it_is_fighting()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        MakeGameMaster(server);
        PutStationaryTargetAhead(server, target =>
        {
            target["SpellScripts"] = new JsonArray("Monster_플라모");
            // 하데스 괴물은 걷기 루틴(`CommonMonster.Walk`)에서 시전을 켠다 — 제자리 고정 괴물은 마법을 못 쓴다.
            target["PathQualifer"] = 1;
            target["MoodType"] = 2;
            target["CastSpeed"] = 300;
            target["MaximumMP"] = 100;
        });
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["_MaximumHp"] = 100000;
            saved["CurrentHp"] = 100000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");
        await FindTarget(world);

        // 한 대 쳐서 괴물의 표적이 된다.
        await world.SayAsync("/skill \"기본공격\" 1", _deadline.Token);
        int slot = await Slot(() => world.Skills.FirstOrDefault(s => s.Name.StartsWith("기본공격 ("))?.Slot, "기본공격");
        await world.UseSkillAsync(slot, _deadline.Token);

        // `message 3, object_name() + "가(이) 플라모를 가합니다."` — 맞는 사람에게 온다. 글자 잇기(`+`)를 지나는
        // 첫 시험이기도 하다 — 잘못 옮기면 끝없는 재귀로 서버가 죽는다.
        await Until(() => world.Said.Contains("플라모를 가합니다"), $"괴물이 플라모를 쓰지 않았습니다: {world.Said}");
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

    private static void PutStationaryTargetAhead(IsolatedHadesServer server, Action<JsonNode>? change = null)
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
        change?.Invoke(target);

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
