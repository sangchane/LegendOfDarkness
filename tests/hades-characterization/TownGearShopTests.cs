using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 노비스마을 다음 자리 — 수오미마을과 우드랜드입구에는 장비를 파는 사람이 없었다.
/// 5.99 팩의 상점 목록 25개가 NPC 에 안 묶인 채 남아 있었고, 그중 **장신구 7개 목록**
/// (각반·신발·벨트·귀걸이·방패·반지·전사투구)은 게임 어디에서도 살 수 없었다.
///
/// 셋을 세운다 — `가이@수오미무기점#7,7`(무기) · `아돌@수오미방어구점#4,5`(갑옷) ·
/// `보석상여주인@우드랜드입구#10,15`(장신구). 물목은 5.99 팩, 이름·그림·인사말은 혼든 팩,
/// 자리는 혼든(수오미)과 5.99(우드랜드)의 spawn 줄이다 — `scripts/build-town-gear-shops.py`.
/// 되돌리면(템플릿 세 장을 지우면) 아래 넷이 모두 실패한다.
///
/// **값은 아이템 템플릿에서 읽는다.** 가격·레벨은 다른 작업이 원작 도감으로 되돌리는 중이라
/// 시험에 박아 두면 엉뚱하게 깨진다. 여기서 지키는 것은 **NPC 와 물목**이다.
/// </summary>
public sealed class TownGearShopTests : IDisposable
{
    private const int SuomiTown = 20355;
    private const int SuomiWeaponShop = 20356;
    private const int SuomiArmourShop = 20357;
    private const int WoodlandEntrance = 20028;

    private const string WeaponSmith = "가이@수오미무기점#7,7";
    private const string Armourer = "아돌@수오미방어구점#4,5";
    private const string Jeweller = "보석상여주인@우드랜드입구#10,15";

    /// <summary>혼든 `수오미마을_npc.txt` 의 이미지 30·30·163 — NPC 도 괴물과 같은 번호 체계로 온다(0x4000 을 더한다).</summary>
    private const int WeaponSmithSprite = 0x4000 + 30;
    private const int ArmourerSprite = 0x4000 + 30;
    private const int JewellerSprite = 0x4000 + 163;

    /// <summary>1레벨 공용 무기. 5.99 `전사무기` 목록의 첫 줄이고 다섯 직업이 다 든다.</summary>
    private const string Sword = "에페";

    /// <summary>5.99 `전사갑옷사기` 목록의 전사 갑옷.</summary>
    private const string Mail = "레더메일";

    /// <summary>5.99 `반지사기` 목록의 첫 줄 — 안 묶여 있어 어디서도 못 사던 장신구다.</summary>
    private const string Ring = "로오의반지";

    /// <summary>1~25레벨 다섯 직업이 초반 동선에서 살 것이 있어야 한다.</summary>
    private const int EarlyLevel = 25;

    /// <summary>값이 도감 값으로 오르내리는 중이라 넉넉히 준다.</summary>
    private const uint Purse = 1_000_000;

    /// <summary>수오미무기점은 15x15, 수오미방어구점은 12x12, 우드랜드입구는 40x24 다(`areas/*.json`).</summary>
    private static readonly (int Columns, int Rows) WeaponShopSize = (15, 15);
    private static readonly (int Columns, int Rows) ArmourShopSize = (12, 12);
    private static readonly (int Columns, int Rows) WoodlandSize = (40, 24);

    /// <summary>
    /// 문으로 들어오면 내려놓는 칸. 같은 문칸 수오미마을(11,55) 에 워프가 **둘** 걸려 있어
    /// (`warp … to 수오미무기점(6,13)` · `… (7,13)`) 어느 쪽에 내릴지는 서버가 먼저 읽은 쪽이다.
    /// 그래서 박아 두지 않고 둘 중 하나인지만 본다.
    /// </summary>
    private static readonly Tile[] WeaponDoorsteps = { new(6, 13), new(7, 13) };

    /// <summary>`warp 수오미마을(19,48) to 수오미방어구점(10,8)` — 마을 문으로 들어오면 내려놓는 칸.</summary>
    private static readonly Tile ArmourDoorstep = new(10, 8);

    /// <summary>
    /// 계산대 앞 — 손님이 걸어 닿는 칸 중 상인에게 가장 가까운 칸이다. 상인은 계산대 **뒤**에 서고
    /// 손님 쪽과는 벽으로 갈라져 있다. 서버는 NPC 를 누를 때 거리를 재지 않고
    /// (`GameServerHandlers.Format43Handler`) 12칸까지 보이므로(`LoruleConfig.json` `WithinRangeProximity`)
    /// 3칸이면 눌린다. 서버 상점 12곳이 전부 이 꼴이다.
    /// </summary>
    private static readonly Tile WeaponCounter = new(7, 10);
    private static readonly Tile ArmourCounter = new(4, 8);

    /// <summary>우드랜드입구에는 상점 건물이 없다 — 장신구상이 길가에 선다. 손님은 바로 옆 칸에 선다.</summary>
    private static readonly Tile JewelStall = new(10, 15);
    private static readonly Tile JewelCounter = new(10, 16);

    /// <summary>`warp 우드랜드입구 to world map` — 세계지도에서 내려서는 칸.</summary>
    private static readonly Tile WoodlandLanding = new(10, 23);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    /// <summary>
    /// 수오미마을 (11,56) 에서 북쪽 한 칸이 무기점 문이다. 걸어 들어가 계산대까지 간 다음 가이에게서
    /// 무기를 산다. 길은 서버가 아는 벽(`Area.ParseMapWalls` + `static/sotp.dat`) 그대로 잰다.
    /// </summary>
    [Fact]
    public async Task Walking_in_from_the_suomi_weapon_door_reaches_the_counter_and_buys_across_it()
    {
        const string who = "suomigear";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (SuomiTown, 11, 56));
        server.Start(TimeSpan.FromMinutes(2));

        WorldClient world = await Arrive(server, who, SuomiTown);

        // (11,56) 에서 북쪽 한 칸이 문이다 — 밟으면 서버가 무기점으로 옮긴다.
        await Waiting.WalkUntil(world, Lod.Mobile.Core.Art.Direction.North,
            () => world.State?.Map.Id == SuomiWeaponShop, _deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == SuomiWeaponShop,
            $"수오미무기점({SuomiWeaponShop})으로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        await world.RefreshAsync(_deadline.Token);
        Tile doorstep = world.State!.Where;
        Assert.Contains(doorstep, WeaponDoorsteps);

        Func<Tile, bool> walled = WorldMapTests.Walled(
            server, SuomiWeaponShop, WeaponShopSize.Columns, WeaponShopSize.Rows);

        IReadOnlyList<Tile>? toCounter = Pathing.Way(doorstep, WeaponCounter, walled, reach: 200);
        Assert.True(toCounter is not null, $"{doorstep} 에서 무기 계산대 {WeaponCounter} 로 걸어갈 길이 없습니다.");

        await WorldMapTests.WalkTheWay(world, toCounter!, SuomiWeaponShop, _deadline.Token);
        Assert.Equal(WeaponCounter, world.State!.Where);

        Creature smith = await Standing(world, new Tile(7, 7));
        Assert.Equal(WeaponSmithSprite, smith.Sprite);
        await BuyOne(world, smith, WeaponSmith, Sword);
    }

    /// <summary>
    /// 수오미마을 (20,48) 에서 서쪽 한 칸이 방어구점 문이다. 걸어 들어가 계산대까지 간 다음 아돌에게서
    /// 갑옷을 산다.
    /// </summary>
    /// <remarks>
    /// 무기점 안쪽에도 방어구점으로 가는 문이 있다(`warp 수오미무기점(12,0) to 수오미방어구점(1,9)`).
    /// 한때 그 칸을 밟으면 연결이 끊긴다고 적혀 있었는데, 2026-09-23 에 두 길(무기점 안에서 바로 북쪽으로,
    /// 그리고 마을 문 → 계산대 → 안쪽 문)로 다시 밟아 보니 **둘 다 멀쩡히 넘어간다**. 서버 문제가 아니었다.
    /// 그래도 이 시험은 방어구점을 **따로** 증명하려고 마을 문
    /// (`warp 수오미마을(19,48) to 수오미방어구점(10,8)`)으로 들어간다 — 무기점 시험이 깨져도 같이 깨지지 않는다.
    /// </remarks>
    [Fact]
    public async Task Walking_in_from_the_suomi_armour_door_reaches_the_counter_and_buys_across_it()
    {
        const string who = "suomiarm";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (SuomiTown, 20, 48));
        server.Start(TimeSpan.FromMinutes(2));

        WorldClient world = await Arrive(server, who, SuomiTown);

        // (20,48) 에서 서쪽 한 칸이 문이다 — 밟으면 서버가 방어구점으로 옮긴다.
        await Waiting.WalkUntil(world, Lod.Mobile.Core.Art.Direction.West,
            () => world.State?.Map.Id == SuomiArmourShop, _deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == SuomiArmourShop,
            $"수오미방어구점({SuomiArmourShop})으로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        await world.RefreshAsync(_deadline.Token);
        Assert.Equal(ArmourDoorstep, world.State!.Where);

        Func<Tile, bool> walled = WorldMapTests.Walled(
            server, SuomiArmourShop, ArmourShopSize.Columns, ArmourShopSize.Rows);

        IReadOnlyList<Tile>? toArmour = Pathing.Way(ArmourDoorstep, ArmourCounter, walled, reach: 200);
        Assert.True(toArmour is not null, $"{ArmourDoorstep} 에서 갑옷 계산대 {ArmourCounter} 로 걸어갈 길이 없습니다.");

        await WorldMapTests.WalkTheWay(world, toArmour!, SuomiArmourShop, _deadline.Token);
        Assert.Equal(ArmourCounter, world.State!.Where);

        Creature armourer = await Standing(world, new Tile(4, 5));
        Assert.Equal(ArmourerSprite, armourer.Sprite);
        await BuyOne(world, armourer, Armourer, Mail);
    }

    /// <summary>
    /// 우드랜드입구는 세계지도에서 내려서는 사냥터 입구다. 내려선 칸(10,23)에서 걸어 올라가 장신구상에게
    /// 말을 걸고 반지를 산다 — 5.99 팩이 NPC 에 묶지 않아 게임에 없던 장신구다.
    /// </summary>
    [Fact]
    public async Task Walking_up_from_the_woodland_landing_buys_a_trinket_from_the_jeweller()
    {
        const string who = "woodgear";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandEntrance, WoodlandLanding.X, WoodlandLanding.Y));
        server.Start(TimeSpan.FromMinutes(2));

        WorldClient world = await Arrive(server, who, WoodlandEntrance);

        Func<Tile, bool> walled = WorldMapTests.Walled(
            server, WoodlandEntrance, WoodlandSize.Columns, WoodlandSize.Rows);

        IReadOnlyList<Tile>? way = Pathing.Way(WoodlandLanding, JewelCounter, walled, reach: 400);
        Assert.True(way is not null, $"{WoodlandLanding} 에서 장신구 가게 앞 {JewelCounter} 로 걸어갈 길이 없습니다.");

        await WorldMapTests.WalkTheWay(world, way!, WoodlandEntrance, _deadline.Token);
        Assert.Equal(JewelCounter, world.State!.Where);

        Creature jeweller = await Standing(world, JewelStall);
        Assert.Equal(JewellerSprite, jeweller.Sprite);

        await BuyOne(world, jeweller, Jeweller, Ring);
    }

    /// <summary>
    /// 파는 물건이 전부 아이템 템플릿에 있어야 하고(끊긴 참조 0), 다섯 직업이 1~25레벨에 살 것이 있어야 한다.
    /// 서버는 없는 이름을 조용히 건너뛰므로(`shop1.cs` 의 `defaultbag` 이 null 을 거른다) 창에는 아무 말도 안 나온다.
    /// </summary>
    [Fact]
    public void The_town_gear_shops_sell_only_things_that_exist_and_cover_every_class()
    {
        Dictionary<string, JsonNode> items = Items();

        foreach ((string shop, int[] slots) in new[]
                 {
                     (WeaponSmith, new[] { 1 }),
                     (Armourer, new[] { 2 }),
                     (Jeweller, AccessorySlots),
                 })
        {
            JsonNode keeper = Keeper(shop);
            Assert.Equal("shop1", keeper["ScriptKey"]!.GetValue<string>());

            string[] stock = Stock(shop);
            Assert.NotEmpty(stock);

            string[] missing = stock.Where(one => !items.ContainsKey(one)).ToArray();
            Assert.True(missing.Length == 0, $"{shop} 가 없는 물건을 팝니다: {string.Join(", ", missing)}");

            // 서버는 자기 직업이거나 공용(0)일 때만 끼워 준다(`GameClient.cs:171`).
            foreach (int career in new[] { 1, 2, 3, 4, 5 })
            {
                string[] wearable = stock.Where(one =>
                    items[one]["EquipmentSlot"]?.GetValue<int>() is int slot && slots.Contains(slot)
                    && items[one]["LevelRequired"]?.GetValue<int>() <= EarlyLevel
                    && items[one]["Class"]?.GetValue<int>() is int only && (only == career || only == 0)).ToArray();

                Assert.True(wearable.Length > 0,
                    $"{shop}: 직업 {career} 가 1~{EarlyLevel}레벨에 살 수 있는 것이 없습니다. 파는 것: {string.Join(", ", stock)}");
            }
        }
    }

    /// <summary>
    /// 5.99 팩이 NPC 에 묶지 않고 남긴 장신구 목록 — 각반·신발·벨트·귀걸이·방패·반지(+전사투구)。
    /// 장신구 칸은 방패 3 · 투구 4 · 귀걸이 5 · 목걸이 6 · 반지 7 · 장갑 9 · 벨트 11 · 각반 12 · 신발 13 이다.
    /// 그중 **1~25레벨에 낄 수 있는 칸**이 실제로 팔리는지 본다.
    /// </summary>
    [Fact]
    public void The_woodland_jeweller_covers_the_accessory_slots_the_pack_left_unbound()
    {
        Dictionary<string, JsonNode> items = Items();
        string[] stock = Stock(Jeweller);

        // 5.99 목록이 1~25레벨에 실제로 주는 칸들이다(반지·장갑·신발·벨트·각반·방패·귀걸이).
        foreach (int slot in new[] { 3, 5, 7, 9, 11, 12, 13 })
        {
            string[] got = stock.Where(one =>
                items.TryGetValue(one, out JsonNode? made)
                && made["EquipmentSlot"]?.GetValue<int>() == slot
                && made["LevelRequired"]?.GetValue<int>() <= EarlyLevel).ToArray();

            Assert.True(got.Length > 0,
                $"{Jeweller}: {slot}번 칸에 1~{EarlyLevel}레벨이 낄 것이 없습니다. 파는 것: {string.Join(", ", stock)}");
        }

        // 값이 0 이면 shop1 이 공짜로 내준다(`shop1.cs:149` `GoldPoints >= Value`).
        string[] free = stock.Where(one => items[one]["Value"]?.GetValue<int>() <= 0).ToArray();
        Assert.True(free.Length == 0, $"{Jeweller} 가 값 0 인 것을 팝니다(공짜로 나갑니다): {string.Join(", ", free)}");
    }

    private static readonly int[] AccessorySlots = { 3, 4, 5, 6, 7, 9, 11, 12, 13 };

    /// <summary>로그인해 그 맵에 서고, 살 돈을 쥐여 준다.</summary>
    private async Task<WorldClient> Arrive(IsolatedHadesServer server, string who, int map)
    {
        LoginFlow.TryCreateAccount(server, who);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["GoldPoints"] = Purse;
        File.WriteAllText(saved, character.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == map,
            $"맵 {map} 에 들어가지 못했습니다. 마지막: {world.State}", _deadline.Token);

        return world;
    }

    /// <summary>상인을 눌러 창을 열고 하나 사서, 가방에 들어오고 금화가 그만큼 주는지 본다.</summary>
    private async Task BuyOne(WorldClient world, Creature keeper, string keeperName, string goods)
    {
        uint price = (uint)Items()[goods]["Value"]!.GetValue<int>();
        long purse = world.Vitals?.Gold ?? 0L;

        Assert.True(price > 0, $"{goods} 의 값이 0 이라 상점이 공짜로 내줍니다.");
        Assert.True(purse >= price, $"{goods}({price}골드)을 살 돈이 모자랍니다. 지금 {purse}골드.");

        int opened = world.TalkCount;
        await world.ClickAsync(keeper.Serial, _deadline.Token);
        Dialogue menu = await Window(world, opened, talk => talk.Options.Count > 0);

        Assert.Equal((keeperName, DialogueKind.Options), (menu.Who, menu.Kind));

        // 상점 첫 창은 삽니다·팝니다·수리합니다 셋이다(`shop1.cs:24-28`).
        DialogueOption buy = Assert.Single(menu.Options, option => option.Text == "삽니다");

        opened = world.TalkCount;
        await world.AnswerAsync(keeper.Serial, buy.Step, _deadline.Token);
        Dialogue shelf = await Window(world, opened, talk => talk.Kind == DialogueKind.Goods);

        DialogueGoods wanted = Assert.Single(shelf.Goods, one => one.Name == goods);
        Assert.Equal(price, wanted.Price);

        await world.AnswerAsync(keeper.Serial, shelf.Step, wanted.Name, _deadline.Token);
        await Waiting.Until(
            () => world.Pack.Any(item => item.Name == goods) && world.Vitals?.Gold == purse - price,
            $"{goods}을 사지 못했습니다. 소지품: {string.Join(", ", world.Pack.Select(item => item.Name))} · "
            + $"금화 {world.Vitals?.Gold} · 창: {world.Talking?.What}",
            _deadline.Token);
    }

    private static JsonNode Keeper(string shop)
    {
        string path = Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "mundanes", $"{shop}.json");

        Assert.True(File.Exists(path), $"{shop} 템플릿이 없습니다: {path}");

        return JsonNode.Parse(File.ReadAllText(path))!;
    }

    private static string[] Stock(string shop) =>
        Keeper(shop)["DefaultMerchantStock"]!.AsArray().Select(one => one!.GetValue<string>()).ToArray();

    private static Dictionary<string, JsonNode> Items()
    {
        Dictionary<string, JsonNode> found = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(
                     Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "items"), "*.json"))
        {
            JsonNode? template;

            try
            {
                template = JsonNode.Parse(File.ReadAllText(file));
            }
            catch (JsonException)
            {
                continue;
            }

            if (template?["Name"]?.GetValue<string>() is { } name)
            {
                found[name] = template;
            }
        }

        return found;
    }

    private async Task<Dialogue> Window(WorldClient world, int seen, Func<Dialogue, bool> wanted)
    {
        await Waiting.Until(() => world.TalkCount > seen && world.Talking is { } talk && wanted(talk),
            $"기다린 창이 오지 않았습니다. 마지막 창: {world.Talking?.Kind} {world.Talking?.What}", _deadline.Token);

        return world.Talking!;
    }

    /// <summary>
    /// 그 칸에 선 사람을 집는다. 서버는 둘레의 것들을 **들어갈 때 한 번** 보내고(`GameClient.EnterArea`),
    /// 그 한 번을 놓치면 가만히 기다리는 쪽으로는 영영 오지 않는다 — 다시 보내 달라는 0x38 도
    /// 이미 새로 고치는 중이면 조용히 버려진다(`GameServerHandlers.Format38Handler` 의 `IsRefreshing`).
    /// 다섯 번에 한 번쯤 아무도 못 보고 시험이 깨지던 까닭이다(2026-09-23). 그래서 기다리는 동안
    /// 1초마다 다시 달라고 한다.
    /// </summary>
    private async Task<Creature> Standing(WorldClient world, Tile where)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Creatures.FirstOrDefault(one => one.Where == where) is { } found)
            {
                return found;
            }

            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        throw new TimeoutException(
            $"{where} 에 아무도 없습니다. 본 것: "
            + string.Join(", ", world.Creatures.Select(one => $"{one.Serial}@{one.Where}")));
    }
}
