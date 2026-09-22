using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 세상에 1차 직업 무기·갑옷을 파는 NPC 가 한 명도 없었다. 상점 틀(`shop1.cs`)과 모바일 상점 창은 이미
/// 돌았지만 파는 것이 포션·음식·귀환서뿐이라, 1~25레벨이 장비를 얻을 길이 없었다.
///
/// 노비스마을 옆 무기방어구상점(20375)에 둘을 세운다 — `델란`(무기)·`드보이`(갑옷).
/// 물목은 5.99 팩의 목록, 자리·이름·그림·인사말은 혼든 팩이다(`scripts/build-novice-gear-shops.py`).
/// 되돌리면(템플릿 두 장을 지우면) 아래 셋이 모두 실패한다.
/// </summary>
public sealed class NoviceGearShopTests : IDisposable
{
    private const int GearShopId = 20375;

    private const string WeaponSmith = "델란@노비스무기방어구상점#3,7";
    private const string Armourer = "드보이@노비스무기방어구상점#6,2";

    /// <summary>혼든 `노비스마을_npc.txt` 의 이미지 56·30 — NPC 도 괴물과 같은 번호 체계로 온다(0x4000 을 더한다).</summary>
    private const int WeaponSmithSprite = 0x4000 + 56;
    private const int ArmourerSprite = 0x4000 + 30;

    /// <summary>1레벨 도적 무기. 5.99 `도적무기` 목록의 첫 줄이고 값은 아이템 템플릿의 `Value` 다.</summary>
    private const string Dagger = "설단검";
    private const uint DaggerPrice = 500;

    /// <summary>21레벨 전사 갑옷. 5.99 `전사갑옷사기` 목록에 있다.</summary>
    private const string Mail = "레더메일";
    private const uint MailPrice = 3000;

    /// <summary>1레벨 전사 갑옷. 혼든 드보이 목록에서 왔다 — 5.99 갑옷은 전부 21레벨부터라 1~20레벨이 입을 것이 없었다.</summary>
    private const string Tunic = "레더튜닉";
    private const uint TunicPrice = 300;

    /// <summary>1레벨 무도가 너클. 노바 팩에서 들여왔다(`scripts/build-nova-knuckles.py`).</summary>
    private const string Knuckle = "글러브1";
    private const uint KnucklePrice = 500;

    /// <summary>11레벨 무도가 너클. 같은 사다리의 두 번째 칸.</summary>
    private const string BetterKnuckle = "견습자의글러브";

    /// <summary>사람이 1~20레벨에 입고 드는 것을 다섯 직업 모두 살 수 있어야 한다.</summary>
    private const int StarterLevel = 20;

    private const uint Purse = 10000;

    /// <summary>노비스마을 — 상점 문이 (50,25) 에 있고 문 안쪽에서 나오면 (50,26) 에 선다.</summary>
    private const int NoviceTown = 20373;

    /// <summary>상점방은 20x20 이다(`areas/노비스무기방어구상점.json`).</summary>
    private const int ShopColumns = 20;
    private const int ShopRows = 20;

    /// <summary>문으로 들어오면 내려놓는 칸(`warps/warp 노비스마을(50,25) to 노비스무기방어구상점(9,18)`).</summary>
    private static readonly Tile Doorstep = new(9, 18);

    /// <summary>
    /// 계산대 앞 — 손님이 설 수 있는 칸 중 상인에게 가장 가까운 칸이다. 상인은 계산대 **뒤**에 서고
    /// 손님 쪽과는 벽으로 갈라져 있다(그래서 상인 칸까지 걸어갈 수는 없다). 서버는 NPC 를 누를 때
    /// 거리를 재지 않고(`GameServerHandlers.Format43Handler`), 눈에 보이는 거리는 12칸이다
    /// (`LoruleConfig.json` `WithinRangeProximity`) — 3칸이면 보이고 눌린다.
    /// 상점 12곳이 전부 이 꼴이다(상인과 손님 칸 사이 거리가 하나같이 3칸).
    /// </summary>
    private static readonly Tile WeaponCounter = new(6, 7);
    private static readonly Tile ArmourCounter = new(6, 5);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Buying_a_weapon_from_the_novice_weapon_shop_puts_it_in_the_pack()
    {
        await Bought("gearwpn", (3, 8), new Tile(3, 7), WeaponSmith, WeaponSmithSprite, Dagger, DaggerPrice);
    }

    [Fact]
    public async Task Buying_armour_from_the_novice_armour_shop_puts_it_in_the_pack()
    {
        await Bought("geararm", (6, 3), new Tile(6, 2), Armourer, ArmourerSprite, Mail, MailPrice);
    }

    /// <summary>
    /// 노비스마을에서 상점 문을 밟고 들어가 **걸어서** 계산대까지 간 다음, 계산대 너머로 무기상·갑옷상에게
    /// 말을 걸어 산다. 앞서 "상점방 안쪽이 입구에서 닿지 않는다"고 적힌 일이 있는데, 닿지 않는 것은 상인이
    /// 선 칸이고 **계산대 앞까지는 닿는다** — 원작이 그런 꼴이다(5.99·혼든·노바 세 팩이 모두 상인을 계산대
    /// 뒤에 세웠다). 이 시험이 그것을 실제로 걸어서 확인한다.
    /// </summary>
    [Fact]
    public async Task Walking_in_from_the_town_door_reaches_both_counters_and_buys_across_them()
    {
        const string who = "gearwalk";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTown, 50, 26));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, who);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["GoldPoints"] = Purse;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == NoviceTown,
            $"노비스마을({NoviceTown})에 들어가지 못했습니다. 마지막: {world.State}", _deadline.Token);

        // (50,26) 에서 북쪽 한 칸이 문이다 — 밟으면 서버가 상점방으로 옮긴다.
        await Waiting.WalkUntil(world, Lod.Mobile.Core.Art.Direction.North,
            () => world.State?.Map.Id == GearShopId, _deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == GearShopId,
            $"상점 문을 밟았는데 무기방어구상점({GearShopId})으로 가지 않았습니다. 마지막: {world.State}", _deadline.Token);

        await world.RefreshAsync(_deadline.Token);
        Assert.Equal(Doorstep, world.State!.Where);

        // 서버가 아는 벽 그대로 잰다(`Area.ParseMapWalls`).
        Func<Tile, bool> blocked = WorldMapTests.Walled(server, GearShopId, ShopColumns, ShopRows);

        IReadOnlyList<Tile>? toWeapons = Pathing.Way(Doorstep, WeaponCounter, blocked, reach: 100);
        IReadOnlyList<Tile>? toArmour = Pathing.Way(WeaponCounter, ArmourCounter, blocked, reach: 100);

        Assert.True(toWeapons is not null, $"{Doorstep} 에서 무기 계산대 {WeaponCounter} 로 걸어갈 길이 없습니다.");
        Assert.True(toArmour is not null, $"{WeaponCounter} 에서 갑옷 계산대 {ArmourCounter} 로 걸어갈 길이 없습니다.");

        await WorldMapTests.WalkTheWay(world, toWeapons!, GearShopId, _deadline.Token);
        Assert.Equal(WeaponCounter, world.State!.Where);

        Creature smith = await Standing(world, new Tile(3, 7));
        Assert.Equal(WeaponSmithSprite, smith.Sprite);

        await BuyOne(world, smith, WeaponSmith, Dagger, DaggerPrice, Purse);
        await BuyOne(world, smith, WeaponSmith, Knuckle, KnucklePrice, Purse - DaggerPrice);

        await WorldMapTests.WalkTheWay(world, toArmour!, GearShopId, _deadline.Token);
        Assert.Equal(ArmourCounter, world.State!.Where);

        Creature armourer = await Standing(world, new Tile(6, 2));
        Assert.Equal(ArmourerSprite, armourer.Sprite);

        await BuyOne(world, armourer, Armourer, Tunic, TunicPrice, Purse - DaggerPrice - KnucklePrice);

        Assert.Equal(
            Purse - DaggerPrice - KnucklePrice - TunicPrice,
            world.Vitals?.Gold);
    }

    /// <summary>
    /// 5.99 갑옷은 전부 21레벨부터다 — 1~20레벨은 벗고 다녀야 했다. 혼든 팩 드보이 목록의 원작 옷
    /// (레더튜닉·도복·스카웃튜닉 …)으로 채운다. 다섯 직업 모두 20레벨 전에 입을 것이 있어야 한다.
    /// </summary>
    [Fact]
    public void Every_class_can_dress_before_level_twenty()
    {
        Assert.All(Careers(Armourer, slot: 2, upTo: StarterLevel), got =>
            Assert.True(got.Wearable.Length > 0,
                $"직업 {got.Career} 가 1~{StarterLevel}레벨에 입을 갑옷이 없습니다. 파는 것: {string.Join(", ", got.Stock)}"));
    }

    /// <summary>
    /// 무도가가 1~20레벨에 들 무기가 공용 에페 하나뿐이었다. 노바 팩의 너클 사다리 아래 두 칸
    /// (글러브1 1레벨 · 견습자의글러브 11레벨)을 들여와 델란이 판다.
    /// </summary>
    [Fact]
    public void The_weaponsmith_sells_monk_knuckles_for_the_first_twenty_levels()
    {
        Dictionary<string, JsonNode> items = Templates(
            Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "items"));

        string[] stock = Stock(WeaponSmith);

        foreach ((string name, int level) in new[] { (Knuckle, 1), (BetterKnuckle, 11) })
        {
            Assert.True(items.ContainsKey(name), $"{name} 아이템 템플릿이 없습니다.");

            JsonNode made = items[name];

            Assert.Equal(1, made["EquipmentSlot"]!.GetValue<int>());
            Assert.Equal(5, made["Class"]!.GetValue<int>());
            Assert.Equal(level, made["LevelRequired"]!.GetValue<int>());

            // 값이 0 이면 shop1 이 공짜로 내준다(`shop1.cs:149` `GoldPoints >= Value`).
            Assert.True(made["Value"]!.GetValue<int>() > 0, $"{name} 의 값이 0 이라 상점이 공짜로 내줍니다.");

            Assert.Contains(name, stock);
        }
    }

    /// <summary>
    /// 파는 물건이 전부 아이템 템플릿에 있어야 한다(끊긴 참조 0), 그리고 다섯 직업이 1~25레벨에 살 것이 있어야 한다.
    /// 서버는 없는 이름을 조용히 건너뛰므로(`shop1.cs` 의 `defaultbag` 이 null 을 거른다) 창에는 아무 말도 안 나온다.
    /// </summary>
    [Fact]
    public void The_novice_gear_shops_sell_only_things_that_exist_and_cover_every_class()
    {
        string templates = Path.Combine(HadesWorkspace.ServerDataDirectory, "templates");
        Dictionary<string, JsonNode> items = Templates(Path.Combine(templates, "items"));

        foreach ((string shop, int slot) in new[] { (WeaponSmith, 1), (Armourer, 2) })
        {
            string path = Path.Combine(templates, "mundanes", $"{shop}.json");
            Assert.True(File.Exists(path), $"{shop} 템플릿이 없습니다: {path}");

            JsonNode keeper = JsonNode.Parse(File.ReadAllText(path))!;
            Assert.Equal("shop1", keeper["ScriptKey"]!.GetValue<string>());

            string[] stock = keeper["DefaultMerchantStock"]!.AsArray().Select(one => one!.GetValue<string>()).ToArray();
            Assert.NotEmpty(stock);

            string[] missing = stock.Where(one => !items.ContainsKey(one)).ToArray();
            Assert.True(missing.Length == 0, $"{shop} 가 없는 물건을 팝니다: {string.Join(", ", missing)}");

            // 서버는 자기 직업이거나 공용(0)일 때만 끼워 준다(GameClient.cs:171).
            foreach (int career in new[] { 1, 2, 3, 4, 5 })
            {
                string[] wearable = stock.Where(one =>
                    items[one]["EquipmentSlot"]?.GetValue<int>() == slot
                    && items[one]["LevelRequired"]?.GetValue<int>() <= 25
                    && items[one]["Class"]?.GetValue<int>() is int only && (only == career || only == 0)).ToArray();

                Assert.True(wearable.Length > 0,
                    $"{shop}: 직업 {career} 가 1~25레벨에 살 수 있는 것이 없습니다. 파는 것: {string.Join(", ", stock)}");
            }
        }
    }

    private static Dictionary<string, JsonNode> Templates(string directory)
    {
        Dictionary<string, JsonNode> found = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
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

    /// <summary>선 자리 옆에서 상점을 열고 하나 사서, 가방에 들어오고 금화가 그만큼 주는지 본다.</summary>
    private async Task Bought(
        string who, (int X, int Y) standing, Tile keeperAt, string keeperName, int sprite, string goods, uint price)
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (GearShopId, standing.X, standing.Y));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, who);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["GoldPoints"] = Purse;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Waiting.Until(() => world.State?.Map.Id == GearShopId,
            $"노비스무기방어구상점({GearShopId})에 들어가지 못했습니다. 마지막: {world.State}", _deadline.Token);

        Creature keeper = await Standing(world, keeperAt);

        // 그림 번호가 괴물과 같은 체계여야 클라이언트가 mns### 을 찾아 그리고 누를 수 있다.
        Assert.Equal(sprite, keeper.Sprite);

        await BuyOne(world, keeper, keeperName, goods, price, Purse);
    }

    /// <summary>
    /// 상인을 눌러 창을 열고 하나 사서, 가방에 들어오고 금화가 그만큼 주는지 본다.
    /// <paramref name="purse" /> 는 사기 **전**의 금화다.
    /// </summary>
    private async Task BuyOne(
        WorldClient world, Creature keeper, string keeperName, string goods, uint price, uint purse)
    {
        int opened = world.TalkCount;
        await world.ClickAsync(keeper.Serial, _deadline.Token);
        Dialogue menu = await Window(world, opened, talk => talk.Options.Count > 0);

        Assert.Equal((keeperName, DialogueKind.Options), (menu.Who, menu.Kind));

        // 상점 첫 창은 삽니다·팝니다·수리합니다 셋이다(`shop1.cs:24-28`). 2026-09-18 에 한국어로 바뀌었다.
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

    /// <summary>상점 템플릿이 파는 이름들.</summary>
    private static string[] Stock(string shop)
    {
        string path = Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "mundanes", $"{shop}.json");

        Assert.True(File.Exists(path), $"{shop} 템플릿이 없습니다: {path}");

        JsonNode keeper = JsonNode.Parse(File.ReadAllText(path))!;

        return keeper["DefaultMerchantStock"]!.AsArray().Select(one => one!.GetValue<string>()).ToArray();
    }

    /// <summary>
    /// 직업마다 그 상점에서 <paramref name="upTo" /> 레벨까지 실제로 낄 수 있는 것.
    /// 서버는 자기 직업이거나 공용(0)일 때만 끼워 준다(`GameClient.cs:171`).
    /// </summary>
    private static IEnumerable<(int Career, string[] Stock, string[] Wearable)> Careers(string shop, int slot, int upTo)
    {
        Dictionary<string, JsonNode> items = Templates(
            Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "items"));

        string[] stock = Stock(shop);

        foreach (int career in new[] { 1, 2, 3, 4, 5 })
        {
            yield return (career, stock, stock.Where(one =>
                items.TryGetValue(one, out JsonNode? made)
                && made["EquipmentSlot"]?.GetValue<int>() == slot
                && made["LevelRequired"]?.GetValue<int>() <= upTo
                && made["Class"]?.GetValue<int>() is int only && (only == career || only == 0)).ToArray());
        }
    }

    private async Task<Dialogue> Window(WorldClient world, int seen, Func<Dialogue, bool> wanted)
    {
        await Waiting.Until(() => world.TalkCount > seen && world.Talking is { } talk && wanted(talk),
            $"기다린 창이 오지 않았습니다. 마지막 창: {world.Talking?.Kind} {world.Talking?.What}", _deadline.Token);

        return world.Talking!;
    }

    private async Task<Creature> Standing(WorldClient world, Tile where)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Creatures.FirstOrDefault(one => one.Where == where) is { } found)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(
            $"{where} 에 아무도 없습니다. 본 것: "
            + string.Join(", ", world.Creatures.Select(one => $"{one.Serial}@{one.Where}")));
    }
}
