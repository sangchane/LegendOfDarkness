using System.Diagnostics;
using System.Net;
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
/// (<see cref="HuntProxyRunner" />), 옮기기는 봇 전용 순간이동(0xF1 8), 장보기는 가게를 차례로 돌며 입기·팔기·물약·장비.
/// 일마다 사건 한 줄(<see cref="EcoLog" />). 접속이 끊기면 돌아온다(다시 들이기는 <see cref="EcoHost" />).
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
    private WorldClient _world = null!;
    private HuntProxyRunner? _hunt;
    private int _ground;
    private TimeSpan? _personSince;
    private TimeSpan _nextTick = TimeSpan.FromMinutes(1);
    private long _lastExp = -1;
    private TimeSpan _lastKill;
    private int _lastLevel;
    private bool _dead;

    public string Name => bot.Name;

    /// <summary>지금 맵(요약·사냥터 나누기용). 접속 전이면 0.</summary>
    public int Map => _world?.State?.Map.Id ?? 0;

    public int Level => _world?.Vitals?.Level ?? 0;

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

        if (StatPlan.Next(_world.Path, vitals) is { } stat)
        {
            await _world.RaiseAsync(stat, token);
        }

        EcoSight sight = new(
            now,
            Overhead.InComa(_world.Ailments),
            Ghost: map == DeathMap,
            EcoShopping.Potions(_world.Pack),
            Tuning.PackSlots - _world.Pack.Count,
            _hunt is null || _hunt.Stopped is not null,
            _personSince,
            vitals.MaximumHealth > 0 ? vitals.Health * 100 / vitals.MaximumHealth : 100);

        EcoAct act = _life.Next(sight);
        if (act != EcoAct.Hunt)
        {
            Doing = act.ToString();
        }

        switch (act)
        {
            case EcoAct.Hunt:
                Doing = "사냥";
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

    private static string Why(EcoSight sight) =>
        sight.HuntStopped ? "stopped"
        : sight.Potions < Tuning.EcoPotionLow ? "potions"
        : sight.FreeSlots < Tuning.EcoBagLow ? "bag"
        : "time";

    private async Task GoHunt(EcoSight sight, CancellationToken token)
    {
        int level = _world.Vitals!.Level;
        int[] avoid = _personSince is not null ? [_ground] : [];
        if (EcoGrounds.Pick(land.Grounds, level, map => host.BotsOn(map, bot.Name), Tuning.EcoBotsPerMap, avoid, _life.Lower) is not { } ground)
        {
            Doing = "사냥터 없음";
            await Task.Delay(TimeSpan.FromSeconds(60), token);
            return;
        }

        Func<Tile, bool> blocked = land.Walls.For(ground.Map);
        Random dice = Random.Shared;
        Tile spot = Enumerable.Range(0, 500).Select(_ => new Tile(dice.Next(2, 100), dice.Next(2, 100))).FirstOrDefault(tile => !blocked(tile), new Tile(10, 10));

        int from = _world.State!.Map.Id;
        if (!await MoveTo(ground.Map, spot, token))
        {
            return;
        }

        Event("move", new { fromMap = from, toMap = ground.Map, why = "hunt", ground = ground.Name, groundLevel = ground.Level });
        _ground = ground.Map;
        _personSince = null;
        _life.Arrived(EcoPlace.Hunting, _clock.Elapsed);

        // 대신 사냥과 같은 판단 — 기술·마법은 직업 기술 표(class-kit.txt)에 보이는 것, 물약은 가방의 가장 센 체력 물약.
        int? path = _world.Path;
        ProxyOrders orders = new()
        {
            Radius = 12,
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
        _hunt = new HuntProxyRunner(_world, land.Walls, MapGuide.Empty, orders, DateTime.MaxValue);
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
    /// 장보기 — 가방의 더 좋은 장비 입기 → 물약 가게에서 팔기·물약 → 장비 가게마다 살 것 사서 입기. 물약 값의 다음 번 몫은 남긴다.
    /// </summary>
    private async Task Shop(CancellationToken token)
    {
        Doing = "장보기";
        await WearBetter(token);
        long reserve = 0;

        if (land.PotionStop is { } potions && await MoveTo(potions.Map, potions.Where, token) && await Merchant(potions, token) is { } seller)
        {
            IReadOnlyList<InventoryItem> sell = EcoShopping.ToSell(_world.Pack, keep: []);
            if (sell.Count > 0)
            {
                long before = _world.Vitals!.Gold;
                await _world.BulkTradeAsync(seller.Serial, selling: true, [.. sell.Select(item => (item.Name, item.Slot, item.Stacks))], token);
                await Until(() => _world.Vitals!.Gold != before, Answer, token);
                Event("sell", new { items = sell.Select(item => new { name = item.Name, qty = item.Stacks }), goldBefore = before, goldAfter = _world.Vitals!.Gold });
            }

            if (await Goods(seller, token) is { } goods
                && EcoShopping.PotionsToBuy(goods, _world.Vitals!.Level, _world.Vitals.Gold, EcoShopping.Potions(_world.Pack), Tuning.EcoPotionStock) is { } buy)
            {
                uint price = goods.First(one => one.Name == buy.Name).Price;
                reserve = price * Tuning.EcoPotionStock;
                await Buy(seller, [buy], goods, token);
            }

            await _world.ShutDialogueAsync(token);
        }

        foreach (EcoStop stop in land.GearStops)
        {
            if (!await MoveTo(stop.Map, stop.Where, token) || await Merchant(stop, token) is not { } merchant || await Goods(merchant, token) is not { } goods)
            {
                continue;
            }

            Vitals mine = _world.Vitals!;
            IReadOnlyList<EcoBuy> buys = EcoShopping.GearToBuy(goods, _world.Worn, _world.Path ?? 0, bot.Gender, mine.Level, mine.Gold - reserve);
            if (buys.Count > 0)
            {
                await Buy(merchant, buys, goods, token);
                await WearBetter(token);
            }

            await _world.ShutDialogueAsync(token);
        }

        _life.Shopped(EcoShopping.Potions(_world.Pack));
        Event("state", new { from = "Shop", to = "Town", potions = EcoShopping.Potions(_world.Pack) });
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

    /// <summary>가방에서 지금보다 좋은 장비를 입는다. 서버가 거절해 가방에 남은 것(성별·다른 제한)은 다시 시도하지 않고 다음에 판다.</summary>
    private async Task WearBetter(CancellationToken token)
    {
        foreach (InventoryItem item in EcoShopping.ToWear(_world.Pack, _world.Worn, _world.Path ?? 0, _world.Vitals!.Level, _refused))
        {
            await _world.UseAsync(item.Slot, token);
            bool worn = await Until(() => _world.Worn.Any(one => one.Name == item.Name), Answer, token);

            if (worn)
            {
                Event("equip", new { slot = item.Stats!.Place, name = item.Name, level = item.Stats.Level });
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

    /// <summary>봇 전용 순간이동 — 그 맵 그 칸 곁에 설 때까지 기다린다(서버가 빈 칸으로 옮겨 놓는다).</summary>
    private async Task<bool> MoveTo(int map, Tile where, CancellationToken token)
    {
        if (_world.State is { } now && now.Map.Id == map && Reckon.Steps(now.Where, where) <= 3)
        {
            return true;
        }

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
