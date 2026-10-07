using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 경매장(<c>Types/AuctionHouse.cs</c>, 설계 <c>autopilot/loot-auction/</c>) — 올림 · 즉시 구매 · 받기 한 바퀴에서 물건은 늘 한 곳에만
/// 있고(INV-1) 금화는 수수료만큼만 줄며(INV-2), 캐릭터 저장이 실패하면 거절되고(INV-3), 서버를 껐다 켜도 그대로다(SC-004).
/// </summary>
public sealed class AuctionTests : IDisposable
{
    private const string Sword = "에페";            // 값 500 → 상인 매입가 312, 12시간 보증금 ⌊312 × 15%⌋ = 46
    private const int Deposit = 46;
    private const int Start = 1_000;
    private const int BuyoutPrice = 10_000;
    private const int Cut = BuyoutPrice * 5 / 100;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Post_buyout_take_keeps_items_and_gold()
    {
        using IsolatedHadesServer server = Ready(("aucsell", 100_000, 1), ("aucbuy", 50_000, 0));
        WorldClient seller = Pump(await Login(server, "aucsell"));
        WorldClient buyer = Pump(await Login(server, "aucbuy"));
        await Until(() => seller.Vitals?.Gold == 100_000 && buyer.Vitals?.Gold == 50_000 && seller.Pack.Any(item => item.Name == Sword),
            "두 사람의 금화·가방이 오지 않았습니다.");

        // 올림 — 물건은 가방에서 빠지고 경매장에만 있다, 보증금이 빠진다.
        AuctionDone posted = await Act(seller, () => seller.AuctionPostAsync(1, Start, BuyoutPrice, 12, _deadline.Token));
        Assert.True(posted.Ok, posted.Message);
        await Until(() => seller.Pack.All(item => item.Name != Sword) && seller.Vitals?.Gold == 100_000 - Deposit,
            $"가방 {string.Join(",", seller.Pack.Select(item => item.Name))} · 금화 {seller.Vitals?.Gold}");
        JsonNode book = Book(server);
        JsonNode listing = Assert.Single(book["Listings"]!.AsArray())!;
        Assert.Equal(Sword, (string?)listing["Item"]!["Template"]!["Name"]);
        Assert.Equal(Deposit, (int)listing["Deposit"]!);
        Assert.False(Holds(server, "aucsell", Sword), "올린 물건이 캐릭터 파일에도 남았습니다.");
        Assert.Equal(150_000, Total(server, seller, buyer));

        // 찾기 — 사는 이에게 한 줄, 파는 이 이름은 없다.
        AuctionPage found = await Browse(buyer);
        AuctionRow row = Assert.Single(found.Rows);
        Assert.Contains(Sword, row.Name);
        Assert.Equal((uint)Start, row.Price);
        Assert.Equal((uint)BuyoutPrice, row.Buyout);
        Assert.Equal(0, row.Flags);

        // 즉시 구매 — 사는 이 금화가 빠지고, 물건은 사는 이 받을 것, 대금 − 수수료 + 보증금은 파는 이 받을 것.
        AuctionDone bought = await Act(buyer, () => buyer.AuctionBuyoutAsync(row.Id, _deadline.Token));
        Assert.True(bought.Ok, bought.Message);
        Assert.Equal(1, bought.ClaimCount);
        await Until(() => buyer.Vitals?.Gold == 50_000 - BuyoutPrice, $"사는 이 금화 {buyer.Vitals?.Gold}");
        book = Book(server);
        Assert.Empty(book["Listings"]!.AsArray());
        Assert.Single(Claims(book, "aucbuy"), claim => (string?)claim["Item"]!["Template"]!["Name"] == Sword);
        Assert.Equal(BuyoutPrice - Cut + Deposit, (long)Assert.Single(Claims(book, "aucsell"))["Gold"]!);
        Assert.Equal(150_000 - Cut, Total(server, seller, buyer));

        // 받기 — 파는 이는 금화, 사는 이는 물건. 받을 것은 비고 물건은 사는 이 가방(과 캐릭터 파일)에만.
        AuctionDone paid = await Act(seller, () => seller.AuctionTakeAsync(0, _deadline.Token), done => done.Message.StartsWith("받았습니다"));
        Assert.True(paid.Ok, paid.Message);
        await Until(() => seller.Vitals?.Gold == 100_000 + BuyoutPrice - Cut, $"파는 이 금화 {seller.Vitals?.Gold}");
        AuctionDone received = await Act(buyer, () => buyer.AuctionTakeAsync(0, _deadline.Token));
        Assert.True(received.Ok, received.Message);
        Assert.Equal(0, received.ClaimCount);
        await Until(() => buyer.Pack.Any(item => item.Name == Sword), "사는 이 가방에 에페가 없습니다.");
        Assert.Empty(Book(server)["Claims"]!.AsArray());
        Assert.Equal(150_000 - Cut, Total(server, seller, buyer));
        await Until(() => Holds(server, "aucbuy", Sword), "받은 물건이 사는 이 캐릭터 파일에 저장되지 않았습니다.");
        Assert.False(Holds(server, "aucsell", Sword));

        // 사건 기록 — 조작 넷이 차례로, 모두 같은 seq 의 commit.
        var lines = Events(server);
        Assert.Equal(["post", "buyout", "take", "take"], lines.Where(line => (long)line["seq"]! > 0 && (string?)line["ev"] is not ("commit" or "abort"))
            .Select(line => (string)line["ev"]!).ToArray());
        foreach (long seq in lines.Select(line => (long)line["seq"]!).Where(seq => seq > 0).Distinct())
        {
            Assert.Contains(lines, line => (long)line["seq"]! == seq && (string?)line["ev"] == "commit");
        }
    }

    [Fact]
    public async Task A_failed_character_save_refuses_the_post()
    {
        if (OperatingSystem.IsWindows())
            return; // 폴더를 읽기 전용으로 만드는 방법이 다르다 — 서버는 맥·리눅스에서 돈다

        using IsolatedHadesServer server = Ready(("aucfail", 100_000, 1));
        WorldClient seller = Pump(await Login(server, "aucfail"));
        await Until(() => seller.Pack.Any(item => item.Name == Sword) && seller.Vitals?.Gold == 100_000, "가방·금화가 오지 않았습니다.");

        string characters = Path.Combine(server.ContentLocation, "aislings");
        UnixFileMode was = File.GetUnixFileMode(characters);
        File.SetUnixFileMode(characters, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            AuctionDone refused = await Act(seller, () => seller.AuctionPostAsync(1, Start, BuyoutPrice, 12, _deadline.Token));
            Assert.False(refused.Ok);
            Assert.Equal("저장에 실패했습니다", refused.Message);
        }
        finally
        {
            File.SetUnixFileMode(characters, was);
        }

        await Task.Delay(500, _deadline.Token);
        Assert.Contains(seller.Pack, item => item.Name == Sword && item.Slot == 1);
        Assert.Equal(100_000, seller.Vitals?.Gold);
        Assert.Empty(Book(server)["Listings"]!.AsArray());
        var lines = Events(server);
        Assert.Equal(["post", "abort"], lines.Select(line => (string)line["ev"]!).ToArray());
    }

    [Fact]
    public async Task Restart_keeps_listings_and_claims()
    {
        using IsolatedHadesServer server = Ready(("aucboot", 100_000, 2), ("aucbuy2", 50_000, 0));
        WorldClient seller = Pump(await Login(server, "aucboot"));
        WorldClient buyer = Pump(await Login(server, "aucbuy2"));
        await Until(() => seller.Pack.Count(item => item.Name == Sword) == 2, "에페 둘이 오지 않았습니다.");

        Assert.True((await Act(seller, () => seller.AuctionPostAsync(1, Start, BuyoutPrice, 12, _deadline.Token))).Ok);
        Assert.True((await Act(seller, () => seller.AuctionPostAsync(2, Start, 0, 24, _deadline.Token))).Ok);
        AuctionRow first = (await Browse(buyer)).Rows.Single(row => row.Buyout == BuyoutPrice);
        Assert.True((await Act(buyer, () => buyer.AuctionBuyoutAsync(first.Id, _deadline.Token))).Ok);
        string before = File.ReadAllText(BookPath(server));

        server.Restart(TimeSpan.FromMinutes(2));

        Assert.Equal(before, File.ReadAllText(BookPath(server)));
        seller = Pump(await LoginAgain(server, "aucboot"));
        buyer = Pump(await LoginAgain(server, "aucbuy2"));

        AuctionPage mine = await View(seller, () => seller.AuctionMineAsync(0, _deadline.Token));
        AuctionRow left = Assert.Single(mine.Rows);
        Assert.Equal(0u, left.Buyout);
        Assert.Equal(1, left.Flags & 1);
        AuctionPage sellerClaims = await View(seller, () => seller.AuctionClaimsAsync(0, _deadline.Token));
        Assert.Equal((uint)(BuyoutPrice - Cut + Deposit), Assert.Single(sellerClaims.Claims).Gold);
        AuctionPage buyerClaims = await View(buyer, () => buyer.AuctionClaimsAsync(0, _deadline.Token));
        AuctionClaim won = Assert.Single(buyerClaims.Claims);
        Assert.Contains(Sword, won.Name);

        Assert.True((await Act(buyer, () => buyer.AuctionTakeAsync(won.Id, _deadline.Token))).Ok);
        await Until(() => buyer.Pack.Any(item => item.Name == Sword), "다시 켠 뒤 받은 에페가 가방에 없습니다.");
    }

    // ---- 도우미 ----

    private IsolatedHadesServer Ready(params (string Who, int Gold, int Swords)[] people)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        foreach (var (who, gold, swords) in people)
        {
            LoginFlow.TryCreateAccount(server, who);
            CompanionCallTests.Edit(server, who, saved =>
            {
                saved["GoldPoints"] = gold;
                for (int slot = 1; slot <= swords; slot++)
                {
                    saved["Inventory"]!["Items"]![slot.ToString()] = new JsonObject
                    {
                        ["Template"] = new JsonObject { ["Name"] = Sword }, ["Slot"] = slot, ["Stacks"] = 1, ["Durability"] = 100,
                    };
                }
            });
        }

        return server;
    }

    /// <summary>경매 요청 하나를 보내고 그 답(0x5E 9)을 기다린다. 같은 사람의 요청 사이 0.3초(SPEC S-8)를 지킨다.</summary>
    private async Task<AuctionDone> Act(WorldClient world, Func<Task> send, Func<AuctionDone, bool>? wanted = null)
    {
        await Task.Delay(350, _deadline.Token);
        int seen = world.AuctionDoneCount;
        await send();
        await Until(() => world.AuctionDoneCount > seen && world.AuctionDone is { } done && (wanted?.Invoke(done) ?? true),
            $"경매 답이 오지 않았습니다. 마지막: {world.AuctionDone}");
        return world.AuctionDone!;
    }

    private async Task<AuctionPage> View(WorldClient world, Func<Task> send)
    {
        await Task.Delay(350, _deadline.Token);
        int seen = world.AuctionPageCount;
        await send();
        await Until(() => world.AuctionPageCount > seen, "경매 쪽이 오지 않았습니다.");
        return world.AuctionPage!;
    }

    private Task<AuctionPage> Browse(WorldClient world) => View(world, () => world.AuctionBrowseAsync(0, 0, 0, string.Empty, _deadline.Token));

    private static string BookPath(IsolatedHadesServer server) => Path.Combine(server.ContentLocation, "auction", "auction.json");

    private static JsonNode Book(IsolatedHadesServer server) =>
        File.Exists(BookPath(server)) ? JsonNode.Parse(File.ReadAllText(BookPath(server)))! : new JsonObject { ["Listings"] = new JsonArray(), ["Claims"] = new JsonArray() };

    private static IEnumerable<JsonNode> Claims(JsonNode book, string owner) =>
        book["Claims"]!.AsArray().Where(claim => string.Equals((string?)claim!["Owner"], owner, StringComparison.OrdinalIgnoreCase))!;

    private static List<JsonNode> Events(IsolatedHadesServer server) =>
        Directory.GetFiles(Path.Combine(server.ContentLocation, "auction"), "events-*.jsonl").Order()
            .SelectMany(File.ReadAllLines).Select(line => JsonNode.Parse(line)!).ToList();

    /// <summary>INV-2 의 합: 두 사람 금화 + 진행 중 보증금 + 맡긴 입찰금 + 받을 것 금화.</summary>
    private static long Total(IsolatedHadesServer server, params WorldClient[] people)
    {
        JsonNode book = Book(server);
        return people.Sum(world => world.Vitals?.Gold ?? 0)
               + book["Listings"]!.AsArray().Sum(listing => (long)listing!["Deposit"]! + (long)listing["Bid"]!)
               + book["Claims"]!.AsArray().Sum(claim => (long)claim!["Gold"]!);
    }

    private static bool Holds(IsolatedHadesServer server, string who, string name)
    {
        try
        {
            JsonNode saved = JsonNode.Parse(File.ReadAllText(Path.Combine(server.ContentLocation, "aislings", $"{who}.json")))!;
            return saved["Inventory"]!["Items"]!.AsObject().Any(slot => (string?)slot.Value?["Template"]?["Name"] == name);
        }
        catch (Exception)
        {
            return false; // 쓰는 도중
        }
    }

    private Task<WorldSession> Login(IsolatedHadesServer server, string who) =>
        HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

    private WorldClient Pump(WorldSession session)
    {
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        return world;
    }

    private async Task<WorldSession> LoginAgain(IsolatedHadesServer server, string who)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            try
            {
                return await Login(server, who);
            }
            catch (Exception) when (DateTime.UtcNow < giveUp)
            {
                await Task.Delay(500, _deadline.Token);
            }
        }
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
