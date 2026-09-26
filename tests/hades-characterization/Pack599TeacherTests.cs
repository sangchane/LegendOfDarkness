using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 기술 사범(`Npc_Skill.txt`)을 적힌 그대로 돌린 것(`scripts/build-pack-npcs.py` · `scripts/Pack599/PackNpc.cs`).
/// 밀레스마을 가렌(`가렌@밀레스마을#52,46`, 스크립트 `가렌`)은 인사 두 마디 → 전사가 아니면 거절 → 메뉴
/// "숏블레이드[5] …" → 설명 두 마디 → 레벨 확인 → `skill_add "숏블레이드"`. 플레이어가 "다음"을 누르고 메뉴를 골라야
/// 이어지므로, 대화가 멈췄다 이어 가는지까지 본다.
/// 기술은 레벨이 되면 저절로 익힌다(2026-09-26, <see cref="AutoLearnTests" />) — 11레벨 전사는 들어오며 이미 숏블레이드·
/// 윈드블레이드를 받았다. 윈드블레이드 갈래는 `skill_exist` 를 보므로 가렌은 "이미 이 스킬을 습득 하셧습니다." 로 메뉴에
/// 돌아간다. (숏블레이드 갈래는 5.99 원문에 그 검사가 없어 "익히셧습니다" 라고 말하지만 기술책에 둘이 되지는 않는다.)
/// </summary>
public sealed class Pack599TeacherTests : IDisposable
{
    private const string Name = "packteach";
    private const int MilethId = 20287;
    private const string ShortBlade = "숏블레이드";
    private const string WindBlade = "윈드블레이드";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Garen_tells_a_warrior_who_already_learned_wind_blade_by_level_that_he_has_it()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MilethId, 53, 46));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["Path"] = "Warrior";
        character["ExpLevel"] = 11;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        Creature garen = await Standing(world, new Tile(52, 46));
        await world.ClickAsync(garen.Serial, _deadline.Token);

        List<string> heard = [];
        int answered = 0;
        bool chosen = false;
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);

        // 메뉴에서 윈드블레이드는 한 번만 고른다 — 가렌은 메뉴로 돌아간다(goto re).
        while (!heard.Contains("이미 이 스킬을 습득 하셧습니다."))
        {
            Assert.True(DateTime.UtcNow < giveUp, $"이미 배웠다는 말까지 가지 못했습니다. 들은 말: {string.Join(" / ", heard)}");

            if (world.TalkCount > answered && world.Talking is { } talk)
            {
                answered = world.TalkCount;
                heard.Add(talk.What);

                DialogueOption? pick = (chosen ? null : talk.Options.FirstOrDefault(option => option.Text.StartsWith(WindBlade)))
                    ?? talk.Options.FirstOrDefault(option => option.Text == "다음");
                chosen |= pick?.Text.StartsWith(WindBlade) == true;

                if (pick is not null)
                {
                    await world.AnswerAsync(garen.Serial, pick.Step, _deadline.Token);
                }
            }

            await Task.Delay(50, _deadline.Token);
        }

        // 서버는 기술 이름에 레벨을 붙여 보낸다(`숏블레이드 (Lev:1/100)`). 하나씩만 있다.
        Assert.Single(world.Skills, skill => skill.Name.StartsWith(ShortBlade));
        Assert.Single(world.Skills, skill => skill.Name.StartsWith(WindBlade));

        Assert.Contains("저는 전사 스킬사범 가렌입니다.", heard);
        Assert.Contains(heard, words => words.StartsWith("전사님, 어느 스킬을 원하십니까?"));
        Assert.DoesNotContain(heard, words => words.StartsWith("윈드블레이드를 익히셧습니다"));
    }

    private async Task<Creature> Standing(WorldClient world, Tile where)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Creatures.FirstOrDefault(one => one.Where == where) is { } found)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"{where} 에 아무도 없습니다.");
    }
}
