using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 봇을 여럿 돌린다(사용자 결정 2026-09-27 — 최대 5개). 서버 설정 <c>CompanionBots</c> 에 적힌 봇 중 비어 있는 것을 부른 사람마다 하나씩.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionManyBotsTests : IDisposable
{
    private const int NoviceVillage = 20373;
    private static readonly string[] Bots = [CompanionCallTests.BotName, $"{CompanionCallTests.BotName}2"];

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Two_owners_each_get_a_different_bot_and_a_third_hears_that_all_are_taken()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceVillage, 26, 21));
        CompanionCallTests.Configure(server);

        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["CompanionBots"] = new JsonArray([.. Bots.Select(name => JsonValue.Create(name))]);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        server.Start(TimeSpan.FromMinutes(2));

        foreach (string name in (string[])["ownerone", "ownertwo", "ownerthree", .. Bots])
        {
            LoginFlow.TryCreateAccount(server, name);
        }

        WorldClient first = await Enter(server, "ownerone");
        WorldClient second = await Enter(server, "ownertwo");
        WorldClient third = await Enter(server, "ownerthree");
        WorldClient botOne = await Enter(server, Bots[0]);
        WorldClient botTwo = await Enter(server, Bots[1]);

        await CallUntil(first, () => first.Companion is not null);
        await CallUntil(second, () => second.Companion is not null);

        Assert.NotEqual(first.Companion!.Serial, second.Companion!.Serial);
        Assert.Contains(botOne.Master?.Serial, new uint?[] { first.Serial, second.Serial });
        Assert.Contains(botTwo.Master?.Serial, new uint?[] { first.Serial, second.Serial });
        Assert.NotEqual(botOne.Master?.Serial, botTwo.Master?.Serial);

        await CallUntil(third, () => third.Said.Contains("봇이 모두 다른 분과 함께 있습니다", StringComparison.Ordinal));
        Assert.Null(third.Companion);
    }

    private async Task CallUntil(WorldClient owner, Func<bool> done)
    {
        for (int tries = 0; tries < 10 && !done(); tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.True(done(), $"부르기가 끝나지 않았습니다: {owner.Said}");
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == NoviceVillage && world.Serial != 0, $"{who} 가 서지 못했습니다.", _deadline.Token);
        return world;
    }
}
