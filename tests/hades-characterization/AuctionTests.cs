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

    [Fact]
    public async Task Bids_hold_the_gold_and_a_buyout_settles_the_outbid()
    {
        using IsolatedHadesServer server = Ready(("bidsell", 100_000, 1), ("bidb", 50_000, 0), ("bidc", 50_000, 0));
        WorldClient a = Pump(await Login(server, "bidsell"));
        WorldClient b = Pump(await Login(server, "bidb"));
        WorldClient c = Pump(await Login(server, "bidc"));
        WorldClient[] all = [a, b, c];
        await Until(() => all.All(world => world.Vitals is not null) && a.Pack.Any(item => item.Name == Sword), "셋이 서지 못했습니다.");

        Assert.True((await Act(a, () => a.AuctionPostAsync(1, Start, BuyoutPrice, 12, _deadline.Token))).Ok);
        uint id = Assert.Single((await Browse(b)).Rows).Id;

        Assert.True((await Act(b, () => b.AuctionBidAsync(id, 1_000, _deadline.Token))).Ok);
        await Until(() => b.Vitals!.Gold == 49_000, $"입찰금이 맡겨지지 않았습니다: {b.Vitals!.Gold}");
        Assert.Equal(200_000, Total(server, all));

        // 거절 — 상태는 그대로.
        Assert.Equal("입찰가가 낮습니다 (최소 1,050전)", (await Act(c, () => c.AuctionBidAsync(id, 1_049, _deadline.Token))).Message);
        Assert.Equal("제 물건에는 입찰할 수 없습니다", (await Act(a, () => a.AuctionBidAsync(id, 5_000, _deadline.Token))).Message);
        Assert.Equal("이미 최고 입찰자입니다", (await Act(b, () => b.AuctionBidAsync(id, 2_000, _deadline.Token))).Message);
        Assert.Equal(50_000, c.Vitals!.Gold);

        // 더 높은 입찰 — 밀린 b 는 받을 것으로 돌려받고 알림을 듣는다.
        int heard = b.AuctionDoneCount;
        Assert.True((await Act(c, () => c.AuctionBidAsync(id, 1_050, _deadline.Token))).Ok);
        await Until(() => b.AuctionDoneCount > heard && b.AuctionDone!.Message.StartsWith("입찰에서 밀렸습니다"), "밀린 이가 알림을 듣지 못했습니다.");
        Assert.Equal(1_000, Claims(Book(server), "bidb").Sum(claim => (long)claim["Gold"]!));
        Assert.Equal(200_000, Total(server, all));

        // 즉시 구매 — c 의 입찰금도 돌려준다.
        Assert.True((await Act(b, () => b.AuctionBuyoutAsync(id, _deadline.Token))).Ok);
        Assert.Equal(1_050, Claims(Book(server), "bidc").Sum(claim => (long)claim["Gold"]!));
        await Until(() => Total(server, all) == 200_000 - Cut, $"합 {Total(server, all)}");

        foreach (WorldClient world in all)
        {
            Assert.True((await Act(world, () => world.AuctionTakeAsync(0, _deadline.Token), done => done.Message.StartsWith("받았습니다"))).Ok);
        }

        await Until(() => a.Vitals!.Gold == 100_000 + BuyoutPrice - Cut && b.Vitals!.Gold == 40_000 && c.Vitals!.Gold == 50_000 && b.Pack.Any(item => item.Name == Sword),
            $"받은 뒤 금화 {a.Vitals!.Gold}·{b.Vitals!.Gold}·{c.Vitals!.Gold}");
        Assert.Empty(Book(server)["Claims"]!.AsArray());

        string[] ops = [.. Events(server).Where(line => (string?)line["ev"] is "post" or "bid" or "outbid" or "buyout").Select(line => (string)line["ev"]!)];
        Assert.Equal(["post", "bid", "bid", "outbid", "buyout", "outbid"], ops);
    }

    [Fact]
    public async Task Cancelling_returns_the_item_and_takes_a_cut_only_when_bid_on()
    {
        using IsolatedHadesServer server = Ready(("cansell", 100_000, 2), ("canbid", 50_000, 0));
        WorldClient a = Pump(await Login(server, "cansell"));
        WorldClient b = Pump(await Login(server, "canbid"));
        await Until(() => a.Pack.Count(item => item.Name == Sword) == 2 && b.Vitals is not null, "둘이 서지 못했습니다.");

        Assert.True((await Act(a, () => a.AuctionPostAsync(1, Start, 0, 12, _deadline.Token))).Ok);
        Assert.True((await Act(a, () => a.AuctionPostAsync(2, Start, 0, 12, _deadline.Token))).Ok);
        uint[] ids = [.. (await Browse(b)).Rows.Select(row => row.Id).Order()];
        Assert.True((await Act(b, () => b.AuctionBidAsync(ids[1], 2_000, _deadline.Token))).Ok);
        long gold = a.Vitals!.Gold;

        Assert.Equal("내 경매가 아닙니다", (await Act(b, () => b.AuctionCancelAsync(ids[0], _deadline.Token))).Message);

        // 입찰 없음 — 보증금만 잃고(이미 냈다) 물건은 받을 것으로.
        Assert.True((await Act(a, () => a.AuctionCancelAsync(ids[0], _deadline.Token))).Ok);
        Assert.Equal(gold, a.Vitals!.Gold);

        // 입찰 있음 — 현재가의 수수료를 내고, 입찰자는 받을 것으로 돌려받는다.
        Assert.True((await Act(a, () => a.AuctionCancelAsync(ids[1], _deadline.Token))).Ok);
        await Until(() => a.Vitals!.Gold == gold - 100, $"수수료 100 이 빠지지 않았습니다: {gold} → {a.Vitals!.Gold}");
        Assert.Equal(2_000, Claims(Book(server), "canbid").Sum(claim => (long)claim["Gold"]!));
        Assert.Equal(2, Claims(Book(server), "cansell").Count(claim => (int)claim["Reason"]! == 4));
        Assert.Equal("이미 끝난 경매입니다", (await Act(a, () => a.AuctionCancelAsync(ids[1], _deadline.Token))).Message);
        Assert.Empty(Book(server)["Listings"]!.AsArray());
    }

    [Fact]
    public async Task Expired_listings_go_back_or_sell_when_their_time_comes()
    {
        using IsolatedHadesServer server = Ready(("expsell", 100_000, 2), ("expbid", 50_000, 0));
        WorldClient a = Pump(await Login(server, "expsell"));
        WorldClient b = Pump(await Login(server, "expbid"));
        await Until(() => a.Pack.Count(item => item.Name == Sword) == 2 && b.Vitals is not null, "둘이 서지 못했습니다.");
        Assert.True((await Act(a, () => a.AuctionPostAsync(1, Start, 0, 12, _deadline.Token))).Ok);
        Assert.True((await Act(a, () => a.AuctionPostAsync(2, Start, 0, 12, _deadline.Token))).Ok);
        uint[] ids = [.. (await Browse(b)).Rows.Select(row => row.Id).Order()];
        Assert.True((await Act(b, () => b.AuctionBidAsync(ids[1], 1_000, _deadline.Token))).Ok);

        // 시간을 당긴다 — 서버를 끈 사이 두 경매의 끝 시각을 지난 시각으로.
        server.Restart(TimeSpan.FromMinutes(2), () =>
        {
            JsonNode book = Book(server);
            foreach (JsonNode? listing in book["Listings"]!.AsArray())
            {
                listing!["ExpiresAt"] = DateTime.UtcNow.AddMinutes(-1).ToString("O");
            }

            File.WriteAllText(BookPath(server), book.ToJsonString());
        });

        await Until(() => Book(server)["Listings"]!.AsArray().Count == 0, "기간이 끝난 경매가 남았습니다.");
        JsonNode after = Book(server);
        var seller = Claims(after, "expsell").ToList();
        Assert.Contains(seller, claim => (int)claim["Reason"]! == 2 && (string?)claim["Item"]!["Template"]!["Name"] == Sword);   // 유찰 — 물건만, 보증금 없음
        Assert.Contains(seller, claim => (int)claim["Reason"]! == 1 && (long)claim["Gold"]! == 1_000 - 50 + Deposit);         // 낙찰 대금
        Assert.Single(Claims(after, "expbid"), claim => (int)claim["Reason"]! == 0);                                            // 낙찰품
        string[] ends = [.. Events(server).Where(line => (string?)line["ev"] is "sold" or "expired").Select(line => (string)line["ev"]!).Order()];
        Assert.Equal(["expired", "sold"], ends);
    }

    [Fact]
    public async Task Twenty_simultaneous_buyouts_sell_each_listing_once()
    {
        const int pairs = 20;
        using IsolatedHadesServer server = Ready(("racesell", 100_000, pairs), ("racex", 30_000, 0), ("racey", 30_000, 0));
        WorldClient seller = Pump(await Login(server, "racesell"));
        WorldClient x = Pump(await Login(server, "racex"));
        WorldClient y = Pump(await Login(server, "racey"));
        await Until(() => seller.Pack.Count(item => item.Name == Sword) == pairs && x.Vitals is not null && y.Vitals is not null, "셋이 서지 못했습니다.");

        for (int slot = 1; slot <= pairs; slot++)
        {
            byte at = (byte)slot;
            Assert.True((await Act(seller, () => seller.AuctionPostAsync(at, 100, 1_000, 12, _deadline.Token))).Ok);
        }

        uint[] ids = [.. (await Browse(x)).Rows.Select(row => row.Id)];
        Assert.Equal(pairs, ids.Length);
        foreach (uint id in ids)
        {
            AuctionDone[] both = await Task.WhenAll(
                Act(x, () => x.AuctionBuyoutAsync(id, _deadline.Token)),
                Act(y, () => y.AuctionBuyoutAsync(id, _deadline.Token)));
            Assert.Single(both, done => done.Ok);
            Assert.Single(both, done => done.Message == "이미 끝난 경매입니다");
        }

        JsonNode book = Book(server);
        Assert.Empty(book["Listings"]!.AsArray());
        Assert.Equal(pairs, Claims(book, "racex").Count() + Claims(book, "racey").Count());
        await Until(() => x.Vitals!.Gold + y.Vitals!.Gold == 60_000 - (pairs * 1_000), $"사는 이 금화 {x.Vitals!.Gold}+{y.Vitals!.Gold}");
        Assert.Equal(Claims(book, "racex").Count() * 1_000, 30_000 - x.Vitals!.Gold);
    }

    [Fact]
    public async Task Bad_values_and_hasty_requests_are_turned_back()
    {
        using IsolatedHadesServer server = Ready(("badsell", 100_000, 1), ("badpoor", 0, 1));
        WorldClient a = Pump(await Login(server, "badsell"));
        WorldClient poor = Pump(await Login(server, "badpoor"));
        await Until(() => a.Pack.Any(item => item.Name == Sword) && poor.Pack.Any(item => item.Name == Sword), "둘이 서지 못했습니다.");

        Assert.Equal("값이 맞지 않습니다", (await Act(a, () => a.AuctionPostAsync(1, Start, 0, 13, _deadline.Token))).Message);
        Assert.Equal("값이 맞지 않습니다", (await Act(a, () => a.AuctionPostAsync(1, 5_000, 4_000, 12, _deadline.Token))).Message);
        Assert.Equal("값이 맞지 않습니다", (await Act(a, () => a.AuctionPostAsync(1, 0, 0, 12, _deadline.Token))).Message);
        Assert.Equal("올릴 수 없는 물건입니다", (await Act(a, () => a.AuctionPostAsync(99, Start, 0, 12, _deadline.Token))).Message);
        Assert.Equal("보증금이 모자랍니다", (await Act(poor, () => poor.AuctionPostAsync(1, Start, 0, 12, _deadline.Token))).Message);
        Assert.Equal("이미 끝난 경매입니다", (await Act(a, () => a.AuctionBuyoutAsync(424_242, _deadline.Token))).Message);
        Assert.Equal("받을 것이 없습니다", (await Act(a, () => a.AuctionTakeAsync(0, _deadline.Token))).Message);

        // 0.3초 안에 둘 — 둘째는 「잠시 뒤에」.
        await Task.Delay(350, _deadline.Token);
        int seen = a.AuctionDoneCount;
        await a.AuctionClaimsAsync(0, _deadline.Token);
        await a.AuctionClaimsAsync(0, _deadline.Token);
        await Until(() => a.AuctionDoneCount > seen && a.AuctionDone!.Message == "잠시 뒤에 다시 하십시오", $"마지막 답: {a.AuctionDone}");

        Assert.Contains(a.Pack, item => item.Name == Sword);
        Assert.Equal(100_000, a.Vitals!.Gold);
        Assert.Empty(Book(server)["Listings"]!.AsArray());
    }

    [Fact]
    public async Task Browsing_pages_sorts_and_filters()
    {
        using IsolatedHadesServer server = Ready(("pagesell", 1_000_000, 13), ("pagesel2", 1_000_000, 0), ("pageview", 0, 0));
        Give(server, "pagesel2", 1, "가죽방패", 12);
        WorldClient one = Pump(await Login(server, "pagesell"));
        WorldClient two = Pump(await Login(server, "pagesel2"));
        WorldClient view = Pump(await Login(server, "pageview"));
        await Until(() => one.Pack.Count == 13 && two.Pack.Count == 12 && view.Vitals is not null, "물건이 오지 않았습니다.");

        for (int slot = 1; slot <= 13; slot++)
        {
            byte at = (byte)slot;
            uint price = (uint)(1_000 + (slot * 10));
            Assert.True((await Act(one, () => one.AuctionPostAsync(at, price, 0, 12, _deadline.Token))).Ok);
        }

        for (int slot = 1; slot <= 12; slot++)
        {
            byte at = (byte)slot;
            uint price = (uint)(2_000 - slot);
            Assert.True((await Act(two, () => two.AuctionPostAsync(at, price, 0, 24, _deadline.Token))).Ok);
        }

        AuctionPage first = await View(view, () => view.AuctionBrowseAsync(0, 1, 0, string.Empty, _deadline.Token));
        Assert.Equal((0, 2, 20), (first.Page, first.Pages, first.Rows.Count));
        Assert.Equal(first.Rows.Select(row => row.Price).Order(), first.Rows.Select(row => row.Price));
        AuctionPage second = await View(view, () => view.AuctionBrowseAsync(0, 1, 1, string.Empty, _deadline.Token));
        Assert.Equal(5, second.Rows.Count);
        Assert.True(second.Rows.Min(row => row.Price) >= first.Rows.Max(row => row.Price));

        AuctionPage weapons = await View(view, () => view.AuctionBrowseAsync(1, 0, 0, string.Empty, _deadline.Token));
        Assert.Equal(13, weapons.Rows.Count);
        Assert.All(weapons.Rows, row => Assert.Contains(Sword, row.Name));
        AuctionPage shields = await View(view, () => view.AuctionBrowseAsync(0, 0, 0, "방패", _deadline.Token));
        Assert.Equal(12, shields.Rows.Count);
        AuctionPage armour = await View(view, () => view.AuctionBrowseAsync(2, 0, 0, string.Empty, _deadline.Token));
        Assert.Equal(12, armour.Rows.Count);
    }

    /// <summary>
    /// 확인 사진 — 격리 서버에 다섯 가지를 올려 두고, 실제 앱(사는 이)이 들어가 「경매장」 창을 열면(<c>--auction</c>) 찾기 목록이 선다.
    /// <c>LOD_AUCTION_SHOT</c> 에 png 경로를 줄 때만 돈다(<c>LOD_AUCTION_ORIENT</c> = portrait|landscape).
    /// </summary>
    [Fact]
    public async Task Photograph_the_auction_browse()
    {
        if (Environment.GetEnvironmentVariable("LOD_AUCTION_SHOT") is not { Length: > 0 } shot)
        {
            return;
        }

        (string Name, int Stacks, uint Start, uint Buyout, byte Hours)[] goods =
        [
            (Sword, 1, 1_000, 10_000, 12), ("가죽방패", 1, 2_500, 0, 24), ("대지의목걸이", 1, 8_000, 15_000, 48),
            ("산호반지", 1, 1_800, 3_000, 12), ("상급체력포션", 20, 400, 900, 24),
        ];
        using IsolatedHadesServer server = Ready(("aucshop", 1_000_000, 0), ("aucview", 50_000, 0));
        for (int at = 0; at < goods.Length; at++)
        {
            Give(server, "aucshop", at + 1, goods[at].Name, 1, goods[at].Stacks);
        }

        WorldClient seller = Pump(await Login(server, "aucshop"));
        await Until(() => seller.Pack.Count == goods.Length, $"올릴 물건이 오지 않았습니다: {seller.Pack.Count}");
        for (int at = 0; at < goods.Length; at++)
        {
            var (name, _, price, buyout, hours) = goods[at];
            byte slot = (byte)(at + 1);
            AuctionDone posted = await Act(seller, () => seller.AuctionPostAsync(slot, price, buyout, hours, _deadline.Token));
            Assert.True(posted.Ok, $"{name}: {posted.Message}");
        }

        File.Delete(shot);
        string orient = Environment.GetEnvironmentVariable("LOD_AUCTION_ORIENT") ?? "portrait";
        System.Diagnostics.ProcessStartInfo start = new(Path.Combine(HadesWorkspace.RepositoryRoot, "scripts", "godot.sh"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
                 {
                     "--position", "-3000,-3000", "--audio-driver", "Dummy", "--",
                     "--server", $"127.0.0.1:{server.LoginPort}", "--login", $"aucview:{LoginFlow.SyntheticSecret}",
                     "--orient", orient, "--size", orient == "portrait" ? "360x780" : "800x360",
                     "--auction", "--shot", shot, "--shot-after", "14"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using System.Diagnostics.Process app = System.Diagnostics.Process.Start(start)!;
        Task<string> said = app.StandardOutput.ReadToEndAsync();
        _ = app.StandardError.ReadToEndAsync();
        await app.WaitForExitAsync(_deadline.Token);

        Assert.True(File.Exists(shot), "사진이 남지 않았습니다.");
        Assert.Contains("GREYBOX_AUCTION", await said, StringComparison.Ordinal);
    }

    // ---- 도우미 ----

    private IsolatedHadesServer Ready(params (string Who, int Gold, int Swords)[] people)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));
        foreach (var (who, gold, swords) in people)
        {
            LoginFlow.TryCreateAccount(server, who);
            CompanionCallTests.Edit(server, who, saved => saved["GoldPoints"] = gold);
            Give(server, who, 1, Sword, swords);
        }

        return server;
    }

    /// <summary>가방 <paramref name="from" /> 칸부터 <paramref name="count" />칸에 물건을 심는다. 그림 번호는 템플릿 것(실제 물건처럼).</summary>
    private static void Give(IsolatedHadesServer server, string who, int from, string name, int count, int stacks = 1)
    {
        string template = File.ReadAllText(Path.Combine(server.ContentLocation, "templates", "items", $"{name}.json"));
        int image = (int)JsonNode.Parse(template.TrimStart('\uFEFF'))!["DisplayImage"]!;
        CompanionCallTests.Edit(server, who, saved =>
        {
            for (int slot = from; slot < from + count; slot++)
            {
                saved["Inventory"]!["Items"]![slot.ToString()] = new JsonObject
                {
                    ["Template"] = new JsonObject { ["Name"] = name }, ["Slot"] = slot, ["Stacks"] = stacks, ["Durability"] = 100, ["DisplayImage"] = image,
                };
            }
        });
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
