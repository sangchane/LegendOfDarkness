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
/// 5.99 무도가 기술 정권과 무도가 마법 넷(주먹단련 · 장풍 · 금강불괴 · 다라밀공). `scripts/build-pack-abilities.py` 가
/// 5.99 `무도가(비전직).txt` 를 문장 그대로 옮긴 것(`scripts/Pack599/Skills/정권.cs` · `Spells/*.cs`)이 도복 입은
/// 무도가에게서 5.99 에 적힌 몸동작·이펙트·소리·마력을 보내는지, 그리고 밀레스마을 리신 사범에게 배워지는지 본다.
/// </summary>
public sealed class MonkPackAbilityTests : IDisposable
{
    private const string Name = "monkpack";
    private const int WoodlandOneOne = 20015;
    private const int MilethId = 20287;
    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Jeonggwon_and_the_monk_spells_send_what_their_599_scripts_say()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        MakeGameMaster(server);
        PutStationaryTargetAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["Path"] = 5;
            saved["ExpLevel"] = 10;
            saved["_Str"] = 10;
            saved["_Dex"] = 3; // 치명타 = 민첩 ÷ 11 — 0 으로 두어 ×2 가 끼지 않게 한다.
            saved["_MaximumHp"] = 5000;
            saved["CurrentHp"] = 5000;
            saved["_MaximumMp"] = 30000;
            saved["CurrentMp"] = 30000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start
                          && world.Self?.Wearing is not null,
            "우드랜드1-1 입구에 서지 못했습니다.");
        await FindTarget(world);

        // 도복(착용이미지 3)을 입힌다 — 몸동작이 도복에서 나오는지가 이 시험의 절반이다.
        await world.SayAsync("/give \"도복\" 1", _deadline.Token);
        InventoryItem? robeItem = null;
        await Until(() => (robeItem = world.Pack.FirstOrDefault(carried => carried.Name == "도복")) is not null,
            $"도복이 소지품에 오지 않았습니다: {world.Said}");
        await world.UseAsync(robeItem!.Slot, _deadline.Token);
        await Until(() => world.Self?.Wearing?.Armor == 3, $"도복을 입지 못했습니다: {world.Said}");

        // 정권 — 주먹단련이 없으면 마력 15 · 공격력 ×2.0, 있으면 마력 25 · ×2.5. 몸 132(도복 d 시트 6~9) · 맞는 쪽 27 · 소리 18.
        int jeonggwon = await LearnSkill(world, "정권");
        Figure plain = await Blow(world, () => world.UseSkillAsync(jeonggwon, _deadline.Token), 15,
            motion: (132, 20), effect: (27, 0, 75), sound: 18, "정권");

        await LearnSpell(world, "주먹단련");
        await Task.Delay(TimeSpan.FromSeconds(2.5), _deadline.Token); // 정권 딜레이 2초
        await Blow(world, () => world.UseSkillAsync(jeonggwon, _deadline.Token), 25,
            motion: (132, 20), effect: (27, 0, 75), sound: 18, "주먹단련 뒤 정권");
        // 피해 크기는 대 보지 않는다 — 하데스가 맞은 쪽이 어디를 보고 있었나(등 ×2 …)를 곱해 한 방마다 달라진다.
        // 두 갈래는 마력(15 · 25)으로 가린다.

        // 132 는 도복이 그리는 번호다(skill.tbl 4번 줄 ST 에 3).
        int robe = world.Self!.Wearing!.Armor;
        Assert.True(BodyMotion.Fits(132, robe), $"도복({robe})이 132 를 못 그립니다.");

        // 장풍 — 마력 150 · 맞는 쪽 158 · 소리 15 · 몸 132(속도 50) · 「장풍 외웠습니다.」
        int jangpung = await LearnSpell(world, "장풍");
        await Blow(world, () => world.UseSpellAsync(jangpung, plain.Target, _deadline.Token), 150,
            motion: (132, 50), effect: (158, 0, 75), sound: 15, "장풍");
        await Told(world, "장풍 외웠습니다.");

        // 금강불괴 — 마력 500, 열에 여섯은 쓴 쪽 6 · 소리 8 · 「금강불괴을 외웠습니다.」, 나머지는 「실패했습니다.」
        {
            int slot = await LearnSpell(world, "금강불괴");
            string? said = null;
            for (int attempt = 0; attempt < 12 && said != "금강불괴을 외웠습니다."; attempt++)
            {
                Drain(world);
                int mana = world.Vitals!.Mana;
                await world.UseSpellAsync(slot, 0, _deadline.Token);
                said = await Told(world, "금강불괴을 외웠습니다.", "실패했습니다.");
                await Until(() => world.Vitals!.Mana == mana - 500, $"금강불괴가 마력 500 을 쓰지 않았습니다({mana} → {world.Vitals!.Mana}).");
            }

            Assert.Equal("금강불괴을 외웠습니다.", said);
            Effect ring = await NextEffect(world, "금강불괴");
            Assert.Equal((world.Serial, world.Serial, 6, 0, 100), (ring.Source, ring.Target, ring.SourceAnimation, ring.TargetAnimation, ring.Speed));
            await Sound(world, 8, "금강불괴");
        }

        // 다라밀공 — 마력 1300 이상에서, 맞는 쪽 288(속도 130) · 소리 98 · 체력 1 · 마력 0. 몸은 5.99 의 136(마법사 옷만) 대신
        // 도복이 그리는 손 들기 6(혼든 팩 다라밀공과 같다 — 쿠로토와 같은 까닭).
        {
            int slot = await LearnSpell(world, "다라밀공");
            Drain(world);
            int before = world.Hurts.Count;
            await world.UseSpellAsync(slot, plain.Target, _deadline.Token);
            await Until(() => world.Hurts.Skip(before).Any(h => OnAhead(world, h.Serial)), "다라밀공이 표적을 치지 않았습니다.");
            Effect flash = await NextEffect(world, "다라밀공");
            Assert.True(OnAhead(world, flash.Target), $"다라밀공 그림이 표적 위가 아닙니다: {flash}");
            Assert.Equal((288, 130), (flash.TargetAnimation, flash.Speed));
            Motion cast = await NextMotion(world, "다라밀공");
            Assert.Equal((world.Serial, 6, 75), (cast.Serial, cast.Number, cast.Speed));
            Assert.True(BodyMotion.Fits(6, robe));
            Assert.False(BodyMotion.Fits(136, robe), "도복이 136 을 그린다면 5.99 번호 그대로 돌려도 됩니다.");
            await Sound(world, 98, "다라밀공");
            await Until(() => world.Vitals!.Health == 1 && world.Vitals.Mana < 1300,
                $"다라밀공 뒤 체력 1 · 마력 0 이 아닙니다: {world.Vitals!.Health} / {world.Vitals.Mana}");
        }
    }

    /// <summary>
    /// 밀레스마을 리신(`Npc_Skill.txt` 리신 · 리신4)이 주먹단련[11] · 다라밀공[99] 을 가르친다. 리신2 는 5.99 원본이 망가져
    /// 있다 — 메뉴는 "양의신권 · 단각 · 장풍 · 금강불괴 · 구양신공" 인데 2~6번 갈래가 도적 사범 것을 베낀 채라(블로우 ·
    /// 습격진 · 백스텝 · 파이어트랩 …) 장풍을 고르면 「순수가 아닌자는 배울수없습니다.」 로 돌아간다. 문장 그대로 옮겨 하데스도 같다.
    /// </summary>
    [Fact]
    public async Task The_mileth_lee_sins_teach_the_monk_spells_as_the_599_pack_wrote_them()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MilethId, 49, 45));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["Path"] = "Monk";
            saved["ExpLevel"] = 99;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        foreach ((Tile teacher, string spell) in new[] { (new Tile(48, 46), "주먹단련"), (new Tile(48, 43), "다라밀공") })
        {
            await Converse(world, await Standing(world, teacher), spell,
                words => words.StartsWith(spell + "을 익히셧습니다") || words.StartsWith(spell + "를 익히셧습니다"));
            await Until(() => world.Spells.Any(s => s.Name.StartsWith(spell, StringComparison.Ordinal)),
                $"리신이 익혔다고 했는데 {spell}이 마법창에 없습니다.");
            await world.ShutDialogueAsync(_deadline.Token);
            await Task.Delay(300, _deadline.Token);
        }

        await Converse(world, await Standing(world, new Tile(48, 45)), "장풍", words => words == "순수가 아닌자는 배울수없습니다.");
        await Task.Delay(500, _deadline.Token);
        Assert.DoesNotContain(world.Spells, s => s.Name.StartsWith("장풍", StringComparison.Ordinal));
    }

    private async Task Converse(WorldClient world, Creature npc, string choice, Func<string, bool> done)
    {
        await world.ClickAsync(npc.Serial, _deadline.Token);
        List<string> heard = [];
        int answered = world.TalkCount;
        bool chosen = false;
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);

        while (!heard.Any(done))
        {
            Assert.True(DateTime.UtcNow < giveUp, $"{choice}을 고른 뒤 기다린 말까지 가지 못했습니다. 들은 말: {string.Join(" / ", heard)}");

            if (world.TalkCount > answered && world.Talking is { } talk)
            {
                answered = world.TalkCount;
                heard.Add(talk.What);

                DialogueOption? pick = (chosen ? null : talk.Options.FirstOrDefault(option => option.Text.StartsWith(choice + "[")))
                    ?? talk.Options.FirstOrDefault(option => option.Text == "다음");
                chosen |= pick?.Text.StartsWith(choice + "[") == true;

                if (pick is not null)
                {
                    await world.AnswerAsync(npc.Serial, pick.Step, _deadline.Token);
                }
            }

            await Task.Delay(50, _deadline.Token);
        }

        Assert.Contains("저는 무도가 스킬사범 리신입니다.", heard);
    }

    /// <summary>한 방을 쓰고, 표적이 맞은 만큼 · 마력 · 몸동작 · 이펙트 · 소리를 5.99 값과 대 본다.</summary>
    private async Task<Figure> Blow(WorldClient world, Func<Task> use, int mana,
        (int Number, int Speed) motion, (int Target, int Source, int Speed) effect, int sound, string what)
    {
        Drain(world);
        int manaBefore = world.Vitals!.Mana;
        await use();

        Figure? blow = null;
        await Until(() =>
        {
            while (blow is null && world.TakeFigure(out Figure? figure))
            {
                if (OnAhead(world, figure.Target))
                {
                    blow = figure;
                }
            }

            return blow is not null;
        }, $"{what}이 앞칸 표적을 치지 않았습니다. 마력 {manaBefore} → {world.Vitals!.Mana} · 서버: {world.Said}");

        await Until(() => world.Vitals!.Mana == manaBefore - mana,
            $"{what}이 마력 {mana} 을 쓰지 않았습니다({manaBefore} → {world.Vitals!.Mana}).");

        Motion swing = await NextMotion(world, what);
        Assert.Equal((world.Serial, motion.Number, motion.Speed), (swing.Serial, swing.Number, swing.Speed));

        Effect flash = await NextEffect(world, what);
        Assert.Equal((blow!.Target, effect.Target, effect.Source, effect.Speed),
            (flash.Target, flash.TargetAnimation, flash.SourceAnimation, flash.Speed));

        await Sound(world, sound, what);
        return blow!;
    }

    /// <summary>앞칸에 선 것인가. 시험 표적은 하나지만 클라이언트가 같은 칸에 둘을 들고 있을 때가 있다.</summary>
    private static bool OnAhead(WorldClient world, uint serial) =>
        world.Creatures.Any(creature => creature.Serial == serial && creature.Where == Ahead);

    private static void Drain(WorldClient world)
    {
        while (world.TakeMotion(out _) || world.TakeEffect(out _) || world.TakeSound(out _) || world.TakeFigure(out _)
               || world.TakeTold(out _, out _))
        {
        }
    }

    private async Task<Motion> NextMotion(WorldClient world, string what)
    {
        Motion? found = null;
        await Until(() =>
        {
            while (found is null && world.TakeMotion(out Motion? motion))
            {
                if (motion.Serial == world.Serial)
                {
                    found = motion;
                }
            }

            return found is not null;
        }, $"{what}의 몸동작이 오지 않았습니다.");
        return found!;
    }

    private async Task<Effect> NextEffect(WorldClient world, string what)
    {
        Effect? found = null;
        await Until(() => found is not null || world.TakeEffect(out found), $"{what}의 이펙트가 오지 않았습니다.");
        return found!;
    }

    private async Task Sound(WorldClient world, int number, string what)
    {
        List<int> heard = [];
        await Until(() =>
        {
            while (world.TakeSound(out int sound))
            {
                heard.Add(sound);
            }

            return heard.Contains(number);
        }, $"{what}의 소리 {number} 가 오지 않았습니다: {string.Join(", ", heard)}");
    }

    private async Task<string> Told(WorldClient world, params string[] expected)
    {
        string? found = null;
        await Until(() =>
        {
            while (found is null && world.TakeTold(out _, out string text))
            {
                if (expected.Contains(text))
                {
                    found = text;
                }
            }

            return found is not null;
        }, $"{string.Join(" 또는 ", expected)} 가 오지 않았습니다.");
        return found!;
    }

    private async Task<int> LearnSkill(WorldClient world, string skill)
    {
        await world.SayAsync($"/skill \"{skill}\" 1", _deadline.Token);
        int? slot = null;
        await Until(() => (slot = world.Skills.FirstOrDefault(s => s.Name.StartsWith(skill + " ("))?.Slot) is not null,
            $"기술 창에 {skill}이 오지 않았습니다.");
        return slot!.Value;
    }

    private async Task<int> LearnSpell(WorldClient world, string spell)
    {
        await world.SayAsync($"/spell \"{spell}\" 1", _deadline.Token);
        int? slot = null;
        await Until(() => (slot = world.Spells.FirstOrDefault(s => s.Name.StartsWith(spell))?.Slot) is not null,
            $"마법 창에 {spell}이 오지 않았습니다.");
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

        target["Name"] = "무도가마법시험표적";
        target["BaseName"] = "무도가마법시험표적";
        target["AreaID"] = WoodlandOneOne;
        target["SpawnMax"] = 1;
        target["SpawnType"] = 4;
        target["SpawnRate"] = 1;
        target["DefinedX"] = Ahead.X;
        target["DefinedY"] = Ahead.Y;
        target["MaximumHP"] = 1_000_000_000;
        target["Ac"] = 0;
        target["MoodType"] = 1;
        target["PathQualifer"] = 2;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "monk-pack-target.json"),
            target.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<Creature> FindTarget(WorldClient world)
    {
        Creature? found = null;
        await Until(() => (found = world.Creatures.FirstOrDefault(c => c.Where == Ahead)) is not null,
            "우드랜드1-1 입구 앞칸에 시험 표적이 나타나지 않았습니다.");
        return found!;
    }

    private async Task<Creature> Standing(WorldClient world, Tile where)
    {
        Creature? found = null;
        await Until(() => (found = world.Creatures.FirstOrDefault(one => one.Where == where)) is not null, $"{where} 에 아무도 없습니다.");
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
