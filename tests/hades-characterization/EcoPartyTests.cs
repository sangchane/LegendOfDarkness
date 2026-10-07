using System.Text.Json;
using Lod.CompanionBot;
using Lod.EcoBots;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 생태계 봇 그룹 사냥(결정 19, <c>autopilot/eco-bots/party-SPEC.md</c>) — 1레벨 성직자와 11레벨 전사·도적·무도가가 한 파티로 묶여
/// 파티장의 사냥터에 함께 가고, 서버 그룹으로 경험치를 나눠 사냥하지 않는 성직자도 레벨이 오른다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class EcoPartyTests(ITestOutputHelper output) : IDisposable
{
    private static readonly EcoBotEntry[] Fighters = [new("ecpwar", 1), new("ecprogue", 2), new("ecpmonk", 5)];
    private static readonly EcoBotEntry Priest = new("ecppriest", 4);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(14));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_priest_and_three_fighters_hunt_together_and_the_priest_levels_from_the_shared_exp()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (20373, 37, 29));
        EcoBotServerTests.Configure(server, [.. Fighters.Select(bot => bot.Name), Priest.Name]);
        server.Start(TimeSpan.FromMinutes(2));

        foreach (EcoBotEntry bot in Fighters.Append(Priest))
        {
            LoginFlow.TryCreateAccount(server, bot.Name);
            CompanionCallTests.Edit(server, bot.Name, saved =>
            {
                saved["Path"] = bot.Path switch { 2 => "Rogue", 4 => "Priest", 5 => "Monk", _ => "Warrior" };
                saved["ExpLevel"] = bot.Path == 4 ? 1 : 11;
                saved["GoldPoints"] = bot.Path == 4 ? 0 : 100_000;
            });
        }

        string events = Path.Combine(server.RunRoot, "eco");
        EcoConfig config = new()
        {
            LoginPort = server.LoginPort,
            Password = LoginFlow.SyntheticSecret,
            Bots = [.. Fighters, Priest],
            EventFolder = events,
        };
        List<string> said = [];
        EcoHost host = new(config, EcoWorld.Load(HadesWorkspace.MapLayoutFolder), new EcoEvents(events), line => { lock (said) said.Add(line); });

        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token);
        Task running = host.RunAsync(stop.Token);

        bool Together() => host.PartyOf(Priest.Name) is { } party && host.Find(party.Leader) is { HuntingOn: > 0 } leader
                           && party.All.All(name => host.Find(name)?.Map == leader.HuntingOn);
        bool together = false;

        try
        {
            // 성직자는 금화 0 으로 저장돼 있다 — 처음 들어올 때 서버가 1억을 주고(EcoBots.Seed), 성직자가 그 금화로 마력 물약을 산다(buy).
            await Waiting.Until(() => (together |= Together()) && Kinds(Priest.Name).Contains("level") && Kinds(Priest.Name).Contains("buy"),
                "파티가 함께 사냥해 성직자가 레벨을 올리고 받은 금화로 마력 물약을 사지 못했습니다.", _deadline.Token, within: TimeSpan.FromMinutes(10));
        }
        finally
        {
            stop.Cancel();
            await Task.WhenAny(running, Task.Delay(5000));
            foreach (JsonElement line in Lines().Where(line => line.GetProperty("ev").GetString() is not ("tick" or "kill")))
            {
                output.WriteLine(line.ToString());
            }

            foreach (EcoBotEntry bot in Fighters.Append(Priest))
            {
                output.WriteLine($"{bot.Name}: 레벨 {host.Find(bot.Name)?.Level} 마지막 줄 {Lines().LastOrDefault(line => line.GetProperty("bot").GetString() == bot.Name)}");
            }

            lock (said)
            {
                output.WriteLine(string.Join("\n", said));
            }
        }

        JsonElement joined = Lines().First(line => line.GetProperty("ev").GetString() == "party" && line.GetProperty("data").TryGetProperty("joined", out _));
        Assert.Equal(4, joined.GetProperty("data").GetProperty("joined").GetArrayLength());
        Assert.DoesNotContain("kill", Kinds(Priest.Name));
        Assert.Contains(Lines(), line => line.GetProperty("bot").GetString() == Priest.Name && line.GetProperty("ev").GetString() == "buy"
                                         && line.GetProperty("data").GetProperty("goldBefore").GetInt64() >= 90_000_000
                                         && line.GetProperty("data").GetProperty("goldAfter").GetInt64() < line.GetProperty("data").GetProperty("goldBefore").GetInt64()
                                         && line.GetProperty("data").GetProperty("items")[0].GetProperty("name").GetString()!.Contains("마력"));

        string[] Kinds(string bot) => [.. Lines().Where(line => line.GetProperty("bot").GetString() == bot).Select(line => line.GetProperty("ev").GetString()!)];

        JsonElement[] Lines()
        {
            if (!Directory.Exists(events))
            {
                return [];
            }

            return
            [
                .. Directory.GetFiles(events, "*.jsonl")
                    .SelectMany(path => { using FileStream read = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using StreamReader text = new(read); return text.ReadToEnd().Split('\n'); })
                    .Where(line => line.Length > 0)
                    .Select(line => JsonDocument.Parse(line).RootElement.Clone()),
            ];
        }
    }
}
