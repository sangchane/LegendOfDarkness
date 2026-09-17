using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 NPC 퀘스트 스크립트(`Npc_Quest.txt` 그라가스)를 적힌 그대로 돌린 것. 퀘스트는 캐릭터에 진행값을 남긴다
/// (`set #gragas, 1`) — 그 값이 저장돼 **다시 접속해도** 다음 단계로 이어지는지(`Aisling.PackVariables`), 재료를 가져가면
/// `item_add "별의귀걸이"` 로 보상이 오는지 본다.
/// </summary>
public sealed class Pack599QuestTests : IDisposable
{
    private const string Name = "packquest";
    private const int NoviceHouseId = 20377;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Gragas_remembers_the_errand_across_a_new_login_and_trades_the_earring_for_the_parts()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceHouseId, 5, 4));
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);

        // 첫 접속: 심부름을 받는다.
        using (WorldSession first = await HadesLoginClient.LoginAsync(
                   IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token))
        {
            WorldClient world = new(first);
            _ = world.PumpAsync(_deadline.Token);

            Creature gragas = await Standing(world, new Tile(5, 3));
            List<string> heard = await Converse(world, gragas, "그래. 다녀올게", words => words.StartsWith("이름은 별의귀걸이"));
            Assert.Contains(heard, words => words.StartsWith("자 가서 동굴지네의 껍질 3개와"));
        }

        // 끊긴 뒤 곧바로는 저장을 건너뛴다 — 저장될 때까지 기다린다.
        await Task.Delay(TimeSpan.FromSeconds(4), _deadline.Token);

        using WorldSession second = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient again = new(second);
        _ = again.PumpAsync(_deadline.Token);

        await Until(() => again.Self is not null, "다시 들어오지 못했습니다.");
        await again.SayAsync("/give \"동굴지네의껍질\" 3", _deadline.Token);
        await again.SayAsync("/give \"동굴쥐의꼬리\" 3", _deadline.Token);
        await Until(() => again.Pack.Any(item => item.Name.StartsWith("동굴지네의껍질")) && again.Pack.Any(item => item.Name.StartsWith("동굴쥐의꼬리")),
            $"재료가 오지 않았습니다. 서버가 한 말: {again.Said}");

        Creature him = await Standing(again, new Tile(5, 3));
        List<string> later = await Converse(again, him, null, words => words.StartsWith("참고로 내가 세상에서"));

        // 진행값 1 이 남아 있어야 인사를 건너뛰고 곧장 "제대로 구해왔어??" 로 간다.
        Assert.Contains(later, words => words.StartsWith("어때 제대로 구해왔어"));
        Assert.DoesNotContain(later, words => words.StartsWith("아.. 새로운 술 제조법"));
        await Until(() => again.Pack.Any(item => item.Name.StartsWith("별의귀걸이")),
            $"별의귀걸이를 받지 못했습니다. 소지품: {string.Join(", ", again.Pack.Select(item => item.Name))}");
    }

    /// <summary>"다음"을 누르며 대화를 따라간다. 메뉴가 나오면 <paramref name="pick" /> 으로 시작하는 것을 고른다.</summary>
    private async Task<List<string>> Converse(WorldClient world, Creature npc, string? pick, Func<string, bool> done)
    {
        await world.ClickAsync(npc.Serial, _deadline.Token);

        List<string> heard = [];
        int answered = 0;
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);

        while (!heard.Any(done))
        {
            Assert.True(DateTime.UtcNow < giveUp, $"대화가 끝까지 가지 않았습니다. 들은 말: {string.Join(" / ", heard)}");

            if (world.TalkCount > answered && world.Talking is { } talk)
            {
                answered = world.TalkCount;
                heard.Add(talk.What);

                DialogueOption? choice = (pick is null ? null : talk.Options.FirstOrDefault(option => option.Text.StartsWith(pick)))
                    ?? talk.Options.FirstOrDefault(option => option.Text == "다음");

                if (choice is not null)
                {
                    await world.AnswerAsync(npc.Serial, choice.Step, _deadline.Token);
                }
            }

            await Task.Delay(50, _deadline.Token);
        }

        return heard;
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

    private static void MakeGameMaster(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(Name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
