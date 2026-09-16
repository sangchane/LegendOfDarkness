using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// What a character keeps across leaving and coming back. The file is written as UTF-8, but it used to be read
/// back through ASCII (`AislingStorage.Load`), so every Korean letter in it came back as '?' — a 5.99 skill
/// learned as 달마신공 was ???? the next time the character entered, and the template it pointed at was gone.
/// </summary>
public sealed class CharacterSaveTests : IDisposable
{
    private const string Name = "savekr";
    private const string Skill = "달마신공";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_skill_with_a_korean_name_is_still_there_after_coming_back()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);

        using (WorldSession first = await Login(server))
        {
            WorldClient world = new(first);
            _ = world.PumpAsync(_deadline.Token);

            await Until(() => world.State is not null, "처음 들어가지 못했습니다.");
            await world.SayAsync($"/skill \"{Skill}\" 1", _deadline.Token);
            await Until(() => world.Skills.Any(s => s.Name.StartsWith(Skill)), $"{Skill}을 배우지 못했습니다.");

            // 나갈 때의 저장은 마지막 저장에서 2초가 지나야 한다(GameServer.ClientDisconnected) — 들어오자마자
            // 배우고 나가면 저장을 건너뛴다.
            await Task.Delay(TimeSpan.FromSeconds(3), _deadline.Token);
        }

        // 나갈 때 저장된다.
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        await Until(() => File.Exists(path) && File.ReadAllText(path).Contains(Skill), $"{Skill}이 파일에 저장되지 않았습니다.");

        WorldClient again = await LoginAgain(server);
        await Until(() => again.Skills.Count > 1, "다시 들어간 뒤 기술 창이 오지 않았습니다.");

        Assert.Contains(again.Skills, s => s.Name.StartsWith(Skill));
    }

    private Task<WorldSession> Login(IsolatedHadesServer server) =>
        HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

    /// <summary>Right after leaving the server may still be letting go of the old session, so try for a while.</summary>
    private async Task<WorldClient> LoginAgain(IsolatedHadesServer server)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (true)
        {
            try
            {
                WorldSession session = await Login(server);
                WorldClient world = new(session);
                _ = world.PumpAsync(_deadline.Token);
                await Until(() => world.State is not null, "다시 들어가지 못했습니다.");
                return world;
            }
            catch (Exception) when (DateTime.UtcNow < giveUp)
            {
                await Task.Delay(500, _deadline.Token);
            }
        }
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
