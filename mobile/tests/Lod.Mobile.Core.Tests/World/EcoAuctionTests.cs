using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>생태계 봇의 경매장 판단(설계 <c>autopilot/loot-auction/</c> FR-014·015) — 무엇을 올리고 무엇을 사나.</summary>
public sealed class EcoAuctionTests
{
    private const int Warrior = 1;
    private const int Monk = 5;

    private static ItemStats Gear(int place, int level = 1, int cls = 0, int dmgMax = 0, int ac = 0, long value = 1_600) =>
        new(ac, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, dmgMax, level, cls, 0, 1, 0, 0, value, place);

    private static InventoryItem Carried(int slot, string name, ItemStats? stats, int stacks = 1) => new(slot, 0, 0, name, stacks, 100, 100, stats);

    private static AuctionRow Listed(uint id, uint buyout, ItemStats? stats, byte flags = 0) => new(id, 0, 0, $"물건{id}", 1, 3, buyout / 2, buyout, flags, stats);

    [Fact]
    public void Posts_gear_it_will_not_wear_at_twice_and_four_times_the_merchant_price()
    {
        InventoryItem sword = Carried(1, "에페", Gear(1, value: 1_600));            // 상인 매입가 1,000
        InventoryItem better = Carried(2, "커틀라스", Gear(1, value: 8_000));       // 입을 것
        InventoryItem potion = Carried(3, "하급체력포션", null, stacks: 20);
        InventoryItem cheap = Carried(4, "나무막대", Gear(1, value: 0));            // 값이 없어 올리지 않는다
        InventoryItem odd = Carried(5, "시험방패", Gear(3));                        // 서버가 거절한 적 있음

        IReadOnlyList<EcoPost> posts = EcoAuction.ToPost([sword, better, potion, cheap, odd], wear: [better], active: 0, refused: ["시험방패"]);

        Assert.Equal(new EcoPost(sword, 2_000, 4_000), Assert.Single(posts));
    }

    [Fact]
    public void Posts_dear_items_with_prices_held_to_the_server_cap()
    {
        InventoryItem hat = Carried(1, "산타모자", Gear(4, value: 500_000_000));       // 상인가 3억 1,250만
        InventoryItem crown = Carried(2, "왕관", Gear(4, value: 4_000_000_000));        // 상인가 25억 — 값 상한 20억에 잘린다

        IReadOnlyList<EcoPost> posts = EcoAuction.ToPost([hat, crown], wear: [], active: 0, refused: []);

        Assert.Equal(new EcoPost(crown, 2_000_000_000, 2_000_000_000), posts[0]);
        Assert.Equal(new EcoPost(hat, 625_000_000, 1_250_000_000), posts[1]);
    }

    [Fact]
    public void Keeps_no_more_than_five_listings_and_posts_the_dearest_first()
    {
        InventoryItem[] pack = [.. Enumerable.Range(1, 4).Select(slot => Carried(slot, $"검{slot}", Gear(1, value: slot * 1_600)))];

        Assert.Equal([4, 3], EcoAuction.ToPost(pack, wear: [], active: 3, refused: []).Select(post => post.Item.Slot));
        Assert.Empty(EcoAuction.ToPost(pack, wear: [], active: 5, refused: []));
    }

    [Fact]
    public void Buys_one_better_fitting_piece_per_place_within_the_budget()
    {
        WornItem[] worn = [new(1, 1, "목검", "목검", 0, 0, Gear(1, dmgMax: 3)), new(2, 1, "옷", "옷", 0, 0, Gear(2, ac: -2))];
        AuctionRow[] rows =
        [
            Listed(1, 1_000, Gear(1, dmgMax: 9)),                       // 산다 — 무기, 더 세다
            Listed(2, 900, Gear(1, dmgMax: 6)),                         // 같은 부위 더 약한 것 — 하나만
            Listed(3, 500, Gear(1, dmgMax: 20), flags: 1),              // 내가 올린 것
            Listed(4, 0, Gear(1, dmgMax: 30)),                          // 즉시 구매가 없음
            Listed(5, 500, Gear(1, dmgMax: 40, cls: Monk)),             // 다른 직업
            Listed(6, 500, Gear(1, dmgMax: 40, level: 50)),             // 레벨이 모자람
            Listed(7, 2_000, Gear(2, ac: -1)),                          // 지금 옷보다 약함
            Listed(8, 4_000, Gear(2, ac: -9)),                          // 예산(금화 10,000 의 30% = 3,000)을 넘음
            Listed(9, 2_500, Gear(2, ac: -5)),                          // 산다 — 무기를 산 뒤 남은 9,000 의 30% = 2,700 안
        ];

        Assert.Equal([1u, 9u], EcoAuction.ToBuy(rows, worn, Warrior, level: 10, gold: 10_000).Select(bid => bid.Row.Id));
    }

    [Fact]
    public void Does_not_buy_what_the_server_refused_to_let_it_wear()
    {
        AuctionRow[] rows = [Listed(1, 1_000, Gear(1, dmgMax: 9)) with { Name = "드레스" }];

        Assert.Empty(EcoAuction.ToBuy(rows, [], Warrior, level: 10, gold: 10_000, refused: ["드레스"]));
    }

    [Fact]
    public void A_rich_bot_spends_no_more_than_the_cap_on_one_piece()
    {
        AuctionRow[] rows = [Listed(1, 1_000_001, Gear(1, dmgMax: 99)), Listed(2, 1_000_000, Gear(1, dmgMax: 50))];

        Assert.Equal(2u, Assert.Single(EcoAuction.ToBuy(rows, [], Warrior, level: 99, gold: 100_000_000)).Row.Id);
    }

    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    private static string Trade(long seq, string ev, string item, long gold, DateTime at) =>
        $$"""{"seq":{{seq}},"ev":"{{ev}}","who":"a","listing":{{seq}},"item":"{{item}}","gold":{{gold}},"at":"{{at:yyyy-MM-ddTHH:mm:ssZ}}"}""";

    /// <summary>
    /// 시세 = 최근 7일 낙찰(기간 끝 sold · 즉시 구매 buyout) 값의 이름별 중앙값 — 서버 AuctionHouse 가 쓰는 줄 그대로. 되돌린 조작(abort)·7일 넘은 것·
    /// 올림·입찰·유찰·반만 쓰인 줄은 뺀다. 짝수 개면 가운데 둘의 평균.
    /// </summary>
    [Fact]
    public void The_market_price_is_the_median_of_last_weeks_winning_prices_per_item()
    {
        string[] lines =
        [
            Trade(1, "buyout", "에페", 1_000, Now.AddDays(-1)),
            Trade(2, "sold", "에페", 3_000, Now.AddDays(-2)),
            Trade(3, "buyout", "에페", 2_000, Now.AddHours(-1)),
            Trade(4, "buyout", "에페", 900_000, Now.AddHours(-1)),     // 되돌림 — 뺀다
            """{"seq":4,"ev":"abort"}""",
            Trade(5, "sold", "에페", 50, Now.AddDays(-8)),             // 7일 넘음
            Trade(6, "post", "에페", 70_000, Now.AddHours(-1)),        // 올림은 거래가 아니다
            Trade(7, "bid", "에페", 70_000, Now.AddHours(-1)),
            Trade(8, "expired", "에페", 70_000, Now.AddHours(-1)),
            Trade(9, "sold", "칸의가죽장갑", 10_000, Now.AddDays(-3)),
            Trade(10, "buyout", "칸의가죽장갑", 13_000, Now.AddDays(-3)),
            """{"seq":11,"ev":"buyout","item":"반""",
            """{"seq":9,"ev":"commit"}""",
        ];

        IReadOnlyDictionary<string, long> market = EcoMarket.Medians(lines, Now);

        Assert.Equal(2, market.Count);
        Assert.Equal(2_000, market["에페"]);
        Assert.Equal(11_500, market["칸의가죽장갑"]);
    }

    /// <summary>봇 프로그램은 폴더(설정 AuctionFolder)를 5분에 한 번만 읽는다. 폴더가 없으면 빈 시세.</summary>
    [Fact]
    public void The_market_reads_the_server_folder_at_most_every_few_minutes()
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "events-2026-10-11.jsonl"), Trade(1, "buyout", "에페", 1_000, Now) + "\n");
            File.WriteAllText(Path.Combine(folder, "events-2026-10-01.jsonl"), Trade(2, "buyout", "투구", 1_000, Now) + "\n");   // 날짜가 창 밖 파일은 안 연다
            EcoMarket market = new(folder);

            Assert.Equal(new Dictionary<string, long> { ["에페"] = 1_000 }, market.Prices(Now));

            File.AppendAllText(Path.Combine(folder, "events-2026-10-11.jsonl"), Trade(3, "buyout", "에페", 3_000, Now) + "\n");
            Assert.Equal(1_000, market.Prices(Now.AddMinutes(4))["에페"]);
            Assert.Equal(2_000, market.Prices(Now.AddMinutes(5))["에페"]);

            Assert.Empty(new EcoMarket(null).Prices(Now));
            Assert.Empty(new EcoMarket(Path.Combine(folder, "없음")).Prices(Now));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Posts_at_the_market_price_when_there_is_one_and_the_merchant_multiple_otherwise()
    {
        InventoryItem sword = Carried(1, "에페", Gear(1, value: 1_600));       // 상인 매입가 1,000
        InventoryItem hat = Carried(2, "투구", Gear(4, value: 1_600));

        IReadOnlyList<EcoPost> posts = EcoAuction.ToPost([sword, hat], wear: [], active: 0, refused: [], market: new Dictionary<string, long> { ["에페"] = 6_000 });

        Assert.Equal(new EcoPost(sword, 5_400, 7_800), posts.Single(post => post.Item == sword));
        Assert.Equal(new EcoPost(hat, 2_000, 4_000), posts.Single(post => post.Item == hat));

        // 시세가 상인 매입가의 8배를 넘으면 8배(8,000)에서 멈춘다 — 봇끼리 130% 에 사고팔아 끝없이 오르지 않게.
        EcoPost capped = Assert.Single(EcoAuction.ToPost([sword], wear: [], active: 0, refused: [], market: new Dictionary<string, long> { ["에페"] = 100_000 }));
        Assert.Equal(new EcoPost(sword, 7_200, 10_400), capped);
    }

    /// <summary>
    /// 사는 값 — 시세 10,000 인 장갑. 점수 이득이 작은 봇(전사, 50레벨, 덱스 장갑 = 이득 1)은 시세 가까이(10,000)까지만 내 즉시 구매가 13,000 은 안 사고
    /// 시작가로 입찰하고, 이득이 큰 봇(99, 칸 = 이득 100)은 시세의 150% 까지라 즉시 구매한다. 금화 비율 상한은 그대로.
    /// </summary>
    [Fact]
    public void A_bot_that_gains_more_pays_more_over_the_market_price()
    {
        Dictionary<string, long> market = new() { ["장갑"] = 10_000 };
        ItemStats dex = Gear(9, ac: -1, value: 16_000) with { Dex = 1 };    // 상인가 10,000 — 8배 상한(80,000)에 안 걸린다
        ItemStats kan = Gear(9, ac: -1, value: 16_000) with { Mr = 10 };
        AuctionRow row = Listed(1, 13_000, dex) with { Name = "장갑", Price = 9_000 };
        WornItem[] two = [new(9, 1, "가죽장갑", "가죽장갑", 0, 0, Gear(9, ac: -1)), new(10, 1, "가죽장갑", "가죽장갑", 0, 0, Gear(9, ac: -1))];

        Assert.Equal(10_000, EcoAuction.Ceiling(row, two, Warrior, 50, market));
        Assert.Equal(15_000, EcoAuction.Ceiling(row with { Stats = kan }, two, Warrior, 99, market));
        Assert.Null(EcoAuction.Ceiling(row, two, Warrior, 50, new Dictionary<string, long>()));

        EcoBid small = Assert.Single(EcoAuction.ToBuy([row], two, Warrior, 50, gold: 1_000_000, market: market));
        Assert.Equal((9_000u, false), (small.Amount, small.Buyout));                // 시작가로 입찰

        EcoBid big = Assert.Single(EcoAuction.ToBuy([row with { Stats = kan }], two, Warrior, 99, gold: 1_000_000, market: market));
        Assert.Equal((13_000u, true), (big.Amount, big.Buyout));                   // 즉시 구매

        // 남이 입찰해 둔 것(값 9,600)은 5% 위(10,080)가 상한(10,000)을 넘어 안 낸다 · 이미 최고 입찰자면 기다린다 · 금화 30% 상한은 그대로.
        Assert.Empty(EcoAuction.ToBuy([row with { Price = 9_600, Flags = 4 }], two, Warrior, 50, gold: 1_000_000, market: market));
        Assert.Empty(EcoAuction.ToBuy([row with { Flags = 6 }], two, Warrior, 50, gold: 1_000_000, market: market));
        Assert.Empty(EcoAuction.ToBuy([row with { Stats = kan }], two, Warrior, 99, gold: 20_000, market: market));

        // 시세가 없으면 예전대로 즉시 구매만 — 즉시 구매가가 없는 경매는 안 산다.
        Assert.Empty(EcoAuction.ToBuy([row with { Buyout = 0 }], two, Warrior, 50, gold: 1_000_000));
    }

    /// <summary>리뷰 L2 — 살 때도 시세는 상인 매입가의 8배에서 멈춘다(상인가 1,000 인데 시세 10만 → 8,000 기준). 값이 없는 물건은 시세 그대로.</summary>
    [Fact]
    public void The_buying_ceiling_holds_the_market_price_to_eight_times_the_merchant_price()
    {
        Dictionary<string, long> market = new() { ["장갑"] = 100_000 };
        AuctionRow row = Listed(1, 13_000, Gear(9, ac: -1, value: 1_600) with { Dex = 1 }) with { Name = "장갑" };
        WornItem[] two = [new(9, 1, "가죽장갑", "가죽장갑", 0, 0, Gear(9, ac: -1)), new(10, 1, "가죽장갑", "가죽장갑", 0, 0, Gear(9, ac: -1))];

        Assert.Equal(8_000, EcoAuction.Ceiling(row, two, Warrior, 50, market));
        EcoBid bid = Assert.Single(EcoAuction.ToBuy([row], two, Warrior, 50, gold: 1_000_000, market: market));
        Assert.Equal((6_500u, false), (bid.Amount, bid.Buyout));                   // 즉시 구매가 13,000 > 8,000 — 시작가로 입찰
        Assert.Equal(100_000, EcoAuction.Ceiling(row with { Stats = row.Stats! with { Value = 0 } }, two, Warrior, 50, market));
    }

    /// <summary>
    /// 리뷰 M4 — 맨몸(죽은 뒤) 장보기는 물약 몫(reserve)을 먼저 떼고, 빈 부위 전부를 합쳐 남은 금화의 60% 한 몫으로 산다.
    /// 전에는 부위마다 남은 금화의 60% 를 곱해 셋째 부위까지 사며 거의 다 썼다.
    /// </summary>
    [Fact]
    public void A_bare_bot_keeps_potion_money_and_spends_sixty_percent_once_across_all_bare_places()
    {
        AuctionRow[] dear = [Listed(1, 3_000, Gear(1, dmgMax: 9)), Listed(2, 3_000, Gear(2, ac: -5)), Listed(3, 3_000, Gear(4, ac: -3))];
        // 10,000 − 물약 2,000 = 8,000, 그 60% = 4,800 — 3,000 하나만.
        Assert.Equal([1u], EcoAuction.ToBuy(dear, [], Warrior, level: 10, gold: 10_000, reserve: 2_000).Select(bid => bid.Row.Id));

        AuctionRow[] cheap = [Listed(1, 2_000, Gear(1, dmgMax: 9)), Listed(2, 2_000, Gear(2, ac: -5)), Listed(3, 2_000, Gear(4, ac: -3))];
        // 4,800 한 몫에서 2,000 둘(남은 몫 800) — 셋째는 못 산다.
        Assert.Equal([1u, 2u], EcoAuction.ToBuy(cheap, [], Warrior, level: 10, gold: 10_000, reserve: 2_000).Select(bid => bid.Row.Id));

        // 물약 몫을 떼면 남는 것이 없으면 사지 않는다.
        Assert.Empty(EcoAuction.ToBuy(cheap, [], Warrior, level: 10, gold: 2_500, reserve: 2_000));
    }

    /// <summary>
    /// 유찰돼 돌아온 것은 재접속(새 runner 가 host 에서 받음)·프로그램 재시작(새 host 가 파일에서 읽음)에도 다시 올리지 않는다
    /// (리뷰 2026-10-08 #9 — 전에는 runner 마다 잊어 보증금을 거듭 잃었다).
    /// </summary>
    [Fact]
    public void Expired_items_stay_unlisted_per_bot_across_reconnects_and_restarts()
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(folder, "eco-unlisted.json");
        InventoryItem sword = Carried(1, "에페", Gear(1, value: 1_600));
        try
        {
            EcoUnlisted host = new(path);
            Assert.True(host.Add("전사봇", ["에페"]));
            Assert.Equal(["에페"], host.Of("전사봇"));                       // 재접속 — 같은 host 에서 새 runner 가 받는 목록

            EcoUnlisted restarted = new(path);                              // 재시작 — 파일에서
            Assert.Equal(["에페"], restarted.Of("전사봇"));
            Assert.Empty(restarted.Of("도적봇"));
            Assert.False(File.Exists(path + ".tmp"));                       // 임시 파일에 써서 바꿔 넣었다

            Assert.Empty(EcoAuction.ToPost([sword], wear: [], active: 0, refused: restarted.Of("전사봇")));
            Assert.Single(EcoAuction.ToPost([sword], wear: [], active: 0, refused: restarted.Of("도적봇")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData(null)]          // 파일 없음
    [InlineData("{\"전사봇\": [")] // 쓰다 만 파일
    [InlineData("[1, 2]")]       // 모양이 다름
    [InlineData("{\"전사봇\": null}")]
    public void A_missing_or_broken_unlisted_file_starts_empty(string? text)
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        string path = Path.Combine(folder, "eco-unlisted.json");
        try
        {
            if (text is not null)
            {
                File.WriteAllText(path, text);
            }

            EcoUnlisted read = new(path);
            Assert.Empty(read.Of("전사봇"));
            Assert.True(read.Add("전사봇", ["에페"]));                      // 깨진 파일은 다음 기록이 덮는다
            Assert.Equal(["에페"], new EcoUnlisted(path).Of("전사봇"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
