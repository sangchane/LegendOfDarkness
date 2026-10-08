using System.Diagnostics;
using System.Net;
using Lod.CompanionBot;
using Lod.HuntProxy;
using Lod.Mobile.Core;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace Lod.EcoBots;

/// <summary>
/// 생태계 봇 하나의 접속 — 들어가(없으면 만들고) <see cref="EcoLife" /> 가 정한 일을 한다: 사냥은 대신 사냥과 같은 한 틱
/// (<see cref="HuntProxyRunner" />), 옮기기는 걸어서 워프·월드맵을 잇고(길이 없을 때만 봇 전용 순간이동 0xF1 8), 장보기는 가게를 차례로 돌며 입기·팔기·물약·장비.
/// 일마다 사건 한 줄(<see cref="EcoLog" />). 접속이 끊기면 돌아온다(다시 들이기는 <see cref="EcoHost" />).
/// 파티(<see cref="EcoParty" />, 결정 19)에 들면 싸우는 봇은 파티장의 사냥터로 가고 파티장이 서버 그룹을 청한다. 성직자는 사냥하지 않고
/// 파티장 곁에서 동료 봇의 판단(<see cref="CompanionRunner" />, 주인 = 파티장)으로 파티원을 돌본다.
/// </summary>
public sealed class EcoRunner(EcoBotEntry bot, EcoConfig config, EcoWorld land, EcoHost host, EcoEvents events, Action<string> log)
{
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan Dead = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan Answer = TimeSpan.FromSeconds(5);

    /// <summary>죽으면 오는 곳(서버 설정 DeathMap) — 뮤레칸이 12,5 에 있다.</summary>
    public const int DeathMap = 20138;

    private static readonly Tile Murekan = new(12, 5);

    private readonly EcoLife _life = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HashSet<string> _refused = [];
    private readonly HashSet<string> _unlisted = [];
    private WorldClient _world = null!;
    private HuntProxyRunner? _hunt;
    private int _ground;
    private TimeSpan? _personSince;
    private TimeSpan _nextTick = TimeSpan.FromMinutes(1);
    private long _lastExp = -1;
    private TimeSpan _lastKill;
    private int _lastLevel;
    private bool _dead;
    private EcoParty? _party;
    private CompanionRunner? _priest;
    private TimeSpan _nextAsk;
    private string _following = string.Empty;
    private TimeSpan _nextStock;
    private int _gearLevel;

    public string Name => bot.Name;

    /// <summary>지금 맵(요약·사냥터 나누기용). 접속 전이면 0.</summary>
    public int Map => _world?.State?.Map.Id ?? 0;

    public int Level => _world?.Vitals?.Level ?? 0;

    public int Path => _world?.Path ?? bot.Path;

    public uint Serial => _world?.Serial ?? 0;

    public Tile Where => _world?.State?.Where ?? default;

    /// <summary>사냥 중인 사냥터 맵 — 파티원이 따라온다. 사냥 중이 아니면 0.</summary>
    public int HuntingOn => _life.Where == EcoPlace.Hunting && _hunt is { Stopped: null } ? _ground : 0;

    /// <summary>사냥 중심(사냥터에 내린 칸) — 파티원은 파티장의 중심으로 간다.</summary>
    public Tile Center { get; private set; }

    /// <summary>지금 하는 일 — 요약 한 줄에.</summary>
    public string Doing { get; private set; } = "접속";

    public async Task RunAsync(CancellationToken token)
    {
        using WorldSession session = await Enter(token);
        using WorldClient world = new(session);
        _world = world;
        Task pump = world.PumpAsync(token);

        // 직업은 내 프로필(0x2D → 0x39)에서만 안다 — 묻지 않으면 Path 가 없어 능력치도, 직업 장비도 못 고른다.
        await world.AskProfileAsync(token);
        await Until(() => world.Path is not null, Answer, token);
        log($"접속했습니다(직업 {world.Path?.ToString() ?? "모름"}).");

        while (!token.IsCancellationRequested && !pump.IsCompleted && world.Broke is null)
        {
            if (DateTime.UtcNow - world.LastHeard >= Dead)
            {
                log("서버 소식이 90초 없음 — 끊고 다시 들어갑니다.");
                return;
            }

            if (world.State is not null && world.Vitals is not null && world.Serial != 0)
            {
                await Once(token);
            }

            await Task.Delay(Tick, token);
        }

        log($"끊김 — {world.Broke?.ToString() ?? "받기가 끝남"}");
    }

    /// <summary>로그인, 계정이 없으면 설정의 직업으로 만든다.</summary>
    private async Task<WorldSession> Enter(CancellationToken token)
    {
        IPAddress address = IPAddress.TryParse(config.Host, out IPAddress? parsed) ? parsed : (await Dns.GetHostAddressesAsync(config.Host, token))[0];

        try
        {
            return await HadesLoginClient.LoginAsync(address, config.LoginPort, bot.Name, config.Password, null, token);
        }
        catch (ProtocolException refused) when (refused.Message.Contains("계정", StringComparison.Ordinal))
        {
            log($"계정이 없어 만듭니다(직업 {bot.Path}).");
            return await HadesLoginClient.CreateCharacterAsync(
                address, config.LoginPort, bot.Name, config.Password, hairStyle: 1, gender: (byte)bot.Gender, hairColor: 1, path: (byte)bot.Path,
                cancellationToken: token);
        }
    }

    private async Task Once(CancellationToken token)
    {
        TimeSpan now = _clock.Elapsed;
        Vitals vitals = _world.Vitals!;
        int map = _world.State!.Map.Id;
        Watch(now, vitals, map);

        if ((Path == EcoParties.Priest ? EcoParties.PriestStat(vitals) : StatPlan.Next(_world.Path, vitals)) is { } stat)
        {
            await _world.RaiseAsync(stat, token);
        }

        if (host.PartyOf(Name) is var party && !ReferenceEquals(party, _party))
        {
            await Regroup(party, token);
        }

        // 제 파티장의 그룹 청만 받는다.
        while (_world.TakeAsk(out string? asker))
        {
            if (string.Equals(asker, _party?.Leader, StringComparison.OrdinalIgnoreCase))
            {
                log($"파티장 {asker} 의 그룹 청을 받습니다.");
                await _world.AcceptGroupAsync(asker, token);
            }
        }

        if (Path == EcoParties.Priest)
        {
            await Tend(map, token);
            return;
        }

        await Invite(token);

        EcoSight sight = new(
            now,
            Overhead.InComa(_world.Ailments),
            Ghost: map == DeathMap,
            EcoShopping.Potions(_world.Pack),
            Tuning.PackSlots - _world.Pack.Count,
            _hunt is null || _hunt.Stopped is not null,
            _personSince,
            vitals.MaximumHealth > 0 ? vitals.Health * 100 / vitals.MaximumHealth : 100,
            _lastKill);

        // 파티원 — 파티장이 다른 사냥터나 다른 중심으로 옮겼으면 따라간다(마을 가는 때가 봇마다 달라 갈라진다).
        if (_party is { } mine && !string.Equals(mine.Leader, Name, StringComparison.OrdinalIgnoreCase) && HuntingOn > 0
            && host.Find(mine.Leader) is { HuntingOn: > 0 } boss && boss.Map == boss.HuntingOn
            && (boss.HuntingOn != _ground || Reckon.Steps(Center, boss.Center) > 6))
        {
            await GoHunt(sight, token);
            return;
        }

        EcoAct act = _life.Next(sight);
        if (act != EcoAct.Hunt)
        {
            Doing = act.ToString();
        }

        switch (act)
        {
            case EcoAct.Hunt:
                Doing = "사냥";
                // 파티원은 파티장이 선 칸을 중심으로 싸운다(Tuning.EcoPartyReach) — 성직자 손이 닿는 곳에서.
                if (_party is { } team && !string.Equals(team.Leader, Name, StringComparison.OrdinalIgnoreCase)
                    && _world.Others.FirstOrDefault(one => string.Equals(one.Name, team.Leader, StringComparison.OrdinalIgnoreCase)) is { } seen)
                {
                    _hunt!.Recenter(seen.Where);
                }

                await _hunt!.Once(DateTime.UtcNow, token);
                break;
            case EcoAct.GoHunt:
                await GoHunt(sight, token);
                break;
            case EcoAct.GoTown:
                Event("state", new { from = _life.Where.ToString(), to = "Town", why = Why(sight) });
                await GoTown(token);
                break;
            case EcoAct.Shop:
                await Shop(token);
                break;
            case EcoAct.Revive:
                await Revive(token);
                break;
        }
    }

    /// <summary>경험치·레벨이 바뀐 것과 사람이 보이는지를 지켜본다 — 잡음·레벨 사건, 분마다 요약 사건.</summary>
    private void Watch(TimeSpan now, Vitals vitals, int map)
    {
        if (_lastExp >= 0 && vitals.Experience > _lastExp && _life.Where == EcoPlace.Hunting)
        {
            Event("kill", new { gain = vitals.Experience - _lastExp, ms = (long)(now - _lastKill).TotalMilliseconds });
            _lastKill = now;
        }

        if (_lastLevel > 0 && vitals.Level > _lastLevel)
        {
            Event("level", new { from = _lastLevel, to = vitals.Level });
            _life.LeveledUp();
        }

        _lastExp = vitals.Experience;
        _lastLevel = vitals.Level;

        bool person = map != DeathMap && _world.Others.Any(one => one.Serial != _world.Serial && one.Name.Length > 0 && !host.IsBot(one.Name));
        _personSince = person ? _personSince ?? now : null;

        if (now >= _nextTick)
        {
            _nextTick = now + TimeSpan.FromMinutes(1);
            Event("tick", new { potions = EcoShopping.Potions(_world.Pack), bag = _world.Pack.Count, doing = Doing });
        }
    }

    /// <summary>
    /// 파티가 바뀌었다 — 옛 서버 그룹에서 나가고(제 이름을 청하면 나간다), 사냥을 멈춰 마을에 들렀다 새 사냥터로 간다
    /// (사냥이 멈추면 <see cref="EcoLife" /> 가 마을로 보낸다).
    /// </summary>
    private async Task Regroup(EcoParty? party, CancellationToken token)
    {
        if (_party is not null)
        {
            Event("party", new { left = _party.All });
            await _world.LeaveGroupAsync(token);
        }

        _party = party;
        _hunt = null;
        _priest = null;

        if (party is not null)
        {
            Event("party", new { joined = party.All, leader = party.Leader });
        }
    }

    /// <summary>파티장 — 12칸 안에 온 파티원 중 아직 그룹이 아닌 이에게 5초마다 그룹을 청한다(0x2E 1).</summary>
    private async Task Invite(CancellationToken token)
    {
        if (_party is not { } party || !string.Equals(party.Leader, Name, StringComparison.OrdinalIgnoreCase) || HuntingOn == 0 || _clock.Elapsed < _nextAsk)
        {
            return;
        }

        _nextAsk = _clock.Elapsed + TimeSpan.FromSeconds(5);

        foreach (string name in party.All.Where(name => name != Name && _world.MemberStatus(name) is null
                                                        // 서버는 12칸 안에서만 청을 받는다(WithinRangeProximity).
                                                        && _world.Others.Any(one => string.Equals(one.Name, name, StringComparison.OrdinalIgnoreCase)
                                                                                    && Reckon.Steps(one.Where, Where) < 12)))
        {
            log($"{name} 에게 그룹을 청합니다.");
            await _world.AskToGroupAsync(name, token);
        }
    }

    /// <summary>
    /// 성직자 한 틱 — 유령이면 되살아나고, 파티가 없으면 물약 가게에서 기다린다. 따를 이(사냥 중인 파티원, 파티장 먼저 — 파티장만
    /// 마을에 가도 사냥터에 남은 이를 돌본다. 아무도 사냥 중이 아니면 파티장)와 다른 맵이거나 멀면(12칸) 곁으로 옮기고, 곁이면 동료 봇의
    /// 판단(주인 = 따를 이, 나머지 싸우는 봇은 파티원)으로 회복·해제·따라가기.
    /// </summary>
    private async Task Tend(int map, CancellationToken token)
    {
        if (map == DeathMap)
        {
            await Revive(token);
            return;
        }

        EcoRunner? leader = _party is null ? null
            : _party.Fighters.Select(host.Find).FirstOrDefault(one => one is { HuntingOn: > 0 } && one.Map == one.HuntingOn) ?? host.Find(_party.Leader);

        // 유령인 파티장을 따라 죽은 자의 맵으로 가지 않는다.
        if (_party is null || leader is not { Map: > 0 } || leader.Map == DeathMap)
        {
            Doing = "파티 기다림";
            if (land.PotionStop is { } stop && map != stop.Map)
            {
                await MoveTo(stop.Map, stop.Where, token);
            }

            return;
        }

        if (leader.Map != map || Reckon.Steps(Where, leader.Where) > 12)
        {
            Doing = "파티장에게";
            await MoveTo(leader.Map, leader.Where, token);
            return;
        }

        // 마력 물약 — 모자라면 물약 가게에 다녀온다(다음 틱에 파티장 곁으로 돌아간다).
        if (EcoShopping.ManaPotions(_world.Pack) < Tuning.EcoPriestManaLow && _clock.Elapsed >= _nextStock)
        {
            _nextStock = _clock.Elapsed + TimeSpan.FromMinutes(5);
            await StockMana(token);
            return;
        }

        // 장비 — 레벨이 오를 때마다(처음 포함) 경매장·장비 가게를 돈다. 성직자 체력은 원래 낮아 체력·방어 장비로 채운다(사용자 10-07
        // 「세줄금반지나 칸의녹옥반지 같은 걸로 … 체력 아이템, 장비 방어력」). 전에는 물약 가게만 들러 갑옷 하나로 99 근처까지 갔다.
        if (_world.Vitals!.Level > _gearLevel)
        {
            _gearLevel = _world.Vitals.Level;
            Doing = "장비 사기";
            await Auction(token);
            await BuyGear(reserve: 0, token);
            return;
        }

        Doing = "돌봄";
        EcoParty party = _party;
        _following = leader.Name;
        _priest ??= new CompanionRunner(_world, land.Walls, new CompanionSettings(), log,
            master: () => host.Find(_following) is { Serial: > 0 } boss ? new CompanionTie(boss.Serial, boss.Name) : null,
            mates: () => party.Fighters
                .Where(name => name != _following)
                .Select(name => host.Find(name)?.Serial ?? 0)
                .Select(serial => _world.Others.FirstOrDefault(one => one.Serial == serial && serial != 0))
                .OfType<Character>()
                .ToDictionary(one => one.Serial, one => one.Where));
        await _priest.Once(token);
    }

    /// <summary>
    /// 성직자 장보기 — 경매장(룰렛으로 받은 장비를 올리고 맞는 것을 산다) → 물약 가게에서 물약 아닌 것은 팔고 마력 물약을
    /// <see cref="Tuning.EcoPriestManaStock" /> 개까지. 그룹 전리품을 나눠 받으므로 팔지 않으면 가방이 찬다.
    /// </summary>
    private async Task StockMana(CancellationToken token)
    {
        Doing = "물약 사기";
        await Auction(token);
        if (land.PotionStop is not { } stop || !await MoveTo(stop.Map, stop.Where, token) || await Merchant(stop, token) is not { } seller)
        {
            return;
        }

        int[] potions = [.. _world.Pack.Where(item => AutoPotion.Restoring.Any(potion => potion.Name == item.Name)).Select(item => item.Slot)];
        await Sell(seller, EcoShopping.ToSell(_world.Pack, keep: potions), token);

        if (await Goods(seller, token) is { } goods
            && EcoShopping.PotionsToBuy(goods, _world.Vitals!.Level, _world.Vitals.Gold, EcoShopping.ManaPotions(_world.Pack), Tuning.EcoPriestManaStock,
                AutoPotion.Restoring) is { } buy)
        {
            await Buy(seller, [buy], goods, token);
        }
        else
        {
            log("물약 가게에서 살 마력 물약이 없습니다.");
        }

        await _world.ShutDialogueAsync(token);
    }

    private static string Why(EcoSight sight) =>
        sight.HuntStopped ? "stopped"
        : sight.Potions < Tuning.EcoPotionLow ? "potions"
        : sight.FreeSlots < Tuning.EcoBagLow ? "bag"
        : "time";

    private async Task GoHunt(EcoSight sight, CancellationToken token)
    {
        int level = _world.Vitals!.Level;
        // 걸어서 못 가는 사냥터는 고르지 않는다 — 아벨해안은 적정 99 인데 입구가 51~80 만 들인다(사람과 같은 레벨 검사). 길 자료가 없으면 거르지 않는다.
        // 마을(물약 가게 맵)에서 따진다 — 지금 맵에서 따지면 나가는 워프에 최대 레벨이 걸린 맵(노비스지하던전·포테의숲)에서 레벨이 오를 때 다 빠진다.
        int town = land.PotionStop?.Map ?? _world.State!.Map.Id;
        int[] avoid = [.. _personSince is not null ? [_ground] : Array.Empty<int>(),
            .. land.Links.Fields.Count == 0 ? [] : land.Grounds.Where(one => one.Map != town && EcoRoute.Plan(land.Links, town, one.Map, level) is null).Select(one => one.Map)];
        EcoGround ground;
        Tile spot;

        if (_party is { } party && !string.Equals(party.Leader, Name, StringComparison.OrdinalIgnoreCase))
        {
            // 파티원 — 파티장이 사냥하는 맵, 파티장 곁으로.
            if (host.Find(party.Leader) is not { HuntingOn: > 0 } leader)
            {
                Doing = "파티장 기다림";
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                return;
            }

            ground = land.Grounds.FirstOrDefault(one => one.Map == leader.HuntingOn) ?? new EcoGround(leader.HuntingOn, level, "파티");
            spot = leader.Center;
        }
        else
        {
            // 파티장은 파티 밖의 봇이 없는 맵으로(한 맵에 한 파티).
            IEnumerable<string> mine = _party?.All ?? [Name];
            Func<int, int> botsOn = map => host.Running.Count(runner => runner.Map == map && !mine.Contains(runner.Name, StringComparer.OrdinalIgnoreCase));

            if (EcoGrounds.Pick(land.Grounds, level, botsOn, _party is null ? Tuning.EcoBotsPerMap : 1, avoid, _life.Lower) is not { } picked)
            {
                Doing = "사냥터 없음";
                await Task.Delay(TimeSpan.FromSeconds(60), token);
                return;
            }

            ground = picked;
            Func<Tile, bool> blocked = land.Walls.For(ground.Map);
            Random dice = Random.Shared;
            spot = Enumerable.Range(0, 500).Select(_ => new Tile(dice.Next(2, 100), dice.Next(2, 100))).FirstOrDefault(tile => !blocked(tile), new Tile(10, 10));
        }

        int from = _world.State!.Map.Id;
        if (!await MoveTo(ground.Map, spot, token))
        {
            return;
        }

        Event("move", new { fromMap = from, toMap = ground.Map, why = "hunt", ground = ground.Name, groundLevel = ground.Level });
        _ground = ground.Map;
        _personSince = null;
        _life.Arrived(EcoPlace.Hunting, _clock.Elapsed);
        Center = _world.State!.Where;

        // 대신 사냥과 같은 판단 — 기술·마법은 직업 기술 표(class-kit.txt)에 보이는 것, 물약은 가방의 가장 센 체력 물약.
        int? path = _world.Path;
        ProxyOrders orders = new()
        {
            // 파티원은 파티장 곁만 — 중심은 사냥 틱마다 파티장이 선 칸으로 옮긴다(Once).
            Radius = _party is { } team && !string.Equals(team.Leader, Name, StringComparison.OrdinalIgnoreCase) ? Tuning.EcoPartyReach : 12,
            Map = ground.Map,
            X = _world.State!.Where.X,
            Y = _world.State.Where.Y,
            Hp = new PotionRule(true, 50, EcoShopping.BestPotion(_world.Pack) ?? AutoPotion.Healing[0].Name),
            Loot = true,
            Skills = [.. _world.Skills.Select(one => one.Name).Where(name => land.Kit.Shows(path, false, name) && land.Kit.AutoCasts(path, name))],
            Spells = [.. _world.Spells.Select(one => one.Name).Where(name => land.Kit.Shows(path, true, name) && !land.Kit.AimsAtEnemy(path, name))],
            EnemySpells = [.. _world.Spells.Select(one => one.Name)
                .Where(name => land.Kit.Shows(path, true, name) && land.Kit.AimsAtEnemy(path, name) && land.Kit.AutoCasts(path, name))],
        };
        _hunt = new HuntProxyRunner(_world, land.Walls, MapGuide.Empty, orders, DateTime.MaxValue, log);
    }

    private async Task GoTown(CancellationToken token)
    {
        _hunt = null;

        if (land.PotionStop is not { } stop)
        {
            log("guide.txt 에 물약 가게가 없습니다.");
            await Task.Delay(TimeSpan.FromSeconds(60), token);
            return;
        }

        int from = _world.State!.Map.Id;
        if (await MoveTo(stop.Map, stop.Where, token))
        {
            Event("move", new { fromMap = from, toMap = stop.Map, why = "town" });
            _life.Arrived(EcoPlace.Town, _clock.Elapsed);
        }
    }

    /// <summary>
    /// 장보기 — 경매장(받기·올리기·사기) → 가방의 더 좋은 장비 입기 → 물약 가게에서 팔기·물약 → 장비 가게마다 살 것 사서 입기. 물약 값의 다음 번 몫은 남긴다.
    /// </summary>
    private async Task Shop(CancellationToken token)
    {
        Doing = "장보기";
        await Auction(token);
        await WearBetter(token);
        long reserve = 0;

        if (land.PotionStop is { } potions && await MoveTo(potions.Map, potions.Where, token) && await Merchant(potions, token) is { } seller)
        {
            await Sell(seller, EcoShopping.ToSell(_world.Pack, keep: []), token);

            if (await Goods(seller, token) is { } goods
                && EcoShopping.PotionsToBuy(goods, _world.Vitals!.Level, _world.Vitals.Gold, EcoShopping.Potions(_world.Pack), Tuning.EcoPotionStock) is { } buy)
            {
                uint price = goods.First(one => one.Name == buy.Name).Price;
                reserve = price * Tuning.EcoPotionStock;
                await Buy(seller, [buy], goods, token);
            }

            await _world.ShutDialogueAsync(token);
        }

        await BuyGear(reserve, token);
        _life.Shopped(EcoShopping.Potions(_world.Pack));
        Event("state", new { from = "Shop", to = "Town", potions = EcoShopping.Potions(_world.Pack) });
    }

    /// <summary>
    /// 판다 — 싼 것부터 열 개씩. 서버는 받을 금화가 들 수 있는 한도(MaxCarryGold)를 넘으면 그 판매를 통째로 거절해(HandleSell), 한꺼번에 팔면
    /// 비싼 것 하나 때문에 아무것도 안 팔리고 가방이 찬 채 마을을 오갔다(10-08 전사봇15, 산타모자 값 5억). 금화가 안 늘면 거기서 멈춘다.
    /// 겹치지 않는 장비도 수량 1 — 0 이면 서버가 줄을 버린다. 값이 없는 것은 서버가 안 사므로 보내지 않는다.
    /// </summary>
    private async Task Sell(Creature seller, IReadOnlyList<InventoryItem> items, CancellationToken token)
    {
        foreach (InventoryItem[] chunk in items.Where(item => EcoAuction.Offer(item) > 0).OrderBy(EcoAuction.Offer).Chunk(10))
        {
            long before = _world.Vitals!.Gold;
            await _world.BulkTradeAsync(seller.Serial, selling: true, [.. chunk.Select(item => (item.Name, item.Slot, Math.Max(1, item.Stacks)))], token);
            bool sold = await Until(() => _world.Vitals!.Gold != before, Answer, token);
            Event("sell", new { items = chunk.Select(item => new { name = item.Name, qty = item.Stacks }), goldBefore = before, goldAfter = _world.Vitals!.Gold });
            if (!sold)
            {
                break;
            }
        }
    }

    /// <summary>장비 가게마다 맞고 지금보다 좋은 것(체력·방어 — <see cref="EcoShopping.Score" />)을 사서 입는다. <paramref name="reserve" /> 금화는 남긴다.</summary>
    private async Task BuyGear(long reserve, CancellationToken token)
    {
        foreach (EcoStop stop in land.GearStops)
        {
            if (!await MoveTo(stop.Map, stop.Where, token) || await Merchant(stop, token) is not { } merchant || await Goods(merchant, token) is not { } goods)
            {
                continue;
            }

            Vitals mine = _world.Vitals!;
            IReadOnlyList<EcoBuy> buys = EcoShopping.GearToBuy(goods, _world.Worn, _world.Path ?? 0, bot.Gender, mine.Level, mine.Gold - reserve, _refused);
            if (buys.Count > 0)
            {
                await Buy(merchant, buys, goods, token);
                await WearBetter(token);
            }

            await _world.ShutDialogueAsync(token);
        }
    }

    private async Task Buy(Creature merchant, IReadOnlyList<EcoBuy> buys, IReadOnlyList<DialogueGoods> goods, CancellationToken token)
    {
        long before = _world.Vitals!.Gold;
        await _world.BulkTradeAsync(merchant.Serial, selling: false, [.. buys.Select(one => (one.Name, 0, one.Quantity))], token);
        await Until(() => _world.Vitals!.Gold != before, Answer, token);
        Event("buy", new
        {
            items = buys.Select(one => new { name = one.Name, qty = one.Quantity, price = goods.First(g => g.Name == one.Name).Price }),
            goldBefore = before,
            goldAfter = _world.Vitals!.Gold,
        });
    }

    /// <summary>
    /// 경매장(설계 <c>autopilot/loot-auction/</c> FR-014·015) — 받을 것을 받아 입고, 못 입는 장비는 올리고(봇당 <see cref="Tuning.EcoAuctionMax" />),
    /// 맞고 지금 것보다 좋은 장비는 즉시 구매해 다시 받아 입는다. 서버가 경매장을 모르면(답 없음) 그냥 지나간다.
    /// </summary>
    private async Task Auction(CancellationToken token)
    {
        if (await AuctionView(() => _world.AuctionClaimsAsync(0, token), token) is not { } claims)
        {
            return;
        }

        // 유찰돼 돌아온 것(까닭 2)은 다시 올리지 않는다 — 아무도 안 사는 값이었다(DL-14). 받기는 모두 받으므로 쪽을 다 본다.
        // ponytail: 봇 프로그램이 다시 켜지면 잊는다 — 그때 한 번 더 올려 보증금(시작가의 2%)만 잃는다. 잦으면 eco 기록에 남긴다.
        int owed = claims.ClaimCount;
        for (ushort at = 1; ; at++)
        {
            _unlisted.UnionWith(claims.Claims.Where(claim => claim.Reason == 2).Select(claim => claim.Name));
            ushort next = at;
            if (at >= claims.Pages || await AuctionView(() => _world.AuctionClaimsAsync(next, token), token) is not { } more)
            {
                break;
            }

            claims = more;
        }

        if (owed > 0 && await AuctionAct(() => _world.AuctionTakeAsync(0, token), token) is null)
        {
            return;
        }

        await WearBetter(token);
        if (await AuctionView(() => _world.AuctionMineAsync(0, token), token) is { } mine)
        {
            int active = mine.Rows.Count(row => (row.Flags & 1) != 0);
            IReadOnlyList<InventoryItem> wear = EcoShopping.ToWear(_world.Pack, _world.Worn, _world.Path ?? 0, _world.Vitals!.Level, _refused);
            foreach (EcoPost post in EcoAuction.ToPost(_world.Pack, wear, active, _unlisted))
            {
                AuctionDone? done = await AuctionAct(() => _world.AuctionPostAsync((byte)post.Item.Slot, post.Start, post.Buyout, 24, token), token);
                if (done is { Ok: true })
                {
                    Event("auction-post", new { name = post.Item.Name, start = post.Start, buyout = post.Buyout });
                }
                else
                {
                    _unlisted.Add(post.Item.Name);
                }
            }
        }

        if (await AuctionView(() => _world.AuctionBrowseAsync(0, 2, 0, string.Empty, token), token) is not { } page)
        {
            return;
        }

        bool bought = false;
        foreach (AuctionRow row in EcoAuction.ToBuy(page.Rows, _world.Worn, _world.Path ?? 0, _world.Vitals!.Level, _world.Vitals.Gold, _refused))
        {
            long before = _world.Vitals.Gold;
            if (await AuctionAct(() => _world.AuctionBuyoutAsync(row.Id, token), token) is { Ok: true })
            {
                bought = true;
                Event("auction-buy", new { name = row.Name, price = row.Buyout, goldBefore = before });
            }
        }

        if (bought)
        {
            await AuctionAct(() => _world.AuctionTakeAsync(0, token), token);
        }
    }

    /// <summary>경매 요청 하나와 그 답(0x5E 9). 같은 접속의 요청은 0.3초 넘게 띄운다(서버가 그보다 잦으면 돌려보낸다).</summary>
    private async Task<AuctionDone?> AuctionAct(Func<Task> send, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(350), token);
        int seen = _world.AuctionDoneCount;
        await send();
        return await Until(() => _world.AuctionDoneCount > seen, Answer, token) ? _world.AuctionDone : null;
    }

    private async Task<AuctionPage?> AuctionView(Func<Task> send, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(350), token);
        int seen = _world.AuctionPageCount;
        await send();
        return await Until(() => _world.AuctionPageCount > seen, Answer, token) ? _world.AuctionPage : null;
    }

    /// <summary>가방에서 지금보다 좋은 장비를 입는다. 서버가 거절해 가방에 남은 것(성별·다른 제한)은 다시 시도하지 않고 다음에 판다.</summary>
    private async Task WearBetter(CancellationToken token)
    {
        foreach (InventoryItem item in EcoShopping.ToWear(_world.Pack, _world.Worn, _world.Path ?? 0, _world.Vitals!.Level, _refused))
        {
            // 두 손이 다 찼으면 약한 쪽을 먼저 벗는다 — 서버는 빈 손에 끼운다(EcoShopping.ToFree).
            if (EcoShopping.ToFree(item.Stats!, _world.Worn) is { } free)
            {
                await _world.TakeOffAsync(free, token);
                await Until(() => _world.Worn.All(one => one.Slot != free), Answer, token);
            }

            // 두손 무기와 방패는 함께 못 든다 — 서버가 다른 쪽을 먼저 벗긴다(Shield.cs·Weapon.cs). 앱은 두손인지 모르니 벗겨지는 것으로 안다.
            int place = item.Stats!.Place;
            WornItem? other = place is 1 or 3 ? _world.Worn.FirstOrDefault(one => one.Slot == (place == 1 ? 3 : 1)) : null;

            // 같은 이름 반지를 하나 더 낄 수 있어 수가 느는지 본다.
            int before = _world.Worn.Count(one => one.Name == item.Name);
            await _world.UseAsync(item.Slot, token);
            bool worn = await Until(() => _world.Worn.Count(one => one.Name == item.Name) > before, Answer, token);

            if (worn)
            {
                Event("equip", new { slot = place, name = item.Name, level = item.Stats.Level });
                if (other is not null && _world.Worn.All(one => one.Slot != other.Slot))
                {
                    // 방패를 버린다 — 다음 WearBetter 가 빈 무기 칸에 두손 무기를 다시 든다(10-08: 둘을 번갈아 사고 껴 사냥을 못 함).
                    // ponytail: 이름으로 기억해 한손 무기로 바꿔도 그 방패는 안 든다 — 봇 프로그램이 다시 켜지면 잊는다.
                    _refused.Add(place == 3 ? item.Name : other.Name);
                }
            }
            else
            {
                _refused.Add(item.Name);
            }
        }
    }

    /// <summary>유령 — 뮤레칸을 눌러 「다음」을 넘기면 살아나 레벨 맞는 마을로 간다.</summary>
    private async Task Revive(CancellationToken token)
    {
        Doing = "부활";
        _hunt = null;
        if (!_dead)
        {
            _dead = true;
            Event("death", new { ground = _ground });
        }

        if (_world.Creatures.FirstOrDefault(one => one.Where == Murekan) is not { } murekan)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            return;
        }

        await _world.ClickAsync(murekan.Serial, token);
        int answered = 0;
        Stopwatch waited = Stopwatch.StartNew();

        while (_world.State?.Map.Id == DeathMap && waited.Elapsed < TimeSpan.FromSeconds(30) && !token.IsCancellationRequested)
        {
            if (_world.TalkCount > answered && _world.Talking is { } talk && talk.Options.FirstOrDefault(option => option.Text == "다음") is { } next)
            {
                answered = _world.TalkCount;
                await _world.AnswerAsync(murekan.Serial, next.Step, token);
            }

            await Task.Delay(Tick, token);
        }

        if (_world.State?.Map.Id != DeathMap)
        {
            _dead = false;
            _life.Revived();
            _life.Arrived(EcoPlace.Town, _clock.Elapsed);
            Event("state", new { from = "Dead", to = "Town", lower = _life.Lower });
        }
    }

    /// <summary>
    /// 걸어서 간다(결정 18, <c>autopilot/eco-bots/walk-SPEC.md</c>) — 같은 맵이면 그 칸 2칸 안으로, 다른 맵이면 워프·월드맵 칸을 이어 밟는다
    /// (<see cref="EcoRoute" />). 걸음 칸이 막히면 그 칸을 빼고 다시 길을 찾고, 길이 없으면 봇 전용 순간이동으로 넘어간다.
    /// 다른 맵으로 갔거나 순간이동했으면 <c>walk</c> 사건 하나.
    /// </summary>
    private async Task<bool> MoveTo(int map, Tile where, CancellationToken token)
    {
        if (_world.State is { } start && start.Map.Id == map && Reckon.Steps(start.Where, where) <= 3)
        {
            return true;
        }

        int from = _world.State?.Map.Id ?? 0, legs = 0, steps = 0;
        Stopwatch took = Stopwatch.StartNew();
        HashSet<(int Map, Tile Where)> avoid = [];
        string why = "길 없음";
        void Walked(bool teleport) =>
            Event("walk", new { from, to = map, legs, steps, seconds = (int)took.Elapsed.TotalSeconds, teleport, why = teleport ? why : null });

        while (legs < 64 && _world.State is { } now && !token.IsCancellationRequested)
        {
            if (now.Map.Id == map)
            {
                // 상인은 계산대 안쪽 막힌 칸에 서 있기도 하다(20041 물약 가게) — 2칸부터 넓혀 가며 걸어서 닿는 칸 중 그 칸에 가까운 곳으로.
                // 서버 거래 거리는 12(WithinRangeProximity)라 10칸까지. 닿는 칸이 없으면(갇힌 자리) 이 맵에 온 것으로 친다.
                HashSet<Tile> exits = [.. land.Links.TilesOn(map)];
                Func<Tile, bool> walls = land.Walls.For(map);
                Tile at = now.Where;
                if (Enumerable.Range(2, 9).Select(reach => (Tile[])[.. Near(where, reach).Where(tile => !exits.Contains(tile))])
                        .FirstOrDefault(near => TabMap.WayToAny(at, near, tile => tile.X < 0 || tile.Y < 0 || walls(tile) || exits.Contains(tile)) is not null)
                    is not { } goals)
                {
                    if (from != map)
                    {
                        Walked(teleport: false);
                    }

                    return true;
                }

                (Walking done, int n) = await Walk(map, goals, warp: false, token);
                steps += n;
                if (done is Walking.Arrived && from != map)
                {
                    Walked(teleport: false);
                }

                if (done is Walking.Arrived or Walking.Down)
                {
                    return done is Walking.Arrived;
                }

                if (done is Walking.Stuck)
                {
                    why = "목표 곁이 막힘";
                    break;
                }

                continue;
            }

            if (EcoRoute.Plan(land.Links, now.Map.Id, map, _world.Vitals?.Level ?? 1, avoid) is not { } leg)
            {
                why = "길 없음";
                break;
            }

            int shown = _world.FieldShown;
            (Walking went, int m) = await Walk(now.Map.Id, leg.Tiles, warp: true, token);
            steps += m;
            if (went is Walking.Down)
            {
                return false;
            }

            // 칸을 밟았는데 맵이 그대로면 월드맵이 열리는 칸이거나 서버가 옮기는 중이다. 레벨이 안 맞으면 서버가 말만 하고 그대로 둔다.
            if (went is Walking.Arrived)
            {
                if (leg.Field != 0 && await Until(() => _world.FieldShown != shown, Answer, token))
                {
                    await _world.ChooseFieldAsync(leg.Field, token);
                }

                went = await Until(() => _world.State?.Map.Id != now.Map.Id, Answer, token) ? Walking.Left : Walking.Stuck;
            }

            if (went is Walking.Stuck)
            {
                avoid.UnionWith(leg.Tiles.Select(tile => (now.Map.Id, tile)));
                continue;
            }

            legs++;
            // 새 맵 — 순간이동 때와 같은 까닭(아래)으로 시야를 다시 받는다.
            await Resync(token);
        }

        if (_world.State is not { } here || here.Map.Id == DeathMap || token.IsCancellationRequested)
        {
            return false;
        }

        why = legs >= 64 ? "워프 64번 넘음" : why;
        Walked(teleport: true);
        int reports = _world.PositionReports;
        await _world.EcoMoveAsync(map, where.X, where.Y, token);
        bool moved = await Until(() => _world.State?.Map.Id == map && _world.PositionReports != reports, Answer, token);

        if (!moved)
        {
            log($"맵 {map} ({where.X},{where.Y}) 로 옮기지 못했습니다(지금 맵 {_world.State?.Map.Id}).");
            return false;
        }

        // 서버는 순간이동(네트워크 스레드)과 시야 갱신(게임 루프)이 엇갈려 새 맵의 상인·괴물(0x07)을 맵(0x15)보다 먼저 보낼 때가 있다 —
        // 알맹이는 맵이 바뀌면 보던 것을 비우므로 그것들이 사라지고, 서버는 이미 보냈다고 여겨 다시 안 보낸다(격리 서버에서 상인이 안 보임).
        // 같은 맵 새로고침(0x38)은 서버가 시야를 다시 보내고 알맹이는 비우지 않는다.
        await _world.RefreshAsync(token);
        return true;
    }

    private enum Walking { Arrived, Left, Stuck, Down }

    /// <summary>시야와 내 칸을 다시 받는다(0x38) — 서버는 새로고침 뒤 RefreshRate(0.3초) 동안 걸음을 말없이 버리므로 그만큼 쉬고 나서 걷는다.</summary>
    private async Task Resync(CancellationToken token)
    {
        await _world.RefreshAsync(token);
        await Task.Delay(TimeSpan.FromMilliseconds(400), token);
    }

    /// <summary>그 칸과 <paramref name="reach" /> 칸 안 — 상인·파티장은 그 칸에 서 있어 곁에 서면 된다.</summary>
    private static IEnumerable<Tile> Near(Tile spot, int reach) =>
        from dx in Enumerable.Range(-reach, (reach * 2) + 1)
        from dy in Enumerable.Range(-reach, (reach * 2) + 1)
        where Math.Abs(dx) + Math.Abs(dy) <= reach
        select new Tile(spot.X + dx, spot.Y + dy);

    /// <summary>
    /// 이 맵 안에서 <paramref name="goals" /> 중 가까운 칸까지 걷는다 — 벽·괴물·사람 칸과 목표가 아닌 워프 칸은 피한다(엉뚱한 맵으로 가지 않게).
    /// 서버는 걸음을 되돌릴 때만 칸을 알려 주므로(0x04) 그때는 서버 칸, 아니면 내가 센 칸(<see cref="HuntProxyRunner" /> 와 같다).
    /// 남은 길이 <see cref="Tuning.EcoWalkStuck" /> 초 동안 안 줄면 막힘, 맵이 바뀌면 떠남, 혼수·죽음이면 쓰러짐. <paramref name="warp" /> 면 목표 칸이
    /// 워프·월드맵 칸이라 밟고 서버가 옮겨 주기를 기다린다.
    /// </summary>
    private async Task<(Walking, int Steps)> Walk(int map, IReadOnlyCollection<Tile> goals, bool warp, CancellationToken token)
    {
        Func<Tile, bool> walls = land.Walls.For(map);
        HashSet<Tile> exits = [.. land.Links.TilesOn(map).Except(goals)];
        Tile here = _world.State?.Where ?? default;
        int reports = -1, steps = 0, best = int.MaxValue;
        Stopwatch stuck = Stopwatch.StartNew();

        while (!token.IsCancellationRequested)
        {
            if (_world.State is not { } now || now.Map.Id == DeathMap || Overhead.InComa(_world.Ailments))
            {
                return (Walking.Down, steps);
            }

            if (now.Map.Id != map)
            {
                return (Walking.Left, steps);
            }

            if (_world.PositionReports != reports)
            {
                reports = _world.PositionReports;
                here = now.Where;
            }

            // 월드맵이 열렸으면 그 칸을 밟은 것이다 — 열린 동안 서버는 걸음을 버린다.
            if (_world.Field is not null || (!warp && goals.Contains(here)))
            {
                return (Walking.Arrived, steps);
            }

            if (stuck.Elapsed > TimeSpan.FromSeconds(Tuning.EcoWalkStuck))
            {
                return (Walking.Stuck, steps);
            }

            if (goals.Contains(here))
            {
                // 워프 칸이면 서버가 곧 옮기거나 월드맵을 연다. 아니면 내가 센 칸이 틀렸다 — 서버는 새로고침 뒤 0.3초(RefreshRate) 동안의 걸음을
                // 말없이 버린다(격리 서버: 46걸음 걸어 월드맵 칸이라 셌는데 한 칸 모자람). 서버 칸을 다시 받아 이어 걷는다.
                if (!await Until(() => _world.State?.Map.Id != map || _world.Field is not null, TimeSpan.FromSeconds(1.5), token))
                {
                    await Resync(token);
                }

                continue;
            }

            HashSet<Tile> taken = [.. _world.Creatures.Where(one => one.Kind != CreatureKind.Passable).Select(one => one.Where), .. _world.Others.Select(one => one.Where)];
            if (TabMap.WayToAny(here, goals, tile => tile.X < 0 || tile.Y < 0 || walls(tile) || exits.Contains(tile) || taken.Contains(tile)) is not { Count: > 0 } way)
            {
                // 길을 막은 괴물·사람이 비키기를 기다린다 — 끝내 안 비키면 막힘.
                await Task.Delay(Tick, token);
                continue;
            }

            if (way.Count < best)
            {
                best = way.Count;
                stuck.Restart();
            }

            await _world.WalkAsync(TabMap.StepOf(here, way[0]), token);
            here = way[0];
            steps++;
            await Task.Delay(TimeSpan.FromSeconds(Tuning.StepSeconds), token);
        }

        return (Walking.Down, steps);
    }

    /// <summary>가게 자리 곁의 상인.</summary>
    private async Task<Creature?> Merchant(EcoStop stop, CancellationToken token)
    {
        Creature? found = null;
        Stopwatch waited = Stopwatch.StartNew();
        bool Seen() => (found = _world.Creatures.FirstOrDefault(one => one.Kind == CreatureKind.Merchant && one.Where == stop.Where)) is not null;

        // 순간이동 직후에는 상인이 몇 초 늦게 오기도 한다(격리 서버 실측 1.6~6.9초) — 2초마다 새로고침하며 12초까지.
        bool seen = false;
        for (int tries = 0; tries < 6 && !(seen = await Until(Seen, TimeSpan.FromSeconds(2), token)); tries++)
        {
            await _world.RefreshAsync(token);
        }

        if (!seen)
        {
            log($"맵 {stop.Map} ({stop.Where.X},{stop.Where.Y}) 에 상인이 안 보입니다. 보이는 것: " +
                string.Join(", ", _world.Creatures.Where(one => one.Kind == CreatureKind.Merchant).Select(one => $"{one.Name}({one.Where.X},{one.Where.Y})")) +
                $" · 나 ({_world.State?.Where.X},{_world.State?.Where.Y}) 맵 {_world.State?.Map.Id}");
        }
        else if (waited.Elapsed > TimeSpan.FromSeconds(1))
        {
            log($"상인이 {waited.Elapsed.TotalSeconds:0.0}초 뒤에 보였습니다(맵 {stop.Map}).");
        }

        return found;
    }

    /// <summary>상인을 눌러 「삽니다」 — 파는 것 목록(이름·값·성별·수치).</summary>
    private async Task<IReadOnlyList<DialogueGoods>?> Goods(Creature merchant, CancellationToken token)
    {
        int talks = _world.TalkCount;
        await _world.ClickAsync(merchant.Serial, token);

        if (!await Until(() => _world.TalkCount > talks && _world.Talking?.Options.Any(option => option.Text == "삽니다") == true, Answer, token))
        {
            return null;
        }

        talks = _world.TalkCount;
        await _world.AnswerAsync(merchant.Serial, _world.Talking!.Options.First(option => option.Text == "삽니다").Step, token);
        return await Until(() => _world.TalkCount > talks && _world.Talking?.Kind == DialogueKind.Goods, Answer, token) ? _world.Talking!.Goods : null;
    }

    private void Event(string ev, object data)
    {
        Tile where = _world.State?.Where ?? default;
        events.Write(EcoLog.Line(DateTime.UtcNow, bot.Name, _world.Path, _world.Vitals, _world.State?.Map.Id ?? 0, where, Doing, ev, data));
    }

    private static async Task<bool> Until(Func<bool> done, TimeSpan within, CancellationToken token)
    {
        Stopwatch waited = Stopwatch.StartNew();

        while (!done())
        {
            if (waited.Elapsed >= within)
            {
                return false;
            }

            await Task.Delay(Tick, token);
        }

        return true;
    }
}
