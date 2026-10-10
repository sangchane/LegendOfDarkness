using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace Lod.Mobile.Core.Automation;

/// <summary>생태계 봇이 갈 수 있는 사냥터 하나 — 맵과 적정 레벨(그 맵 괴물 50마리면 한 레벨, <c>eco-grounds.txt</c>).</summary>
public sealed record EcoGround(int Map, int Level, string Name);

/// <summary>레벨에 맞는 사냥터 고르기(설계 <c>autopilot/eco-bots/</c> FR-002).</summary>
public static class EcoGrounds
{
    /// <summary><c>eco-grounds.txt</c> — 줄마다 「맵 적정레벨 이름」, <c>#</c> 은 주석. 틀린 줄은 건너뛴다.</summary>
    public static IReadOnlyList<EcoGround> Read(string text) =>
    [
        .. text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries))
            .Where(part => part.Length == 3 && int.TryParse(part[0], out _) && int.TryParse(part[1], out _))
            .Select(part => new EcoGround(int.Parse(part[0]), int.Parse(part[1]), part[2])),
    ];

    /// <summary>
    /// 적정 레벨 ≤ 내 레벨 중 가장 높은 층(내 레벨이 가장 낮은 층보다 낮으면 그 층, <paramref name="lower" /> 만큼 아래 층, 제 서클 밑으로는 안 감), 그 층에서 봇이 가장 적은 맵.
    /// 봇이 <paramref name="perMap" /> 인 맵과 <paramref name="avoid" /> 는 빼고, 그 층이 다 차면 아래 층.
    /// 갈 수 있는 층이 다 차면(새 봇이 한꺼번에 첫 층에 몰릴 때) 제 층에서 봇이 가장 적은 맵으로 넘친다 — 마을에서 놀지 않게. 사냥터가 없으면 null.
    /// </summary>
    /// <summary>서클의 첫 레벨 — 1~10 · 11~40 · 41~70 · 71~98 · 99(<c>docs/item-prices-by-circle.md</c>).</summary>
    public static int CircleFloor(int level) => level >= 99 ? 99 : level >= 71 ? 71 : level >= 41 ? 41 : level >= 11 ? 11 : 1;

    public static EcoGround? Pick(
        IReadOnlyList<EcoGround> grounds, int level, Func<int, int> botsOn, int perMap, IReadOnlyCollection<int> avoid, int lower = 0)
    {
        // 가장 낮은 사냥터보다 레벨이 낮으면(새 캐릭터) 가장 낮은 층으로.
        int reach = grounds.Count == 0 ? level : Math.Max(level, grounds.Min(one => one.Level));
        int[] tiers = [.. grounds.Where(one => one.Level <= reach).Select(one => one.Level).Distinct().OrderByDescending(tier => tier)];

        // 제 서클 밑 층으로는 내려가지 않는다 — 99 파티가 99 사냥터가 차거나 죽어서 층을 낮추다 아벨해안(67)까지 내려왔다(사용자
        // 2026-10-09 「아벨인데 99장비입은 전사랑 도적이」). 서클 안에 층이 없을 때만 예전처럼 아래로.
        int[] mine = [.. tiers.Where(tier => tier >= CircleFloor(reach))];
        if (mine.Length > 0)
        {
            tiers = mine;
        }

        int[] open = [.. tiers.Skip(Math.Min(lower, Math.Max(0, tiers.Length - 1)))];

        return open
            .Select(tier => grounds
                .Where(one => one.Level == tier && !avoid.Contains(one.Map) && botsOn(one.Map) < perMap)
                .OrderBy(one => botsOn(one.Map))
                .FirstOrDefault())
            .FirstOrDefault(found => found is not null)
            ?? grounds
                .Where(one => open.Length > 0 && one.Level == open[0])
                .OrderBy(one => avoid.Contains(one.Map))
                .ThenBy(one => botsOn(one.Map))
                .FirstOrDefault();
    }
}

/// <summary>상점에서 살 것 한 줄.</summary>
public sealed record EcoBuy(string Name, int Quantity);

/// <summary>
/// 생태계 봇의 장보기(FR-006) — 물약은 <see cref="AutoPotion.Healing" /> 중 살 수 있는 가장 센 것, 장비는 부위마다 직업·성별·레벨이 맞고
/// 지금 것보다 좋은 것 하나. 「서클에 맞게」 = 물건 레벨 제한 ≤ 내 레벨(상점 꼬리 <see cref="ItemStats.Level" />).
/// </summary>
public static class EcoShopping
{
    public static bool IsHealing(string name) => AutoPotion.Healing.Any(potion => potion.Name == name);

    /// <summary>가방의 체력 물약 수.</summary>
    public static int Potions(IReadOnlyList<InventoryItem> pack) => pack.Where(item => IsHealing(item.Name)).Sum(item => item.Stacks);

    /// <summary>가방의 마력 물약 수 — 성직자 봇이 장보기를 정한다.</summary>
    public static int ManaPotions(IReadOnlyList<InventoryItem> pack) =>
        pack.Where(item => AutoPotion.Restoring.Any(potion => potion.Name == item.Name)).Sum(item => item.Stacks);

    /// <summary>가방에 있는 가장 센 체력 물약 이름, 없으면 null.</summary>
    public static string? BestPotion(IReadOnlyList<InventoryItem> pack) =>
        Enumerable.Reverse(AutoPotion.Healing).Select(potion => potion.Name).FirstOrDefault(name => pack.Any(item => item.Name == name));

    /// <summary>
    /// <paramref name="stock" /> 개까지 채울 물약 — 모자란 만큼을 다 살 수 있는 가장 센 것, 그런 게 없으면 가장 싼 것으로 살 수 있는 만큼. 살 게 없으면 null.
    /// </summary>
    /// <param name="kinds">살 물약 갈래(약한 것부터) — 없으면 체력 물약(<see cref="AutoPotion.Healing" />), 성직자는 마력 물약.</param>
    public static EcoBuy? PotionsToBuy(IReadOnlyList<DialogueGoods> goods, int level, long gold, int have, int stock, IReadOnlyList<Potion>? kinds = null)
    {
        int need = stock - have;
        DialogueGoods[] sold =
        [
            .. (kinds ?? AutoPotion.Healing)
                .Select(potion => goods.FirstOrDefault(one => one.Name == potion.Name && one.Price > 0 && (one.Stats?.Level ?? 0) <= level))
                .OfType<DialogueGoods>(),
        ];

        if (need <= 0 || sold.Length == 0)
        {
            return null;
        }

        if (Enumerable.Reverse(sold).FirstOrDefault(one => one.Price * need <= gold) is { } full)
        {
            return new EcoBuy(full.Name, need);
        }

        DialogueGoods cheapest = sold.MinBy(one => one.Price)!;
        int afford = (int)Math.Min(need, gold / cheapest.Price);
        return afford > 0 ? new EcoBuy(cheapest.Name, afford) : null;
    }

    /// <summary>
    /// 장비 점수 — 무기는 최대 공격력 + 공격 + 명중. 그 밖은 무장(AC 는 낮을수록 좋다) + 능력치 + 마법방어 + 체력/10 + 명중·공격, 능력치는 직업의 주 능력치
    /// (<see cref="Main" />)를 <see cref="Tuning.EcoScoreStat" /> 로, 마법방어 10 도 그만큼(사용자 2026-10-11). 99 는 모든 직업이 마법방어 · 체력 500 이상 ·
    /// 무장을 먼저(<see cref="Tuning.EcoScoreTop" />·<see cref="Tuning.EcoScoreAc99" />). 속성은 목걸이의 공격 속성과 벨트의 방어 속성만 서버가 본다
    /// (<c>GearElements</c>) — 그 둘에 <see cref="Tuning.EcoElementScore" /> 를 더한다.
    /// </summary>
    public static int Score(ItemStats stats, int path, int level)
    {
        if (stats.Place == 1)
        {
            return stats.DmgMax + stats.Dmg + stats.Hit;
        }

        Stat[] main = Main(path);
        int Weigh(Stat which, int value) => value * (main.Contains(which) ? Tuning.EcoScoreStat : 1);
        bool top = level >= 99;

        return -stats.Ac * (top ? Tuning.EcoScoreAc99 : 1)
               + Weigh(Stat.Str, stats.Str) + Weigh(Stat.Con, stats.Con) + Weigh(Stat.Dex, stats.Dex) + Weigh(Stat.Int, stats.Int) + Weigh(Stat.Wis, stats.Wis)
               + (stats.Mr * (top ? Tuning.EcoScoreTop : Tuning.EcoScoreStat) / 10)
               + (stats.Hp / 10) + (top && stats.Hp >= 500 ? Tuning.EcoScoreTop : 0)
               + stats.Hit + stats.Dmg
               + ((stats.Place == 6 && stats.Offense != 0) || (stats.Place == 11 && stats.Defense != 0) ? Tuning.EcoElementScore : 0);
    }

    /// <summary>직업의 주 능력치 — 전사·무도가 힘(세토아)·콘(메투스), 도적 힘·덱스(셔스), 마법사·성직자 인트(로오)·위즈(이아). 평민은 없음.</summary>
    public static Stat[] Main(int path) => path switch
    {
        1 or 5 => [Stat.Str, Stat.Con],
        2 => [Stat.Str, Stat.Dex],
        3 or 4 => [Stat.Int, Stat.Wis],
        _ => [],
    };

    /// <summary>내가 입을 수 있나 — 장비이고, 직업(0 = 누구나)·레벨이 맞는다. 성별은 상점 목록에만 있어 <see cref="GearToBuy" /> 가 본다.</summary>
    public static bool Fits(ItemStats stats, int path, int level) =>
        stats.Place > 0 && (stats.Class == 0 || stats.Class == path) && stats.Level <= level;

    /// <summary>두 손에 끼는 것(반지 7·8, 장갑 9·10)은 한 부위 — 서버는 빈 손에 끼우고(Generic.cs), 물건이 알리는 자리는 어느 손일 수도 있다.</summary>
    public static int Family(int place) => place switch { 8 => 7, 10 => 9, _ => place };

    private static WornItem[] WornIn(IReadOnlyList<WornItem> worn, int place) =>
        [.. worn.Where(item => item.Stats is { } stats && Family(stats.Place) == Family(place))];

    /// <summary>그 부위에 지금 입은 것 중 가장 약한 것의 점수. 빈 자리가 있으면(반지·장갑은 두 손) null.</summary>
    private static int? WornScore(IReadOnlyList<WornItem> worn, int place, int path, int level) =>
        Empty(place, worn) ? null : WornIn(worn, place).Min(item => Score(item.Stats!, path, level));

    public static bool Better(ItemStats stats, IReadOnlyList<WornItem> worn, int path, int level) =>
        WornScore(worn, stats.Place, path, level) is not { } now || Score(stats, path, level) > now;

    /// <summary>입으면 오르는 점수 — 빈 자리면 점수 그대로. 경매에서 얼마까지 낼지(<see cref="EcoAuction.ToBuy" />)를 정한다.</summary>
    public static int Gain(ItemStats stats, IReadOnlyList<WornItem> worn, int path, int level) =>
        Score(stats, path, level) - (WornScore(worn, stats.Place, path, level) ?? 0);

    /// <summary>그 부위가 비었나(반지·장갑은 한 손이라도).</summary>
    public static bool Empty(int place, IReadOnlyList<WornItem> worn) => WornIn(worn, place).Length < (Family(place) is 7 or 9 ? 2 : 1);

    /// <summary>
    /// 이것을 끼기 전에 벗을 자리 — 두 손이 다 찼으면 약한 쪽. 서버는 두 손이 다 차면 물건이 알리는 손을 바꾸므로 센 쪽이 빠질 수 있다.
    /// 빈 손이 있거나 한 손 부위면 null(서버가 알아서 바꾼다).
    /// </summary>
    public static int? ToFree(ItemStats stats, IReadOnlyList<WornItem> worn, int path, int level)
    {
        WornItem[] on = WornIn(worn, stats.Place);
        return Family(stats.Place) is 7 or 9 && on.Length >= 2 ? on.MinBy(item => Score(item.Stats!, path, level))!.Slot : null;
    }

    /// <summary>부위마다(무기부터) 맞고 · 지금보다 좋고 · 남은 예산 안의 가장 좋은 것 하나. 서버가 거절했거나 두손 무기와 부딪힌 이름(<paramref name="refused" />)은 뺀다.</summary>
    public static IReadOnlyList<EcoBuy> GearToBuy(
        IReadOnlyList<DialogueGoods> goods, IReadOnlyList<WornItem> worn, int path, int gender, int level, long budget,
        IReadOnlyCollection<string>? refused = null)
    {
        List<EcoBuy> buys = [];

        foreach (IGrouping<int, DialogueGoods> place in goods
                     .Where(one => one.Stats is { } stats && Fits(stats, path, level) && one.Price > 0
                                   && (one.Gender == 255 || one.Gender == gender) && Better(stats, worn, path, level)
                                   && refused?.Contains(one.Name) != true)
                     .GroupBy(one => Family(one.Stats!.Place))
                     .OrderBy(group => group.Key))
        {
            if (place.Where(one => one.Price <= budget).MaxBy(one => (Score(one.Stats!, path, level), one.Stats!.Level)) is { } pick)
            {
                buys.Add(new EcoBuy(pick.Name, 1));
                budget -= pick.Price;
            }
        }

        return buys;
    }

    /// <summary>가방에서 입을 것 — 부위마다 맞고 지금보다 좋은 가장 좋은 것. 서버가 거절한 이름(<paramref name="refused" />, 성별 등)은 뺀다.</summary>
    public static IReadOnlyList<InventoryItem> ToWear(
        IReadOnlyList<InventoryItem> pack, IReadOnlyList<WornItem> worn, int path, int level, IReadOnlyCollection<string> refused) =>
    [
        .. pack.Where(item => item.Stats is { } stats && Fits(stats, path, level) && !refused.Contains(item.Name) && Better(stats, worn, path, level))
            .GroupBy(item => Family(item.Stats!.Place))
            .Select(place => place.MaxBy(item => Score(item.Stats!, path, level))!)
            .OrderBy(item => item.Slot),
    ];

    /// <summary>팔 것 — 체력 물약과 <paramref name="keep" /> 칸 말고 전부(값이 없는 것은 서버가 건너뛴다).</summary>
    public static IReadOnlyList<InventoryItem> ToSell(IReadOnlyList<InventoryItem> pack, IReadOnlyCollection<int> keep) =>
        [.. pack.Where(item => !IsHealing(item.Name) && !keep.Contains(item.Slot)).OrderBy(item => item.Slot)];
}

/// <summary>경매에 올릴 것 한 줄 — 가방 칸, 시작가, 즉시 구매가.</summary>
public sealed record EcoPost(InventoryItem Item, uint Start, uint Buyout);

/// <summary>경매에서 낼 것 한 줄 — <see cref="Amount" /> 가 즉시 구매가면 즉시 구매, 아니면 그 값으로 입찰.</summary>
public sealed record EcoBid(AuctionRow Row, uint Amount)
{
    public bool Buyout => Row.Buyout > 0 && Amount >= Row.Buyout;
}

/// <summary>
/// 생태계 봇의 경매장(FR-014·015) — 못 입는 장비는 올리고, 맞고 지금 것보다 좋은 장비는 산다. 올리는 값은 시세(<see cref="EcoMarket" />)가 있으면
/// 시세 배수, 없으면 상인 매입가(서버 <c>ShopPricing.Offer</c> = 값 / 1.6) 배수. 사는 값은 들고 있는 금화의 <see cref="Tuning.EcoAuctionBudget" />% 와 상한 중
/// 작은 것까지, 시세가 있으면 점수 이득이 클수록 시세 위로 더(<see cref="Tuning.EcoMarketOver" />) — 수요가 값을 올린다.
/// </summary>
public static class EcoAuction
{
    /// <summary>서버 상인 매입가 — 값 / 1.6 내림, 묶음 수만큼.</summary>
    public static long Offer(InventoryItem item) => (long)((item.Stats?.Value ?? 0) / 1.6) * Math.Max(1, item.Stacks);

    /// <summary>
    /// 올릴 것 — 장비(입는 칸이 있는 것) 중 지금 입을 것(<paramref name="wear" />)이 아니고, 서버가 거절했거나 유찰돼 돌아온 적 없고
    /// (<paramref name="refused" />), 값이 있는 것. 이미 걸어 둔 <paramref name="active" /> 개와 합쳐 <see cref="Tuning.EcoAuctionMax" /> 개까지.
    /// </summary>
    /// <param name="market">이름별 시세 — 있으면 시작가·즉시 구매가 = 시세의 <see cref="Tuning.EcoMarketStart" />·<see cref="Tuning.EcoMarketBuyout" />%.</param>
    public static IReadOnlyList<EcoPost> ToPost(
        IReadOnlyList<InventoryItem> pack, IReadOnlyList<InventoryItem> wear, int active, IReadOnlyCollection<string> refused,
        IReadOnlyDictionary<string, long>? market = null) =>
    [
        .. pack.Where(item => item.Stats is { Place: > 0 } && Offer(item) > 0
                              && !refused.Contains(item.Name) && wear.All(one => one.Slot != item.Slot))
            .OrderByDescending(Offer)
            .Take(Math.Max(0, Tuning.EcoAuctionMax - active))
            .Select(item => market?.TryGetValue(item.Name, out long price) == true
                ? new EcoPost(item, Price(price * Tuning.EcoMarketStart / 100), Price(price * Tuning.EcoMarketBuyout / 100))
                : new EcoPost(item, Price(Offer(item) * Tuning.EcoAuctionStart), Price(Offer(item) * Tuning.EcoAuctionBuyout))),
    ];

    // 서버가 받는 값의 상한(서버 AuctionHouse.AUCTION_MAX_PRICE).
    private const long MaxPrice = 2_000_000_000;

    private static uint Price(long gold) => (uint)Math.Clamp(gold, 1, MaxPrice);

    /// <summary>
    /// 살 것 — 남의 경매 중 내가 입을 수 있고 지금 것보다 좋은 장비. 부위마다 가장 좋은 것 하나, 사는 대로 금화를 빼며 물건 하나에
    /// min(금화 × <see cref="Tuning.EcoAuctionBudget" />%(빈 부위는 <see cref="Tuning.EcoAuctionBareBudget" />%), <see cref="Tuning.EcoAuctionSpendCap" />) 까지.
    /// 즉시 구매가가 그 안이고 <see cref="Ceiling" /> 안이면 즉시 구매, 시세가 있으면 다음 입찰가가 그 안일 때 입찰. 시세가 없으면 즉시 구매만(예전 그대로).
    /// 이미 최고 입찰자인 부위는 그 경매를 기다린다.
    /// </summary>
    /// <param name="refused">서버가 입기를 거절한 이름(성별 등 — 경매 줄에는 성별이 없다). 사서 못 입고 다시 올리는 일을 되풀이하지 않게.</param>
    public static IReadOnlyList<EcoBid> ToBuy(
        IReadOnlyList<AuctionRow> rows, IReadOnlyList<WornItem> worn, int path, int level, long gold, IReadOnlyCollection<string>? refused = null,
        IReadOnlyDictionary<string, long>? market = null)
    {
        List<EcoBid> buys = [];
        foreach (IGrouping<int, AuctionRow> place in rows
                     .Where(row => (row.Flags & 1) == 0 && refused?.Contains(row.Name) != true
                                   && row.Stats is { } stats && EcoShopping.Fits(stats, path, level) && EcoShopping.Better(stats, worn, path, level))
                     .GroupBy(row => EcoShopping.Family(row.Stats!.Place))
                     .OrderBy(group => group.Key))
        {
            if (place.Any(row => (row.Flags & 2) != 0))
            {
                continue;
            }

            int share = EcoShopping.Empty(place.Key, worn) ? Tuning.EcoAuctionBareBudget : Tuning.EcoAuctionBudget;
            long limit = Math.Min(gold * share / 100, Tuning.EcoAuctionSpendCap);
            // 낼 값은 즉시 구매가(uint)이거나 서버 값 상한(20억)의 5% 위까지라 uint 에 든다.
            if (place.Select(row => new EcoBid(row, (uint)Pay(row, Ceiling(row, worn, path, level, market))))
                    .Where(bid => bid.Amount > 0 && bid.Amount <= limit)
                    .MaxBy(bid => (EcoShopping.Score(bid.Row.Stats!, path, level), -(long)bid.Amount)) is { } pick)
            {
                buys.Add(pick);
                gold -= pick.Amount;
            }
        }

        return buys;
    }

    /// <summary>
    /// 이 봇이 낼 값의 상한 — 시세 × (100 + 점수 이득 × <see cref="Tuning.EcoMarketOver" /> / 100, 많아야 <see cref="Tuning.EcoMarketOverMax" />)%.
    /// 시세가 없으면 null(금화 상한만).
    /// </summary>
    public static long? Ceiling(AuctionRow row, IReadOnlyList<WornItem> worn, int path, int level, IReadOnlyDictionary<string, long>? market) =>
        market?.TryGetValue(row.Name, out long price) == true
            ? price * (100 + Math.Min(EcoShopping.Gain(row.Stats!, worn, path, level) * Tuning.EcoMarketOver / 100, Tuning.EcoMarketOverMax)) / 100
            : null;

    /// <summary>
    /// 낼 값 — 즉시 구매가가 상한 안이면 그것, 상한(시세)이 있으면 다음 입찰가(서버 <c>NextBid</c>: 입찰이 없으면 시작가, 있으면
    /// <c>AUCTION_MIN_STEP</c> 5% 위)가 상한 안이면 그것, 아니면 0.
    /// </summary>
    private static long Pay(AuctionRow row, long? ceiling)
    {
        if (row.Buyout > 0 && row.Buyout <= (ceiling ?? long.MaxValue))
        {
            return row.Buyout;
        }

        long next = (row.Flags & 4) != 0 ? row.Price + Math.Max(1L, row.Price * 5L / 100) : row.Price;
        return ceiling is { } most && next <= most ? next : 0;
    }
}

/// <summary>파티를 지을 봇 하나 — 접속해 있고 아직 파티가 없는 것.</summary>
public sealed record EcoMember(string Name, int Path, int Level);

/// <summary>생태계 파티 — 성직자 하나와 싸우는 봇 셋(결정 19). 파티장은 셋 중 레벨이 가장 낮은 봇(사냥터를 그 레벨로 고른다).</summary>
public sealed record EcoParty(string Priest, IReadOnlyList<string> Fighters)
{
    public string Leader => Fighters[0];

    public IEnumerable<string> All => [.. Fighters, Priest];
}

/// <summary>파티 짓기(설계 <c>autopilot/eco-bots/party-SPEC.md</c>).</summary>
public static class EcoParties
{
    /// <summary>성직자 직업 번호.</summary>
    public const int Priest = 4;

    /// <summary>
    /// 성직자 봇의 레벨업 점수 — 위즈 150 까지, 그다음 콘 50(정해진 것 없어 시작값, 2026-10-07). 사람 성직자의 앱 자동 분배
    /// (<see cref="StatPlan" />)는 건드리지 않으려고 여기 따로 둔다.
    /// </summary>
    public static Stat? PriestStat(Vitals mine) =>
        mine.Unspent <= 0 ? null : mine.Wis < 150 ? Stat.Wis : mine.Con < 50 ? Stat.Con : null;

    /// <summary>
    /// 성직자마다(이름 순) 파티 레벨을 넘은 싸우는 봇 셋 — 남은 봇 중 레벨이 가장 낮은 봇을 파티장으로, 그와 레벨이 가까운 순으로
    /// 직업이 겹치지 않는 봇을 먼저, 모자라면 아무 직업이나. 셋이 안 되면 그 성직자부터는 짓지 않는다.
    /// </summary>
    public static IReadOnlyList<EcoParty> Form(IReadOnlyList<EcoMember> free, int minLevel)
    {
        List<EcoMember> fighters = [.. free.Where(one => one.Path != Priest && one.Level >= minLevel).OrderBy(one => one.Level).ThenBy(one => one.Name)];
        List<EcoParty> parties = [];

        foreach (EcoMember priest in free.Where(one => one.Path == Priest).OrderBy(one => one.Name, StringComparer.Ordinal))
        {
            if (fighters.Count < Tuning.EcoPartyFighters)
            {
                break;
            }

            EcoMember leader = fighters[0];
            List<EcoMember> chosen = [leader];
            IEnumerable<EcoMember> near = fighters.Skip(1).OrderBy(one => Math.Abs(one.Level - leader.Level)).ThenBy(one => one.Name).ToList();

            chosen.AddRange(near.Where(one => chosen.All(pick => pick.Path != one.Path)).DistinctBy(one => one.Path).Take(Tuning.EcoPartyFighters - 1));
            chosen.AddRange(near.Except(chosen).Take(Tuning.EcoPartyFighters - chosen.Count));
            fighters.RemoveAll(chosen.Contains);
            parties.Add(new EcoParty(priest.Name, [.. chosen.Select(one => one.Name)]));
        }

        return parties;
    }
}
