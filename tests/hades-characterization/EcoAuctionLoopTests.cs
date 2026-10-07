using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.CompanionBot;
using Lod.EcoBots;
using Xunit;
using Xunit.Abstractions;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 생태계 봇과 경매장(설계 <c>autopilot/loot-auction/</c> FR-014·015) — 봇 프로그램(<see cref="EcoHost" />)을 그대로 붙인다. 못 입는 무도가 장갑을
/// 든 전사 봇은 장보기 때 그것을 경매에 올리고, 그 뒤 들어온 무도가 봇은 장보기 때 그것을 즉시 구매해 받아 입는다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class EcoAuctionLoopTests(ITestOutputHelper output) : IDisposable
{
    private const string Glove = "견습자의글러브";   // 무도가 11레벨 무기, 값 1,500
    private static readonly EcoBotEntry Seller = new("ecoaucw", 1);
    private static readonly EcoBotEntry Buyer = new("ecoaucm", 5);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(12));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_bot_posts_gear_it_cannot_wear_and_another_buys_and_wears_it()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (20373, 37, 29));
        EcoBotServerTests.Configure(server, Seller.Name, Buyer.Name);
        server.Start(TimeSpan.FromMinutes(2));

        foreach (EcoBotEntry bot in new[] { Seller, Buyer })
        {
            LoginFlow.TryCreateAccount(server, bot.Name);
            CompanionCallTests.Edit(server, bot.Name, saved =>
            {
                saved["Path"] = bot.Path == 5 ? "Monk" : "Warrior";
                saved["ExpLevel"] = 11;
                saved["GoldPoints"] = 20_000;
                saved["Inventory"]!["Items"]!["1"] = new JsonObject
                {
                    ["Template"] = new JsonObject { ["Name"] = bot == Seller ? Glove : "사과" },
                    ["Slot"] = 1, ["DisplayImage"] = bot == Seller ? 33866 : 32808, ["Stacks"] = bot == Seller ? 1 : 5, ["Durability"] = 100,
                };
            });
        }

        string events = Path.Combine(server.RunRoot, "eco");
        List<string> said = [];
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token);
        EcoWorld land = EcoWorld.Load(HadesWorkspace.MapLayoutFolder);
        Task Run(EcoBotEntry bot) => new EcoHost(
            new EcoConfig { LoginPort = server.LoginPort, Password = LoginFlow.SyntheticSecret, Bots = [bot], EventFolder = events },
            land, new EcoEvents(events), line => { lock (said) said.Add(line); }).RunAsync(stop.Token);

        bool Did(EcoBotEntry bot, string ev) => Lines(events).Any(line => line.GetProperty("bot").GetString() == bot.Name && line.GetProperty("ev").GetString() == ev
                                                                    && line.GetProperty("data").ToString().Contains(Glove));
        List<Task> running = [Run(Seller)];
        try
        {
            await Waiting.Until(() => Did(Seller, "auction-post"), "전사 봇이 장갑을 올리지 않았습니다.", _deadline.Token, within: TimeSpan.FromMinutes(4));

            running.Add(Run(Buyer));
            await Waiting.Until(() => Did(Buyer, "auction-buy") && Did(Buyer, "equip"), "무도가 봇이 장갑을 사 입지 않았습니다.", _deadline.Token, within: TimeSpan.FromMinutes(5));
        }
        finally
        {
            stop.Cancel();
            await Task.WhenAny(Task.WhenAll(running), Task.Delay(5000));
            foreach (JsonElement line in Lines(events))
            {
                output.WriteLine(line.ToString());
            }

            lock (said)
            {
                output.WriteLine(string.Join("\n", said));
            }
        }

        // 판 값(즉시 구매가 = 상인 매입가 937 × 4) − 수수료 5% + 보증금(24시간 30%)이 전사 봇의 받을 것에.
        JsonNode book = JsonNode.Parse(File.ReadAllText(Path.Combine(server.ContentLocation, "auction", "auction.json")))!;
        Assert.Empty(book["Listings"]!.AsArray());
        const long buyout = 937 * 4;
        Assert.Contains(book["Claims"]!.AsArray(), claim => (string?)claim!["Owner"] == Seller.Name && (long)claim["Gold"]! == buyout - (buyout * 5 / 100) + (937 * 30 / 100));
    }

    private static JsonElement[] Lines(string events) =>
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
