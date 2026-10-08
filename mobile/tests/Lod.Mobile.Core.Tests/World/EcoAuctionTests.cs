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

        Assert.Equal([1u, 9u], EcoAuction.ToBuy(rows, worn, Warrior, level: 10, gold: 10_000).Select(row => row.Id));
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

        Assert.Equal(2u, Assert.Single(EcoAuction.ToBuy(rows, [], Warrior, level: 99, gold: 100_000_000)).Id);
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
