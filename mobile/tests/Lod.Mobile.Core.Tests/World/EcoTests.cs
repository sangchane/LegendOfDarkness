using System.Text.Json;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>생태계 봇의 판단(설계 <c>autopilot/eco-bots/</c>) — 사냥터 고르기·장보기·상태 넘기기·사건 한 줄.</summary>
public sealed class EcoTests
{
    private const int Warrior = 1;
    private const int Monk = 5;

    private static readonly IReadOnlyList<EcoGround> Grounds = EcoGrounds.Read("""
        # 맵 적정레벨 이름
        20015 1 우드랜드1-1
        20016 1 우드랜드1-2
        20022 6 우드랜드2-1
        20030 15 우드랜드3-1
        20263 21 포테의숲1존
        20264 21 포테의숲2존
        """);

    [Fact]
    public void Picks_the_highest_ground_not_above_my_level_and_spreads_bots()
    {
        Assert.Equal(20022, EcoGrounds.Pick(Grounds, 10, _ => 0, 4, [])!.Map);
        Assert.Equal(20263, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [])!.Map);
        Assert.Equal(20264, EcoGrounds.Pick(Grounds, 30, map => map == 20263 ? 1 : 0, 4, [])!.Map);
        Assert.Equal(20030, EcoGrounds.Pick(Grounds, 30, map => map is 20263 or 20264 ? 4 : 0, 4, [])!.Map);
        Assert.Equal(20264, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [20263])!.Map);
    }

    [Fact]
    public void Steps_down_a_tier_after_dying_too_often_and_never_below_the_first()
    {
        Assert.Equal(20030, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [], lower: 1)!.Map);
        Assert.Equal(15, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [], lower: 9)!.Level); // 서클 2(11~40) 맨 아래 층까지
        Assert.Equal(1, EcoGrounds.Pick(Grounds, 10, _ => 0, 4, [], lower: 9)!.Level);
        // 갈 수 있는 층이 다 차면 제 층에서 봇이 가장 적은 맵으로 넘친다(새 봇 30개가 첫 층 16자리에 몰려도 마을에서 놀지 않게).
        Assert.Equal(20264, EcoGrounds.Pick(Grounds, 30, map => map == 20263 ? 6 : 5, 4, [])!.Map);
        // 피할 맵(사람 있음)은 넘칠 때도 뒤로.
        Assert.Equal(20015, EcoGrounds.Pick(Grounds, 1, map => map == 20015 ? 5 : 4, 4, [20016])!.Map);
        Assert.Null(EcoGrounds.Pick([], 30, _ => 0, 4, []));
        // 가장 낮은 사냥터보다 낮은 레벨(새 캐릭터)은 가장 낮은 층으로.
        Assert.Equal(1, EcoGrounds.Pick(Grounds, 0, _ => 0, 4, [])!.Level);
    }

    /// <summary>
    /// 제 서클 밑으로는 안 간다 — 99 파티가 99 사냥터가 차서·죽어서 아벨해안(67)까지 내려왔다(사용자 2026-10-09). 99 층이 다 차면
    /// 99 층에서 봇이 가장 적은 맵으로 넘친다. 서클 안에 층이 없으면(41~45 레벨인데 그 사이 사냥터가 없을 때) 예전처럼 아래 층.
    /// </summary>
    [Fact]
    public void Never_hunts_below_its_own_circle()
    {
        IReadOnlyList<EcoGround> grounds = EcoGrounds.Read("""
            20589 67 아벨해안3-A
            20861 99 서의우드랜드16-1
            20862 99 서의우드랜드17-1
            """);

        Assert.Equal(99, EcoGrounds.Pick(grounds, 99, map => map == 20589 ? 0 : 1, 1, [])!.Level);
        Assert.Equal(99, EcoGrounds.Pick(grounds, 99, _ => 0, 4, [], lower: 5)!.Level);
        Assert.Equal(20862, EcoGrounds.Pick(grounds, 99, _ => 0, 4, [20861])!.Map);
        Assert.Equal(67, EcoGrounds.Pick(grounds, 80, _ => 0, 4, [])!.Level); // 71~98 층이 없다
        Assert.Equal(99, EcoGrounds.CircleFloor(99));
        Assert.Equal(71, EcoGrounds.CircleFloor(98));
        Assert.Equal(11, EcoGrounds.CircleFloor(40));
        Assert.Equal(1, EcoGrounds.CircleFloor(10));
    }

    private static ItemStats Gear(int place, int level = 1, int cls = 0, int ac = 0, int dmgMax = 0, int str = 0) =>
        new(ac, 0, 0, str, 0, 0, 0, 0, 0, 0, 0, 0, dmgMax, level, cls, 0, 1, 0, 0, 100, place);

    private static DialogueGoods Offer(string name, uint price, ItemStats? stats = null, byte gender = 255) =>
        new(1, 0, price, name, Gender: gender, Stats: stats);

    [Fact]
    public void Buys_the_strongest_potion_it_can_stock_up_on()
    {
        DialogueGoods[] shop = [Offer("최하급체력포션", 10), Offer("하급체력포션", 40), Offer("중급체력포션", 200)];

        Assert.Equal(new EcoBuy("하급체력포션", 30), EcoShopping.PotionsToBuy(shop, level: 30, gold: 2000, have: 0, stock: 30));
        Assert.Equal(new EcoBuy("하급체력포션", 20), EcoShopping.PotionsToBuy(shop, level: 30, gold: 2000, have: 10, stock: 30));
        // 모자라면 가장 싼 것으로 살 수 있는 만큼.
        Assert.Equal(new EcoBuy("최하급체력포션", 5), EcoShopping.PotionsToBuy(shop, level: 30, gold: 50, have: 0, stock: 30));
        Assert.Null(EcoShopping.PotionsToBuy(shop, level: 30, gold: 5, have: 0, stock: 30));
        Assert.Null(EcoShopping.PotionsToBuy(shop, level: 30, gold: 5000, have: 30, stock: 30));
    }

    [Fact]
    public void Buys_one_better_piece_per_place_that_fits_class_gender_level_and_budget()
    {
        WornItem[] worn = [new(1, 1, "목검", "목검", 0, 0, Gear(1, dmgMax: 3))];
        DialogueGoods[] shop =
        [
            Offer("커틀라스", 300, Gear(1, level: 11, cls: Warrior, dmgMax: 12)),
            Offer("그라디우스", 500, Gear(1, level: 41, cls: Warrior, dmgMax: 30)), // 레벨이 모자람
            Offer("견습자의글러브", 100, Gear(1, level: 1, cls: Monk, dmgMax: 20)), // 다른 직업
            Offer("에페", 50, Gear(1, level: 1, cls: 0, dmgMax: 2)),               // 지금 것보다 약함
            Offer("가죽갑옷", 200, Gear(2, level: 1, ac: -5)),
            Offer("드레스", 10, Gear(2, level: 1, ac: -9), gender: 2),             // 다른 성별
            Offer("철갑옷", 9000, Gear(2, level: 1, ac: -20)),                     // 비쌈
        ];

        IReadOnlyList<EcoBuy> buys = EcoShopping.GearToBuy(shop, worn, Warrior, gender: 1, level: 20, budget: 600);

        Assert.Equal([new EcoBuy("커틀라스", 1), new EcoBuy("가죽갑옷", 1)], buys);
        Assert.Equal([new EcoBuy("커틀라스", 1)], EcoShopping.GearToBuy(shop, worn, Warrior, 1, 20, budget: 300));
    }

    [Fact]
    public void Wears_better_loot_and_sells_the_rest_but_keeps_potions()
    {
        InventoryItem[] pack =
        [
            new(1, 1, 0, "하급체력포션", 12, 0, 0),
            new(2, 1, 0, "커틀라스", 1, 0, 0, Gear(1, level: 11, cls: Warrior, dmgMax: 12)),
            new(3, 1, 0, "늑대가죽", 3, 0, 0),
            new(4, 1, 0, "견습자의글러브", 1, 0, 0, Gear(1, level: 1, cls: Monk, dmgMax: 20)),
        ];
        WornItem[] worn = [new(1, 1, "목검", "목검", 0, 0, Gear(1, dmgMax: 3))];

        Assert.Equal([2], EcoShopping.ToWear(pack, worn, Warrior, level: 20, refused: []).Select(item => item.Slot));
        Assert.Empty(EcoShopping.ToWear(pack, worn, Warrior, level: 20, refused: ["커틀라스"]));
        Assert.Equal([2, 3, 4], EcoShopping.ToSell(pack, keep: []).Select(item => item.Slot));
        Assert.Equal([3, 4], EcoShopping.ToSell(pack, keep: [2]).Select(item => item.Slot));
        Assert.Equal(12, EcoShopping.Potions(pack));
        Assert.Equal("하급체력포션", EcoShopping.BestPotion(pack));
    }

    [Fact]
    public void Rings_fill_both_hands_and_a_new_one_must_beat_the_weaker()
    {
        // 반지는 두 손(7·8) — 서버는 빈 손에 끼운다(Generic.cs). 클라우드 10-07: 사제봇9 는 홍옥반지 하나로 오른손이 비어 있었다.
        DialogueGoods[] shop = [Offer("홍옥반지", 1000, Gear(7, ac: -2)), Offer("산호반지", 3000, Gear(7, ac: -1))];
        WornItem[] one = [new(7, 1, "홍옥반지", "홍옥반지", 0, 0, Gear(7, ac: -2))];
        Assert.Equal([new EcoBuy("홍옥반지", 1)], EcoShopping.GearToBuy(shop, one, Warrior, 1, 20, budget: 5000));

        InventoryItem[] pack = [new(3, 1, 0, "홍옥반지", 1, 0, 0, Gear(7, ac: -2))];
        Assert.Equal([3], EcoShopping.ToWear(pack, one, Warrior, 20, refused: []).Select(item => item.Slot));
        Assert.Null(EcoShopping.ToFree(Gear(7, ac: -2), one, Warrior, 20));

        // 두 손이 다 차면 약한 쪽보다 좋아야 산다 — 오른손 반지는 자리를 8 로 알려 오기도 한다. 끼기 전에 약한 쪽을 벗는다.
        WornItem[] two = [.. one, new(8, 1, "세토아의사파이어반지", "세토아의사파이어반지", 0, 0, Gear(8, ac: -5))];
        Assert.Empty(EcoShopping.GearToBuy(shop, two, Warrior, 1, 20, budget: 5000));
        Assert.Equal([new EcoBuy("자수정반지", 1)],
            EcoShopping.GearToBuy([Offer("자수정반지", 7000, Gear(7, ac: -3))], two, Warrior, 1, 20, budget: 9000));
        Assert.Equal(7, EcoShopping.ToFree(Gear(7, ac: -3), two, Warrior, 20));
    }

    [Fact]
    public void An_elemental_belt_or_necklace_comes_before_a_plain_one()
    {
        // 공격 속성은 목걸이, 방어 속성은 벨트(사용자 10-07 「방어 속성을 뭐라도 끼고 있는건 중요하지 바다의금벨트 같은거」).
        WornItem[] worn = [new(11, 1, "금벨트", "금벨트", 0, 0, Gear(11, ac: -4))];
        DialogueGoods[] shop = [Offer("바다의벨트", 10000, Gear(11) with { Defense = 2 }), Offer("벨트", 2000, Gear(11, ac: -2))];
        Assert.Equal([new EcoBuy("바다의벨트", 1)], EcoShopping.GearToBuy(shop, worn, Warrior, 1, 20, budget: 20000));

        Assert.True(EcoShopping.Score(Gear(6) with { Offense = 4 }, Warrior, 20) > EcoShopping.Score(Gear(6, ac: -5), Warrior, 20));
        // 벨트의 공격 속성 · 목걸이의 방어 속성은 서버가 보지 않는다.
        Assert.Equal(0, EcoShopping.Score(Gear(11) with { Offense = 4 }, Warrior, 20));
        Assert.Equal(0, EcoShopping.Score(Gear(6) with { Defense = 4 }, Warrior, 20));
    }

    [Fact]
    public void A_shield_that_knocked_off_a_two_handed_weapon_is_not_bought_again()
    {
        // 두손 무기를 들면 방패 칸이 늘 비어 「빈 칸이니 좋다」로 보인다 — 서버가 벗긴 방패(refused)는 다시 사지 않는다(10-08 클라우드: 한 시간에 600번 사고 바꿔 낌).
        WornItem[] worn = [new(1, 1, "투핸드크레이모어화", "투핸드크레이모어화", 0, 0, Gear(1, level: 71, cls: Warrior, dmgMax: 90))];
        DialogueGoods[] shop = [Offer("철방패", 10000, Gear(3, ac: -3))];

        Assert.Equal([new EcoBuy("철방패", 1)], EcoShopping.GearToBuy(shop, worn, Warrior, 1, 80, budget: 20000));
        Assert.Empty(EcoShopping.GearToBuy(shop, worn, Warrior, 1, 80, budget: 20000, refused: ["철방패"]));
    }

    // 신 이름 접두사(가죽장갑 바탕, 무장 1) — 세토아 힘 · 메투스 콘 · 셔스 덱스 · 로오 인트 · 이아 위즈 · 칸 마법방어 10 · 체력 100.
    private static readonly ItemStats Glove = Gear(9, level: 11, ac: -1);

    private static readonly Dictionary<string, ItemStats> Prefixed = new()
    {
        ["세토아"] = Glove with { Str = 1 },
        ["메투스"] = Glove with { Con = 1 },
        ["셔스"] = Glove with { Dex = 1 },
        ["로오"] = Glove with { Int = 1 },
        ["이아"] = Glove with { Wis = 1 },
        ["칸"] = Glove with { Mr = 10 },
        ["체력"] = Glove with { Hp = 100 },
        ["바탕"] = Glove,
    };

    /// <summary>점수가 높은 차례 — 같은 점수는 한 묶음(이름 순).</summary>
    private static string[][] Ranked(int path, int level) =>
    [
        .. Prefixed.GroupBy(one => EcoShopping.Score(one.Value, path, level))
            .OrderByDescending(group => group.Key)
            .Select(group => group.Select(one => one.Key).Order(StringComparer.Ordinal).ToArray()),
    ];

    [Theory]
    [InlineData(1, "메투스,세토아,칸", "체력", "로오,셔스,이아")]   // 전사
    [InlineData(5, "메투스,세토아,칸", "체력", "로오,셔스,이아")]   // 무도가
    [InlineData(2, "세토아,셔스,칸", "체력", "로오,메투스,이아")]   // 도적
    [InlineData(4, "로오,이아,칸", "체력", "메투스,세토아,셔스")]   // 성직자
    [InlineData(3, "로오,이아,칸", "체력", "메투스,세토아,셔스")]   // 마법사
    public void A_growing_class_puts_its_own_prefixes_first_and_any_prefix_before_plain_gear(int path, string first, string second, string third)
    {
        // 사용자 2026-10-11 「무도가랑 전사는 세토아, 메투스, 칸 … 도적은 세토아, 셔스, 칸 … 성직자랑 마법사는 성장할때는 로오, 이아, 칸」.
        string[][] ranked = Ranked(path, level: 50);

        Assert.Equal([first.Split(','), [second], third.Split(','), ["바탕"]], ranked);

        // 접두사 하나가 한 서클 위 기본 장갑(동장갑 무장 2)보다도 앞선다.
        Assert.True(EcoShopping.Score(Prefixed["체력"], path, 50) > EcoShopping.Score(Gear(9, level: 41, ac: -2), path, 50));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void At_99_every_class_puts_magic_resist_health_500_and_armour_before_its_own_stats(int path)
    {
        // 사용자 2026-10-11 「99가 되면 모든 직업이 칸이나 체력이 500 이상 … 혹은 무장이 같은 부위보다 방어력을 많이 높여주는 아이템을 우선순위로」.
        ItemStats gold = Gear(9, level: 1, ac: -4);
        ItemStats own = path is 3 or 4 ? gold with { Int = 1 } : gold with { Str = 1 };   // 제 직업 주 능력치 접두사(로오의·세토아의)
        int Score(ItemStats stats) => EcoShopping.Score(stats, path, 99);

        Assert.True(Score(gold with { Mr = 10 }) > Score(own));
        Assert.True(Score(gold with { Hp = 500 }) > Score(own));
        Assert.True(Score(gold with { Ac = -5 }) > Score(own));
        Assert.True(Score(own) > Score(gold));

        // 98 까지는 거꾸로 — 주 능력치가 무장 1 보다 앞선다.
        Assert.True(EcoShopping.Score(own, path, 98) > EcoShopping.Score(gold with { Ac = -5 }, path, 98));

        // 가게에서도 그대로 고른다.
        DialogueGoods[] shop = [Offer("접두사금장갑", 1000, own), Offer("더단단한장갑", 1000, gold with { Ac = -5 })];
        Assert.Equal([new EcoBuy("더단단한장갑", 1)], EcoShopping.GearToBuy(shop, [], path, 1, 99, budget: 1000));
        Assert.Equal([new EcoBuy("접두사금장갑", 1)], EcoShopping.GearToBuy(shop, [], path, 1, 98, budget: 1000));
    }

    /// <summary>
    /// 죽은 뒤 맨몸(사망 벌칙: 접두사 장비는 사라지고 나머지는 바닥 — 봇이 주워 온다)에서 가방 → 경매 → 가게 차례로 부위를 다 채운다(EcoRunner.Shop 과 같은 차례).
    /// 금화가 적어 30% 상한이면 경매의 접두사 장갑(600)을 못 사지만, 빈 부위는 60% 까지 낸다.
    /// </summary>
    [Fact]
    public void A_bare_bot_after_death_fills_every_place_from_its_pack_the_auction_and_the_shop()
    {
        List<WornItem> worn = [];
        long gold = 1_000;
        void Wear(string name, ItemStats stats) => worn.Add(new WornItem(stats.Place, 0, name, name, 0, 0, stats));

        // 1) 주워 온 가방 — 접두사 없는 무기·갑옷.
        InventoryItem[] pack =
        [
            new(1, 1, 0, "커틀라스", 1, 0, 0, Gear(1, level: 11, cls: Warrior, dmgMax: 12)),
            new(2, 1, 0, "가죽갑옷", 1, 0, 0, Gear(2, level: 1, ac: -5)),
        ];
        foreach (InventoryItem item in EcoShopping.ToWear(pack, worn, Warrior, level: 20, refused: []))
        {
            Wear(item.Name, item.Stats!);
        }

        // 2) 경매 — 사라진 세토아의가죽장갑을 다시(시세 없음: 즉시 구매).
        Lod.Mobile.Core.Protocol.World.AuctionRow[] rows = [new(1, 0, 0, "세토아의가죽장갑", 1, 3, 300, 600, 0, Prefixed["세토아"])];
        foreach (EcoBid bid in EcoAuction.ToBuy(rows, worn, Warrior, level: 20, gold))
        {
            Assert.True(bid.Buyout);
            Wear(bid.Row.Name, bid.Row.Stats!);
            gold -= bid.Amount;
        }

        // 3) 가게 — 남은 금화로 빈 부위(반지 두 손·신발).
        DialogueGoods[] shop =
        [
            Offer("홍옥반지", 100, Gear(7, level: 11, ac: -1)),
            Offer("토끼신발", 150, Gear(13, level: 11, ac: -3)),
            Offer("가죽갑옷", 50, Gear(2, level: 1, ac: -5)),       // 이미 입었다
        ];
        for (int round = 0; round < 3; round++)
        {
            foreach (EcoBuy buy in EcoShopping.GearToBuy(shop, worn, Warrior, gender: 1, level: 20, gold))
            {
                DialogueGoods goods = shop.First(one => one.Name == buy.Name);
                Wear(goods.Name, goods.Stats!);
                gold -= goods.Price;
            }
        }

        Assert.Equal([1, 2, 7, 7, 9, 13], worn.Select(one => one.Slot).Order());
        Assert.Equal(1_000 - 600 - 100 - 100 - 150, gold);
        Assert.False(EcoShopping.Empty(7, worn));
    }

    private static EcoSight Sight(TimeSpan now, int potions = 20, int free = 100, bool stopped = false, TimeSpan? person = null,
        bool ghost = false, bool coma = false, int health = 100, TimeSpan? gained = null) =>
        new(now, coma, ghost, potions, free, stopped, person, health, gained);

    [Fact]
    public void Hunts_until_potions_bag_time_or_a_stop_sends_it_to_town()
    {
        TimeSpan t0 = TimeSpan.FromMinutes(1);
        EcoLife life = new();

        Assert.Equal(EcoAct.GoHunt, life.Next(Sight(t0)));
        life.Arrived(EcoPlace.Hunting, t0);
        Assert.Equal(EcoAct.Hunt, life.Next(Sight(t0)));
        Assert.Equal(EcoAct.GoTown, life.Next(Sight(t0, potions: Tuning.EcoPotionLow - 1)));
        Assert.Equal(EcoAct.GoTown, life.Next(Sight(t0, free: Tuning.EcoBagLow - 1)));
        Assert.Equal(EcoAct.GoTown, life.Next(Sight(t0 + Tuning.EcoTownEvery)));
        Assert.Equal(EcoAct.GoTown, life.Next(Sight(t0, stopped: true)));
        Assert.Equal(EcoAct.Hunt, life.Next(Sight(t0 + TimeSpan.FromSeconds(10), person: t0)));
        Assert.Equal(EcoAct.GoHunt, life.Next(Sight(t0 + Tuning.EcoYield, person: t0)));
    }

    [Fact]
    public void Moves_its_hunting_spot_when_no_exp_comes_for_a_while()
    {
        // 클라우드(10-07): 파티가 도착 자리 12칸 안을 비우고 60분 동안 서 있었다.
        TimeSpan t0 = TimeSpan.FromMinutes(1);
        EcoLife life = new();
        life.Arrived(EcoPlace.Hunting, t0);

        Assert.Equal(EcoAct.Hunt, life.Next(Sight(t0 + Tuning.EcoIdleMove - TimeSpan.FromSeconds(1))));
        Assert.Equal(EcoAct.GoHunt, life.Next(Sight(t0 + Tuning.EcoIdleMove)));
        Assert.Equal(EcoAct.Hunt, life.Next(Sight(t0 + Tuning.EcoIdleMove, gained: t0 + TimeSpan.FromSeconds(30))));
    }

    [Fact]
    public void Shops_once_rests_when_hurt_then_goes_back_and_does_not_loop_for_potions_it_cannot_buy()
    {
        TimeSpan t0 = TimeSpan.FromMinutes(1);
        EcoLife life = new();
        life.Arrived(EcoPlace.Town, t0);

        Assert.Equal(EcoAct.Shop, life.Next(Sight(t0, potions: 0)));
        life.Shopped(potionsNow: 0);
        Assert.Equal(EcoAct.Wait, life.Next(Sight(t0, potions: 0, health: Tuning.EcoRestPercent - 1)));
        Assert.Equal(EcoAct.GoHunt, life.Next(Sight(t0, potions: 0)));

        // 살 금화가 없어 물약 0 으로 나왔다 — 물약 때문에 바로 다시 마을로 가지 않는다.
        life.Arrived(EcoPlace.Hunting, t0);
        Assert.Equal(EcoAct.Hunt, life.Next(Sight(t0, potions: 0)));
    }

    [Fact]
    public void A_ghost_revives_and_dying_again_and_again_steps_the_ground_down()
    {
        TimeSpan t0 = TimeSpan.FromMinutes(1);
        EcoLife life = new();
        life.Arrived(EcoPlace.Hunting, t0);

        Assert.Equal(EcoAct.Wait, life.Next(Sight(t0, coma: true)));

        for (int death = 0; death < Tuning.EcoDeathLoop; death++)
        {
            Assert.Equal(0, life.Lower);
            Assert.Equal(EcoAct.Revive, life.Next(Sight(t0, ghost: true)));
            life.Revived();
            life.Arrived(EcoPlace.Hunting, t0);
        }

        Assert.Equal(1, life.Lower);

        // 제 발로 마을에 다녀와도 낮춘 채로, 레벨이 올라야 제 층으로.
        life.Arrived(EcoPlace.Town, t0);
        Assert.Equal(1, life.Lower);
        life.LeveledUp();
        Assert.Equal(0, life.Lower);
    }

    [Fact]
    public void An_event_line_is_json_with_numbers_as_numbers()
    {
        Vitals mine = Vitals.Unknown with { Level = 30, Experience = 1234, Gold = 99, Health = 50, MaximumHealth = 100 };
        string line = EcoLog.Line(new DateTime(2026, 10, 6, 3, 0, 0, DateTimeKind.Utc), "봇하나", Warrior, mine, 20015, new Tile(3, 4),
            "hunt", "kill", new { monster = "늑대", ms = 4200 });

        using JsonDocument read = JsonDocument.Parse(line);
        JsonElement root = read.RootElement;
        Assert.Equal(1, root.GetProperty("v").GetInt32());
        Assert.Equal("봇하나", root.GetProperty("bot").GetString());
        Assert.Equal(30, root.GetProperty("lvl").GetInt32());
        Assert.Equal(1234, root.GetProperty("exp").GetInt64());
        Assert.Equal(20015, root.GetProperty("map").GetInt32());
        Assert.Equal("kill", root.GetProperty("ev").GetString());
        Assert.Equal(4200, root.GetProperty("data").GetProperty("ms").GetInt32());
        Assert.DoesNotContain('\n', line);
    }

    [Fact]
    public void A_priest_takes_three_fighters_of_different_classes_closest_in_level()
    {
        IReadOnlyList<EcoParty> parties = EcoParties.Form(
        [
            new("사제봇1", EcoParties.Priest, 3),
            new("전사봇1", Warrior, 11),
            new("전사봇2", Warrior, 12),
            new("도적봇1", 2, 15),
            new("무도봇1", Monk, 16),
            new("무도봇2", Monk, 40),
        ], minLevel: 11);

        EcoParty party = Assert.Single(parties);
        Assert.Equal("사제봇1", party.Priest);
        Assert.Equal(["전사봇1", "도적봇1", "무도봇1"], party.Fighters);
        Assert.Equal("전사봇1", party.Leader);
    }

    [Fact]
    public void No_party_below_the_party_level_or_without_three_fighters()
    {
        Assert.Empty(EcoParties.Form([new("사제봇1", EcoParties.Priest, 1), new("전사봇1", Warrior, 30), new("도적봇1", 2, 30), new("무도봇1", Monk, 10)], 11));
        Assert.Empty(EcoParties.Form([new("전사봇1", Warrior, 30), new("도적봇1", 2, 30), new("무도봇1", Monk, 30)], 11));
    }

    [Fact]
    public void Each_priest_gets_its_own_level_band_and_fills_with_any_class_when_one_is_missing()
    {
        IReadOnlyList<EcoParty> parties = EcoParties.Form(
        [
            new("사제봇1", EcoParties.Priest, 1),
            new("사제봇2", EcoParties.Priest, 1),
            new("전사봇1", Warrior, 50),
            new("전사봇2", Warrior, 51),
            new("전사봇3", Warrior, 52),
            new("도적봇1", 2, 20),
            new("무도봇1", Monk, 21),
            new("전사봇4", Warrior, 22),
        ], 11);

        Assert.Equal(2, parties.Count);
        Assert.Equal(["도적봇1", "무도봇1", "전사봇4"], parties[0].Fighters);
        Assert.Equal(["전사봇1", "전사봇2", "전사봇3"], parties[1].Fighters);
    }
}

