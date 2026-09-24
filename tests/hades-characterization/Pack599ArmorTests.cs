using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 서버팩의 갑옷을 하데스 아이템 템플릿으로 옮긴 것(`scripts/build-pack-equipment.py`). 도복은 공격모션 132 라
/// 무기 없이 치면 주먹이 나가고(Novaonline.exe 0x4160f7 — 무기가 없으면 갑옷의 공격모션), 도복을 입고는 신발을 못
/// 신는다(0x41cc49 · 0x41d387, 「신발이 불편하여 입을수가 없습니다.」).
/// </summary>
public sealed class Pack599ArmorTests : IDisposable
{
    private const string Name = "packarmor";

    // 5.99 무도가방어구 도복: 착용이미지 3 · 공격모션 132 · 무도가 · 남자 · 레벨 1.
    private const string Uniform = "도복";
    private const string Boots = "Shagreen Boots";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_monk_in_a_uniform_punches_bare_handed_and_cannot_put_boots_on()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => saved["Path"] = "Monk");

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Self is { Wearing: not null }, "처음 겉모습이 오지 않았습니다.");

        InventoryItem uniform = await Given(world, Uniform);
        await world.UseAsync(uniform.Slot, _deadline.Token);
        await Until(() => world.Self?.Wearing?.Armor == 3, $"도복을 입지 못했습니다. 서버가 한 말: {world.Said}");

        Motion punch = await Blow(world);
        Assert.Equal((132, 20), (punch.Number, punch.Speed));

        InventoryItem boots = await Given(world, Boots);
        int said = world.SaidCount;
        await world.UseAsync(boots.Slot, _deadline.Token);
        await Until(() => world.SaidCount > said && world.Said.Contains("신발이 불편"),
            $"도복 차림에 신발을 막지 않았습니다. 서버가 한 말: {world.Said}");

        await Task.Delay(500, _deadline.Token);
        Assert.Equal(0, world.Self!.Wearing!.Boots);
    }

    /// <summary>
    /// 방패·투구·장신구·장갑·허리띠·각반·신발·장식도 같은 생성기로 들어왔다(상점 판매 목록에 없던 것 대부분). 칸마다 하데스
    /// 스크립트·자리를 따른다 — 목걸이 Necklace 6, 장갑 Generic 9. 5.99 대지의룬스톤목걸이는 체력변화 +1000 · 레벨제한 11.
    /// </summary>
    [Fact]
    public async Task A_599_necklace_and_gloves_go_on_and_the_necklace_adds_its_health()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => saved["ExpLevel"] = 99);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Vitals is { MaximumHealth: > 0 }, "처음 수치가 오지 않았습니다.");
        int before = world.Vitals!.MaximumHealth;

        InventoryItem necklace = await Given(world, "대지의룬스톤목걸이");
        await world.UseAsync(necklace.Slot, _deadline.Token);
        await Until(() => world.Worn.Any(worn => worn.Slot == 6 && worn.Called.StartsWith("대지의룬스톤목걸이")),
            $"목걸이를 걸지 못했습니다. 걸친 것: {string.Join(", ", world.Worn.Select(worn => $"{worn.Slot}:{worn.Called}"))} · 서버가 한 말: {world.Said}");
        await Until(() => world.Vitals?.MaximumHealth == before + 1000,
            $"목걸이의 체력 +1000 이 붙지 않았습니다. 전 {before} · 지금 {world.Vitals?.MaximumHealth}");

        InventoryItem gloves = await Given(world, "가죽장갑");
        await world.UseAsync(gloves.Slot, _deadline.Token);
        await Until(() => world.Worn.Any(worn => worn.Slot is 9 or 10 && worn.Called.StartsWith("가죽장갑")),
            $"장갑을 끼지 못했습니다. 걸친 것: {string.Join(", ", world.Worn.Select(worn => $"{worn.Slot}:{worn.Called}"))} · 서버가 한 말: {world.Said}");
    }

    /// <summary>
    /// 원작 착용 흐름을 끝까지 본다. 소지품 칸을 쓰면(0x1C) 서버가 그 칸을 비우고(0x10) 갑옷 자리를 채운 뒤(0x37) 우리를
    /// 다시 그린다(0x33) — `EquipmentManager.AddEquipment`. 화면은 이 셋을 그대로 옮긴다: 소지품 격자에서 빠지고
    /// (PackPanel), 장비 고리의 갑옷 칸에 뜨고(GearGrid), 몸과 종이인형이 갈아입는다(WorldView.Wear · GearGrid.ShowDoll).
    /// 운영자가 아니다 — 운영자는 `CheckReqs` 가 직업·성별을 보지 않고 입혀 주므로 사람이 겪는 길이 아니다.
    /// </summary>
    [Fact]
    public async Task Putting_on_a_uniform_takes_it_out_of_the_pack_fills_the_armour_place_and_redresses_us()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => Bare(saved, uniformDurability: 3000));

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = await Enter(session);
        InventoryItem uniform = await Carried(world, Uniform);

        await world.UseAsync(uniform.Slot, _deadline.Token);

        await Until(() => world.Worn.Any(worn => worn.Slot == 2 && worn.Name == Uniform),
            $"갑옷 자리에 도복이 오지 않았습니다(0x37). 서버가 한 말: {world.Said}");
        await Until(() => world.Pack.All(carried => carried.Name != Uniform),
            $"도복이 소지품에서 빠지지 않았습니다(0x10). 소지품: {string.Join(", ", world.Pack.Select(carried => carried.Name))}");
        await Until(() => world.Self?.Wearing?.Armor == 3, $"도복 차림으로 다시 그려지지 않았습니다(0x33). 갑옷 {world.Self?.Wearing?.Armor}");
    }

    /// <summary>
    /// `monk` 가 겪은 것(2026-09-23). 우드랜드에서 사냥하다 도복 내구도가 0 이 되어 부서지면 서버는 그것을 없애지 않고
    /// 소지품으로 돌려보낸다(`EquipmentManager.DecreaseDurability` → `RemoveFromExisting`). 그 도복은 다시 입혀지지
    /// 않는다 — `CheckReqs` 가 영어 한 줄(`RepairItemMessage`)만 보내고 아무것도 옮기지 않는다.
    /// </summary>
    [Fact]
    public async Task A_worn_out_uniform_is_refused_and_stays_in_the_pack()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => Bare(saved, uniformDurability: 0));

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = await Enter(session);
        InventoryItem uniform = await Carried(world, Uniform);
        int said = world.SaidCount;

        await world.UseAsync(uniform.Slot, _deadline.Token);

        await Until(() => world.SaidCount > said && world.Said.Contains("부서져서"),
            $"부서진 도복을 막는 말이 오지 않았습니다. 서버가 한 말: {world.Said}");

        await Task.Delay(500, _deadline.Token);
        Assert.Contains(world.Pack, carried => carried.Name == Uniform);
        Assert.DoesNotContain(world.Worn, worn => worn.Slot == 2);
        Assert.Equal(0, world.Self!.Wearing!.Armor);
    }

    private const int DurabilityWoodland = 20015;
    private static readonly Tile DurabilityStart = new(2, 35);
    private static readonly Tile DurabilityAhead = new(2, 34);
    private const int DurabilityBlow = 5;

    /// <summary>
    /// `monk` 이 우드랜드에서 사냥하다 겪은 일(2026-09-23) — 내구도가 0 이 된 도복이 없어지지 않고 소지품으로
    /// 돌아갔다(`EquipmentManager.DecreaseDurability` → `RemoveFromExisting`, returnit 기본값 true). 도복을 입은
    /// 채 내구도 1 로 두고 정의 한 마리에게 한 대 맞혀, 부서진 도복이 장비 칸에도 소지품에도 남지 않고 무게도
    /// 함께 빠지며 부서짐 알림이 오는지 본다. 고치기 전에는 소지품에 도복이 남아 실패한다.
    /// </summary>
    [Fact]
    public async Task A_uniform_that_breaks_while_worn_disappears_instead_of_returning_to_the_pack()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (DurabilityWoodland, DurabilityStart.X, DurabilityStart.Y));
        SpawnDurabilityStriker(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            Worn(saved, uniformDurability: 1);
            saved["_MaximumHp"] = 100_000;
            saved["CurrentHp"] = 100_000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = await Enter(session);

        await Until(() => world.Self?.Wearing?.Armor == 3 && world.Vitals is { Weight: > 0 },
            $"도복을 입고 무게가 잡힌 채로 들어오지 못했습니다. 갑옷 {world.Self?.Wearing?.Armor} · 무게 {world.Vitals?.Weight}");
        int weightBefore = world.Vitals!.Weight;
        int said = world.SaidCount;

        await Until(() => world.Self?.Wearing?.Armor == 0,
            $"도복이 부서져도 갑옷 자리가 그대로입니다. 서버가 한 말: {world.Said}");

        await Task.Delay(500, _deadline.Token);

        Assert.DoesNotContain(world.Worn, worn => worn.Slot == 2);
        Assert.DoesNotContain(world.Pack, carried => carried.Name == Uniform);
        Assert.True(world.Vitals!.Weight < weightBefore,
            $"부서진 도복의 무게가 빠지지 않았습니다. 전 {weightBefore} · 지금 {world.Vitals.Weight}");
        Assert.True(world.SaidCount > said && world.Said.Contains("부서졌습니다"),
            $"부서짐 알림이 오지 않았습니다. 서버가 한 말: {world.Said}");
    }

    /// <summary>내구도 시험용 — 도복을 갑옷 자리(2)에 입힌 채로 등장시킨다. 소지품에는 넣지 않는다.</summary>
    private static void Worn(JsonNode saved, int uniformDurability)
    {
        saved["Path"] = "Monk";
        saved["EquipmentManager"]!["Equipment"]!["2"] = new JsonObject
        {
            ["Slot"] = 2,
            ["Item"] = new JsonObject
            {
                ["Template"] = new JsonObject { ["Name"] = Uniform },
                ["Image"] = 3,
                ["DisplayImage"] = 32866,
                ["Stacks"] = 1,
                ["Durability"] = uniformDurability,
            },
        };
    }

    /// <summary>
    /// 우드랜드1-1 의 다른 정의를 모두 죽이고(SpawnMax 0) 내가 서는 칸 앞에 고정 공격력 정의 한 마리만 세운다
    /// (`Pack599MonsterBlowTests.StandOneStrikerAhead` 와 같은 방법).
    /// </summary>
    private static void SpawnDurabilityStriker(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{DurabilityWoodland}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonNode? striker = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!woodland.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            striker ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString());
        }

        Assert.NotNull(striker);
        striker["Name"] = "내구도시험괴물";
        striker["BaseName"] = "내구도시험괴물";
        striker["SpawnMax"] = 1;
        striker["SpawnType"] = 4;
        striker["SpawnRate"] = 1;
        striker["DefinedX"] = DurabilityAhead.X;
        striker["DefinedY"] = DurabilityAhead.Y;
        striker["MaximumHP"] = 1_000_000;
        striker["DmgMin"] = DurabilityBlow;
        striker["DmgMax"] = DurabilityBlow;
        striker["MoodType"] = 2;
        striker["PathQualifer"] = 2;
        striker["Grow"] = false;
        striker["SpellScripts"] = null;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "pack599-armor-durability.json"), striker.ToJsonString());
    }

    /// <summary>
    /// 무도가 1레벨, 걸친 것 없이 소지품 첫 칸에 도복 한 벌 — `monk` 의 지금 모습이다. 새 계정은 전사 옷을 입고
    /// 태어나므로(`LoginServer.EquipStarterOutfit`) 벗겨 둔다. 힘을 올리는 것은 접속할 때 무게가 한도를 넘으면 서버가
    /// 소지품을 바닥에 떨어뜨리기 때문이다(`GameClient.LoadInventory`).
    /// </summary>
    private static void Bare(JsonNode saved, int uniformDurability)
    {
        saved["Path"] = "Monk";
        saved["_Str"] = 30;
        saved["EquipmentManager"]!["Equipment"]!["2"] = null;
        saved["Inventory"]!["Items"]!["1"] = new JsonObject
        {
            ["Template"] = new JsonObject { ["Name"] = Uniform },
            ["Slot"] = 1,
            ["Image"] = 3,
            ["DisplayImage"] = 32866,
            ["Stacks"] = 1,
            ["Durability"] = uniformDurability,
        };
    }

    private async Task<WorldClient> Enter(WorldSession session)
    {
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Self is { Wearing: not null }, "처음 겉모습이 오지 않았습니다.");

        return world;
    }

    private async Task<InventoryItem> Carried(WorldClient world, string item)
    {
        InventoryItem? carried = null;
        await Until(() => (carried = world.Pack.FirstOrDefault(one => one.Name == item)) is not null,
            $"{item}이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}");
        return carried!;
    }

    private async Task<InventoryItem> Given(WorldClient world, string item)
    {
        await world.SayAsync($"/give \"{item}\" 1", _deadline.Token);
        InventoryItem? given = null;
        await Until(() => (given = world.Pack.FirstOrDefault(carried => carried.Name == item)) is not null,
            $"{item}이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}");
        return given!;
    }

    /// <summary>평타를 치고 내 몸 동작이 오기를 기다린다. 평타 간격을 넘기려고 여러 번 친다.</summary>
    private async Task<Motion> Blow(WorldClient world)
    {
        while (world.TakeMotion(out _))
        {
        }

        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < giveUp)
        {
            await world.AttackAsync(_deadline.Token);

            for (int wait = 0; wait < 12; wait++)
            {
                await Task.Delay(50, _deadline.Token);

                while (world.TakeMotion(out Motion? motion))
                {
                    if (motion.Serial == world.Serial)
                    {
                        return motion;
                    }
                }
            }
        }

        throw new TimeoutException($"평타 뒤 내 몸 동작이 오지 않았습니다. 서버가 한 말: {world.Said}");
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void MakeGameMaster(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(Name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task Until(Func<bool> condition, string failure)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(failure);
    }
}
