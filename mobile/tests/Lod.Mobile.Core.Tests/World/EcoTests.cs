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
        20263 21 포테의숲1존
        20264 21 포테의숲2존
        """);

    [Fact]
    public void Picks_the_highest_ground_not_above_my_level_and_spreads_bots()
    {
        Assert.Equal(20022, EcoGrounds.Pick(Grounds, 10, _ => 0, 4, [])!.Map);
        Assert.Equal(20263, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [])!.Map);
        Assert.Equal(20264, EcoGrounds.Pick(Grounds, 30, map => map == 20263 ? 1 : 0, 4, [])!.Map);
        Assert.Equal(20022, EcoGrounds.Pick(Grounds, 30, map => map is 20263 or 20264 ? 4 : 0, 4, [])!.Map);
        Assert.Equal(20264, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [20263])!.Map);
    }

    [Fact]
    public void Steps_down_a_tier_after_dying_too_often_and_never_below_the_first()
    {
        Assert.Equal(20022, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [], lower: 1)!.Map);
        Assert.Equal(1, EcoGrounds.Pick(Grounds, 30, _ => 0, 4, [], lower: 9)!.Level);
        // 갈 수 있는 층이 다 차면 제 층에서 봇이 가장 적은 맵으로 넘친다(새 봇 30개가 첫 층 16자리에 몰려도 마을에서 놀지 않게).
        Assert.Equal(20264, EcoGrounds.Pick(Grounds, 30, map => map == 20263 ? 6 : 5, 4, [])!.Map);
        // 피할 맵(사람 있음)은 넘칠 때도 뒤로.
        Assert.Equal(20015, EcoGrounds.Pick(Grounds, 1, map => map == 20015 ? 5 : 4, 4, [20016])!.Map);
        Assert.Null(EcoGrounds.Pick([], 30, _ => 0, 4, []));
        // 가장 낮은 사냥터보다 낮은 레벨(새 캐릭터)은 가장 낮은 층으로.
        Assert.Equal(1, EcoGrounds.Pick(Grounds, 0, _ => 0, 4, [])!.Level);
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
        Assert.Null(EcoShopping.ToFree(Gear(7, ac: -2), one));

        // 두 손이 다 차면 약한 쪽보다 좋아야 산다 — 오른손 반지는 자리를 8 로 알려 오기도 한다. 끼기 전에 약한 쪽을 벗는다.
        WornItem[] two = [.. one, new(8, 1, "세토아의사파이어반지", "세토아의사파이어반지", 0, 0, Gear(8, ac: -5))];
        Assert.Empty(EcoShopping.GearToBuy(shop, two, Warrior, 1, 20, budget: 5000));
        Assert.Equal([new EcoBuy("자수정반지", 1)],
            EcoShopping.GearToBuy([Offer("자수정반지", 7000, Gear(7, ac: -3))], two, Warrior, 1, 20, budget: 9000));
        Assert.Equal(7, EcoShopping.ToFree(Gear(7, ac: -3), two));
    }

    [Fact]
    public void An_elemental_belt_or_necklace_comes_before_a_plain_one()
    {
        // 공격 속성은 목걸이, 방어 속성은 벨트(사용자 10-07 「방어 속성을 뭐라도 끼고 있는건 중요하지 바다의금벨트 같은거」).
        WornItem[] worn = [new(11, 1, "금벨트", "금벨트", 0, 0, Gear(11, ac: -4))];
        DialogueGoods[] shop = [Offer("바다의벨트", 10000, Gear(11) with { Defense = 2 }), Offer("벨트", 2000, Gear(11, ac: -2))];
        Assert.Equal([new EcoBuy("바다의벨트", 1)], EcoShopping.GearToBuy(shop, worn, Warrior, 1, 20, budget: 20000));

        Assert.True(EcoShopping.Score(Gear(6) with { Offense = 4 }) > EcoShopping.Score(Gear(6, ac: -5)));
        // 벨트의 공격 속성 · 목걸이의 방어 속성은 서버가 보지 않는다.
        Assert.Equal(0, EcoShopping.Score(Gear(11) with { Offense = 4 }));
        Assert.Equal(0, EcoShopping.Score(Gear(6) with { Defense = 4 }));
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

