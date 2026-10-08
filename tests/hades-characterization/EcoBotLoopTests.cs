using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.CompanionBot;
using Lod.EcoBots;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 생태계 봇 한 바퀴(설계 <c>autopilot/eco-bots/</c> SC-001·SC-002) — 격리 서버에 봇 프로그램(<see cref="EcoHost" />)을 그대로 붙인다.
/// 물약 없이 금화·사과를 든 11레벨 전사·도적·무도가는 마을로 가 사과를 팔고 물약을 사고, 서클(레벨 제한)에 맞는 장비를 사 입은 뒤
/// 사냥터로 가 괴물을 잡는다. 계정이 없는 봇은 처음 접속에 만들어져 사냥터로 간다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class EcoBotLoopTests(ITestOutputHelper output) : IDisposable
{
    private static readonly EcoBotEntry[] Made = [new("ecowar", 1), new("ecorogue", 2), new("ecomonk", 5)];
    private static readonly EcoBotEntry Fresh = new("econew", 1);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(12));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Bots_shop_for_potions_and_gear_then_hunt_and_a_new_bot_is_made()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (20373, 37, 29));
        EcoBotServerTests.Configure(server, [.. Made.Select(bot => bot.Name), Fresh.Name]);
        server.Start(TimeSpan.FromMinutes(2));

        foreach (EcoBotEntry bot in Made)
        {
            LoginFlow.TryCreateAccount(server, bot.Name);
            CompanionCallTests.Edit(server, bot.Name, saved =>
            {
                saved["Path"] = bot.Path switch { 2 => "Rogue", 5 => "Monk", _ => "Warrior" };
                saved["ExpLevel"] = 11;
                saved["GoldPoints"] = 20_000;
                saved["Inventory"]!["Items"]!["1"] = new JsonObject
                {
                    ["Template"] = new JsonObject { ["Name"] = "사과" },
                    ["Slot"] = 1,
                    ["Image"] = 0,
                    ["DisplayImage"] = 32808,
                    ["Stacks"] = 5,
                    ["Durability"] = 0,
                };
            });
        }

        string events = Path.Combine(server.RunRoot, "eco");
        EcoConfig config = new()
        {
            LoginPort = server.LoginPort,
            Password = LoginFlow.SyntheticSecret,
            Bots = [.. Made, Fresh],
            EventFolder = events,
        };
        List<string> said = [];
        EcoHost host = new(config, EcoWorld.Load(HadesWorkspace.MapLayoutFolder), new EcoEvents(events), line => { lock (said) said.Add(line); });

        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token);
        Task running = host.RunAsync(stop.Token);

        string[] Kinds(string bot) => [.. Lines().Where(line => line.GetProperty("bot").GetString() == bot).Select(line => line.GetProperty("ev").GetString()!)];
        bool Shopped(string bot) => Kinds(bot) is var kinds && kinds.Contains("sell") && kinds.Contains("buy") && kinds.Contains("equip")
                                    && Array.LastIndexOf(kinds, "move") > Array.IndexOf(kinds, "buy");

        try
        {
            await Waiting.Until(() => Made.All(bot => Shopped(bot.Name)) && Kinds(Fresh.Name).Contains("move") && Lines().Any(line => line.GetProperty("ev").GetString() == "kill"),
                "봇이 한 바퀴를 돌지 못했습니다.", _deadline.Token, within: TimeSpan.FromMinutes(8));
        }
        finally
        {
            stop.Cancel();
            await Task.WhenAny(running, Task.Delay(5000));
            foreach (JsonElement line in Lines())
            {
                output.WriteLine(line.ToString());
            }

            lock (said)
            {
                output.WriteLine(string.Join("\n", said));
            }
        }

        foreach (EcoBotEntry bot in Made)
        {
            JsonElement[] mine = [.. Lines().Where(line => line.GetProperty("bot").GetString() == bot.Name)];
            JsonElement equip = mine.First(line => line.GetProperty("ev").GetString() == "equip");
            Assert.True(equip.GetProperty("data").GetProperty("level").GetInt32() <= 11);
            JsonElement buy = mine.First(line => line.GetProperty("ev").GetString() == "buy");
            Assert.Contains(buy.GetProperty("data").GetProperty("items").EnumerateArray(), item => item.GetProperty("name").GetString()!.Contains("포션"));
        }

        Assert.True(File.Exists(Path.Combine(server.ContentLocation, "aislings", $"{Fresh.Name}.json")), "새 봇 계정이 만들어지지 않았습니다.");

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

    /// <summary>
    /// ⑤ 99 뒤(<c>autopilot/eco-bots/vitality-SPEC.md</c>) — 99 봇은 장보기 끝에 세오신전으로 가 입은 채로 쌓인 경험치로 체력을 산다.
    /// 최대 체력 1000 · 세 번 값(1000·1050·1100 × 500)만큼 쌓였으면 +150.
    /// </summary>
    [Fact]
    public async Task A_level_99_bot_buys_health_from_seo()
    {
        EcoBotEntry bot = new("ecovita", 1);
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (20373, 37, 29));
        EcoBotServerTests.Configure(server, [bot.Name]);
        server.Start(TimeSpan.FromMinutes(2));

        const long Spent = (1000 + 1050 + 1100) * 500L;
        LoginFlow.TryCreateAccount(server, bot.Name);
        CompanionCallTests.Edit(server, bot.Name, saved =>
        {
            saved["Path"] = "Warrior";
            saved["ExpLevel"] = 99;
            saved["_MaximumHp"] = 1000;
            saved["ExpBank"] = Spent + 5;
        });

        string events = Path.Combine(server.RunRoot, "eco");
        EcoConfig config = new() { LoginPort = server.LoginPort, Password = LoginFlow.SyntheticSecret, Bots = [bot], EventFolder = events };
        List<string> said = [];
        EcoHost host = new(config, EcoWorld.Load(HadesWorkspace.MapLayoutFolder), new EcoEvents(events), line => { lock (said) said.Add(line); });

        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token);
        Task running = host.RunAsync(stop.Token);
        JsonElement vitality = default;

        try
        {
            await Waiting.Until(() => Read(events).Any(line => line.GetProperty("ev").GetString() == "vitality"),
                "99 봇이 세오에게 체력을 사지 않았습니다.", _deadline.Token, within: TimeSpan.FromMinutes(8));
            vitality = Read(events).First(line => line.GetProperty("ev").GetString() == "vitality");
        }
        finally
        {
            stop.Cancel();
            await Task.WhenAny(running, Task.Delay(5000));
            lock (said)
            {
                output.WriteLine(string.Join("\n", said));
            }
        }

        JsonElement data = vitality.GetProperty("data");
        Assert.Equal(1000, data.GetProperty("mhp").GetInt32());
        Assert.Equal(1150, data.GetProperty("toMhp").GetInt32());
        Assert.Equal(Spent, data.GetProperty("spent").GetInt64());
    }

    private static JsonElement[] Read(string events) =>
        !Directory.Exists(events)
            ? []
            :
            [
                .. Directory.GetFiles(events, "*.jsonl")
                    .SelectMany(path => { using FileStream read = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using StreamReader text = new(read); return text.ReadToEnd().Split('\n'); })
                    .Where(line => line.Length > 0)
                    .Select(line => JsonDocument.Parse(line).RootElement.Clone()),
            ];
}
