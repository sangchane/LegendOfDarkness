using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.CompanionBot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 수면(나르콜리) — "맞거나 피해를 입으면 풀린다"(사용자, 2026-09-26: 원작 규칙), 봇은 주인의 수면을 가장 먼저 푼다.
/// 포테의숲1존 사슴 한 마리에게 5.99 괴물 마법 <c>Monster_나르콜리</c>(<c>mobnar_delay @myid, 8</c> — 8초 수면)를 쥐여 준다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionSleepTests : IDisposable
{
    private const string OwnerName = "sleepowner";
    private const int ForestOne = 20263;
    private const int SleepIcon = 90; // debuff_sleep.Icon
    private static readonly Tile Deer = new(33, 46);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_sleeper_struck_by_a_monster_wakes_at_once()
    {
        using IsolatedHadesServer server = Ready(blows: true);
        WorldClient owner = await Enter(server, OwnerName);

        int last = owner.Vitals?.Health ?? 0;
        bool wasAsleep = false;
        DateTime? struckAsleep = null;
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);

        while (DateTime.UtcNow < giveUp)
        {
            bool asleep = owner.Ailments.Any(a => a.Icon == SleepIcon);
            int health = owner.Vitals?.Health ?? 0;

            // 잠들어 있던 사이에 맞았다 — 체력과 깨어남이 한 번에 올 수 있어 앞 순간의 잠을 본다.
            if (wasAsleep && health < last && struckAsleep is null)
            {
                struckAsleep = DateTime.UtcNow;
            }

            wasAsleep = asleep;

            if (struckAsleep is { } at && !asleep)
            {
                // 맞은 그 걸음에 풀린다 — 8초 수면이 저절로 끝난 것이 아니다.
                Assert.True(DateTime.UtcNow - at < TimeSpan.FromSeconds(1), "맞고 한참 뒤에야 풀렸습니다.");
                return;
            }

            last = health;
            await Task.Delay(20, _deadline.Token);
        }

        Assert.Fail($"잠든 채 맞는 순간을 보지 못했습니다(잠든 적 {(owner.Ailments.Any(a => a.Icon == SleepIcon) ? "지금" : "없음")}).");
    }

    [Fact]
    public async Task The_bot_wakes_its_sleeping_owner_within_two_seconds()
    {
        using IsolatedHadesServer server = Ready(blows: false);
        WorldClient owner = await Enter(server, OwnerName);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);
        List<string> did = [];
        _ = new CompanionRunner(bot, new MapWalls(HadesWorkspace.MapLayoutFolder), new CompanionSettings(), line =>
        {
            lock (did)
            {
                did.Add(line);
            }
        }).RunAsync(_deadline.Token);

        for (int tries = 0; tries < 10 && bot.Master is null; tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.NotNull(bot.Master);

        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        Stopwatch? asleepFor = null;
        List<double> woken = [];

        while (DateTime.UtcNow < giveUp && woken.Count < 2)
        {
            bool asleep = owner.Ailments.Any(a => a.Icon == SleepIcon);

            if (asleep && asleepFor is null)
            {
                asleepFor = Stopwatch.StartNew();
            }
            else if (!asleep && asleepFor is not null)
            {
                woken.Add(asleepFor.Elapsed.TotalSeconds);
                asleepFor = null;
            }

            await Task.Delay(20, _deadline.Token);
        }

        Assert.True(woken.Count > 0, $"주인이 잠들지 않았거나 깨지 않았습니다 · 봇이 한 일 {string.Join(" | ", did)}");
        Assert.All(woken, seconds => Assert.True(seconds <= 2.5, $"잠든 뒤 {seconds:0.0}초 만에야 풀렸습니다(8초면 저절로) · {string.Join(" | ", did)}"));
        Assert.Contains(did, line => line.StartsWith("해제 디나르콜리", StringComparison.Ordinal));
    }

    private IsolatedHadesServer Ready(bool blows)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (ForestOne, 33, 47));
        CompanionCallTests.Configure(server);

        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["MonsterSpellSuccessRate"] = 100;
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        OneSleepyDeer(server, blows);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);
        CompanionCallTests.Edit(server, OwnerName, saved =>
        {
            saved["ExpLevel"] = 30;
            saved["_MaximumHp"] = 20000;
            saved["CurrentHp"] = 20000;
        });
        CompanionCallTests.Edit(server, CompanionCallTests.BotName, saved =>
        {
            saved["CurrentMapId"] = 20373;
            saved["XPos"] = 37;
            saved["YPos"] = 29;
        });
        return server;
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null && world.Serial != 0 && world.Vitals is not null, $"{who} 가 서지 못했습니다.", _deadline.Token);
        return world;
    }

    private static void OneSleepyDeer(IsolatedHadesServer server, bool blows)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex zoneOne = new($"\"AreaID\"\\s*:\\s*{ForestOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!zoneOne.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;

            if ((string?)template["Name"] == "사슴")
            {
                template["SpawnType"] = 4;
                template["SpawnRate"] = 100_000;
                template["DefinedX"] = Deer.X;
                template["DefinedY"] = Deer.Y;
                template["MoodType"] = 2;
                template["PathQualifer"] = 2;
                template["SpellScripts"] = new JsonArray("Monster_나르콜리");
                // 치는 시험은 드물게 재운다(8초 수면 사이에 몇 번 친다) — 같은 걸음에 치고 다시 재우면 깬 순간이 안 보인다.
                template["CastSpeed"] = blows ? 9000 : 1500;

                if (!blows)
                {
                    template["AttackSpeed"] = 600_000;
                }
            }
            else
            {
                template["SpawnMax"] = 0;
            }

            File.WriteAllText(path, template.ToJsonString());
        }
    }
}
