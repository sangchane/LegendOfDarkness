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
    /// 적정 레벨 ≤ 내 레벨 중 가장 높은 층(내 레벨이 가장 낮은 층보다 낮으면 그 층, <paramref name="lower" /> 만큼 아래 층, 맨 아래 밑으로는 안 감), 그 층에서 봇이 가장 적은 맵.
    /// 봇이 <paramref name="perMap" /> 인 맵과 <paramref name="avoid" /> 는 빼고, 그 층이 다 차면 아래 층.
    /// 갈 수 있는 층이 다 차면(새 봇이 한꺼번에 첫 층에 몰릴 때) 제 층에서 봇이 가장 적은 맵으로 넘친다 — 마을에서 놀지 않게. 사냥터가 없으면 null.
    /// </summary>
    public static EcoGround? Pick(
        IReadOnlyList<EcoGround> grounds, int level, Func<int, int> botsOn, int perMap, IReadOnlyCollection<int> avoid, int lower = 0)
    {
        // 가장 낮은 사냥터보다 레벨이 낮으면(새 캐릭터) 가장 낮은 층으로.
        int reach = grounds.Count == 0 ? level : Math.Max(level, grounds.Min(one => one.Level));
        int[] tiers = [.. grounds.Where(one => one.Level <= reach).Select(one => one.Level).Distinct().OrderByDescending(tier => tier)];

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

    /// <summary>장비 점수 — 무기는 최대 공격력 + 공격 + 명중, 그 밖은 방어(AC 는 낮을수록 좋다) + 힘·콘·덱스 + 체력/10.</summary>
    public static int Score(ItemStats stats) =>
        stats.Place == 1
            ? stats.DmgMax + stats.Dmg + stats.Hit
            : -stats.Ac + stats.Str + stats.Con + stats.Dex + (stats.Hp / 10) + stats.Hit + stats.Dmg;

    /// <summary>내가 입을 수 있나 — 장비이고, 직업(0 = 누구나)·레벨이 맞는다. 성별은 상점 목록에만 있어 <see cref="GearToBuy" /> 가 본다.</summary>
    public static bool Fits(ItemStats stats, int path, int level) =>
        stats.Place > 0 && (stats.Class == 0 || stats.Class == path) && stats.Level <= level;

    /// <summary>그 부위에 지금 입은 것 중 가장 약한 것의 점수(반지처럼 두 개 입는 부위). 안 입었으면 null.</summary>
    private static int? WornScore(IReadOnlyList<WornItem> worn, int place) =>
        worn.Where(item => item.Stats?.Place == place).Select(item => (int?)Score(item.Stats!)).Min();

    private static bool Better(ItemStats stats, IReadOnlyList<WornItem> worn) =>
        WornScore(worn, stats.Place) is not { } now || Score(stats) > now;

    /// <summary>부위마다(무기부터) 맞고 · 지금보다 좋고 · 남은 예산 안의 가장 좋은 것 하나.</summary>
    public static IReadOnlyList<EcoBuy> GearToBuy(
        IReadOnlyList<DialogueGoods> goods, IReadOnlyList<WornItem> worn, int path, int gender, int level, long budget)
    {
        List<EcoBuy> buys = [];

        foreach (IGrouping<int, DialogueGoods> place in goods
                     .Where(one => one.Stats is { } stats && Fits(stats, path, level) && one.Price > 0
                                   && (one.Gender == 255 || one.Gender == gender) && Better(stats, worn))
                     .GroupBy(one => one.Stats!.Place)
                     .OrderBy(group => group.Key))
        {
            if (place.Where(one => one.Price <= budget).MaxBy(one => (Score(one.Stats!), one.Stats!.Level)) is { } pick)
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
        .. pack.Where(item => item.Stats is { } stats && Fits(stats, path, level) && !refused.Contains(item.Name) && Better(stats, worn))
            .GroupBy(item => item.Stats!.Place)
            .Select(place => place.MaxBy(item => Score(item.Stats!))!)
            .OrderBy(item => item.Slot),
    ];

    /// <summary>팔 것 — 체력 물약과 <paramref name="keep" /> 칸 말고 전부(값이 없는 것은 서버가 건너뛴다).</summary>
    public static IReadOnlyList<InventoryItem> ToSell(IReadOnlyList<InventoryItem> pack, IReadOnlyCollection<int> keep) =>
        [.. pack.Where(item => !IsHealing(item.Name) && !keep.Contains(item.Slot)).OrderBy(item => item.Slot)];
}

/// <summary>경매에 올릴 것 한 줄 — 가방 칸, 시작가, 즉시 구매가.</summary>
public sealed record EcoPost(InventoryItem Item, uint Start, uint Buyout);

/// <summary>
/// 생태계 봇의 경매장(FR-014·015) — 못 입는 장비는 올리고, 맞고 지금 것보다 좋은 장비는 즉시 구매로 산다. 값은 상인 매입가(서버
/// <c>ShopPricing.Offer</c> = 값 / 1.6) 배수, 사는 값은 들고 있는 금화의 <see cref="Tuning.EcoAuctionBudget" />% 와 상한 중 작은 것까지.
/// </summary>
public static class EcoAuction
{
    /// <summary>서버 상인 매입가 — 값 / 1.6 내림, 묶음 수만큼.</summary>
    public static long Offer(InventoryItem item) => (long)((item.Stats?.Value ?? 0) / 1.6) * Math.Max(1, item.Stacks);

    /// <summary>
    /// 올릴 것 — 장비(입는 칸이 있는 것) 중 지금 입을 것(<paramref name="wear" />)이 아니고, 서버가 거절한 적 없고, 값이 있고,
    /// 즉시 구매가가 들 수 있는 금화(1억) 안인 것. 이미 걸어 둔 <paramref name="active" /> 개와 합쳐 <see cref="Tuning.EcoAuctionMax" /> 개까지.
    /// 값이 5억인 이벤트 물건 같은 것은 시작가·즉시 구매가가 1억에 잘려 아무도 못 사고, 하루 뒤 유찰로 보증금(9천만 남짓)만
    /// 사라졌다(클라우드 2026-10-07 — 전사·도적 봇 34건, 20억).
    /// </summary>
    public static IReadOnlyList<EcoPost> ToPost(
        IReadOnlyList<InventoryItem> pack, IReadOnlyList<InventoryItem> wear, int active, IReadOnlyCollection<string> refused) =>
    [
        .. pack.Where(item => item.Stats is { Place: > 0 } && Offer(item) > 0 && Offer(item) * Tuning.EcoAuctionBuyout <= MaxGold
                              && !refused.Contains(item.Name) && wear.All(one => one.Slot != item.Slot))
            .OrderByDescending(Offer)
            .Take(Math.Max(0, Tuning.EcoAuctionMax - active))
            .Select(item => new EcoPost(item, Price(Offer(item) * Tuning.EcoAuctionStart), Price(Offer(item) * Tuning.EcoAuctionBuyout))),
    ];

    // 서버가 받는 값은 들 수 있는 금화(서버 MaxCarryGold)까지.
    private const long MaxGold = 100_000_000;

    private static uint Price(long gold) => (uint)Math.Clamp(gold, 1, MaxGold);

    /// <summary>
    /// 살 것 — 남의 경매 중 즉시 구매가가 있고, 내가 입을 수 있고 지금 것보다 좋은 장비. 부위마다 가장 좋은 것 하나, 사는 대로 금화를 빼며
    /// 물건 하나에 min(금화 × <see cref="Tuning.EcoAuctionBudget" />%, <see cref="Tuning.EcoAuctionSpendCap" />) 까지.
    /// </summary>
    /// <param name="refused">서버가 입기를 거절한 이름(성별 등 — 경매 줄에는 성별이 없다). 사서 못 입고 다시 올리는 일을 되풀이하지 않게.</param>
    public static IReadOnlyList<AuctionRow> ToBuy(
        IReadOnlyList<AuctionRow> rows, IReadOnlyList<WornItem> worn, int path, int level, long gold, IReadOnlyCollection<string>? refused = null)
    {
        List<AuctionRow> buys = [];
        foreach (IGrouping<int, AuctionRow> place in rows
                     .Where(row => (row.Flags & 1) == 0 && row.Buyout > 0 && refused?.Contains(row.Name) != true
                                   && row.Stats is { } stats && EcoShopping.Fits(stats, path, level)
                                   && (worn.Where(one => one.Stats?.Place == stats.Place).Select(one => (int?)EcoShopping.Score(one.Stats!)).Min() is not { } now
                                       || EcoShopping.Score(stats) > now))
                     .GroupBy(row => row.Stats!.Place)
                     .OrderBy(group => group.Key))
        {
            long limit = Math.Min(gold * Tuning.EcoAuctionBudget / 100, Tuning.EcoAuctionSpendCap);
            if (place.Where(row => row.Buyout <= limit).MaxBy(row => (EcoShopping.Score(row.Stats!), -row.Buyout)) is { } pick)
            {
                buys.Add(pick);
                gold -= pick.Buyout;
            }
        }

        return buys;
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
