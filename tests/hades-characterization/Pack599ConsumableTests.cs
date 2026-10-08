using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 서버팩의 소모품(물약·음식·귀환 주문서)을 하데스 아이템 템플릿으로 옮긴 것(`scripts/gen/items/build-pack-consumables.py`).
/// 5.99 는 스크립트가 아니라 칸으로 움직인다 — `체력변화 +1000` 은 쓰면 체력이 그만큼 돌아오고, `이동맵 노비스마을`·
/// `이동좌표 40,33` 은 쓰면 그리로 옮겨진다. 하데스는 스크립트 없는 아이템을 "쓸 수 없다"고만 해서 상점에서 사도 쓸모가 없었다.
/// </summary>
public sealed class Pack599ConsumableTests : IDisposable
{
    private const string Name = "packpotion";

    // 5.99 Potion.txt 하급체력포션: 체력변화 +1000 · 판매가격 300.
    private const string Potion = "하급체력포션";

    // 5.99 Recoll.txt 노비스마을리콜: 이동맵 노비스마을 · 이동좌표 40,33. 노비스마을은 번호표(plans/5.99-맵번호표.tsv)에서 20373.
    private const string Recall = "노비스마을리콜";
    private const int NoviceTown = 20373;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_potion_brings_health_back_and_is_used_up_and_a_recall_scroll_sends_you_to_town()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => saved["CurrentHp"] = 1);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Vitals is { MaximumHealth: > 1, Health: 1 }, $"체력 1 로 들어오지 않았습니다. 마지막: {world.Vitals}");

        InventoryItem potion = await Given(world, Potion);
        await world.UseAsync(potion.Slot, _deadline.Token);
        await Until(() => world.Vitals is { } mine && mine.Health == Math.Min(mine.MaximumHealth, 1 + 1000),
            $"{Potion}을 먹었는데 체력이 돌아오지 않았습니다. 마지막: {world.Vitals} · 서버가 한 말: {world.Said}");
        await Until(() => world.Pack.All(item => item.Name != Potion), $"{Potion}이 줄지 않았습니다.");

        InventoryItem recall = await Given(world, Recall);
        await world.UseAsync(recall.Slot, _deadline.Token);
        await Until(() => world.State is { } state && state.Map.Id == NoviceTown && state.Where == new Tile(40, 33),
            $"{Recall}을 썼는데 노비스마을 40,33 으로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}");
    }

    /// <summary>
    /// 물약은 2초에 한 번(사용자 2026-10-08 「2초」·「서버 규칙 — 사람·봇 모두」, `autopilot/eco-bots/potion-cooldown-SPEC.md`).
    /// 그 안에 또 쓰면 서버가 거절하고 물약은 줄지 않는다.
    /// </summary>
    [Fact]
    public async Task A_second_potion_within_two_seconds_is_refused_and_kept()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => saved["CurrentHp"] = 1);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Vitals is { MaximumHealth: > 1, Health: 1 }, $"체력 1 로 들어오지 않았습니다. 마지막: {world.Vitals}");
        await world.SayAsync($"/give \"{Potion}\" 2", _deadline.Token);
        await Until(() => AutoPotion.Count(world.Pack, Potion) == 2, $"{Potion} 둘이 오지 않았습니다. 서버가 한 말: {world.Said}");

        int slot = world.Pack.First(item => item.Name == Potion).Slot;
        await world.UseAsync(slot, _deadline.Token);
        await Until(() => AutoPotion.Count(world.Pack, Potion) == 1 && world.Vitals!.Health > 1,
            $"첫 {Potion}을 먹지 못했습니다. 마지막: {world.Vitals} · 서버가 한 말: {world.Said}");
        DateTime drank = DateTime.UtcNow;

        int told = world.SaidCount;
        await world.UseAsync(slot, _deadline.Token);
        await Until(() => world.SaidCount > told && world.Said.Contains("2초"), $"곧바로 또 먹었는데 서버가 막지 않았습니다. 서버가 한 말: {world.Said}");
        Assert.Equal(1, AutoPotion.Count(world.Pack, Potion));

        TimeSpan rest = drank + TimeSpan.FromSeconds(2.2) - DateTime.UtcNow;
        await Task.Delay(rest > TimeSpan.Zero ? rest : TimeSpan.Zero, _deadline.Token);
        await world.UseAsync(slot, _deadline.Token);
        await Until(() => AutoPotion.Count(world.Pack, Potion) == 0, $"2초 뒤에도 {Potion}을 먹지 못했습니다. 서버가 한 말: {world.Said}");
    }

    /// <summary>
    /// 염색약은 칸이 아니라 아이템 사용 스크립트다(`script/Item/E.T.C.txt`: `set_haircolor 16; item_del "분홍색염색약", 1;`).
    /// `scripts/gen/world/build-pack-npcs.py` 가 기다리지 않는 블록을 `ITEM_이름` 아이템 스크립트로 옮기고, 템플릿이 사용펄숫으로 그것을 부른다.
    /// </summary>
    [Fact]
    public async Task A_hair_dye_colours_the_hair_and_takes_itself_away()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Self is { Wearing: not null }, "처음 겉모습이 오지 않았습니다.");

        InventoryItem dye = await Given(world, "분홍색염색약");
        await world.UseAsync(dye.Slot, _deadline.Token);
        await Until(() => world.Self?.Wearing?.HairColor == 16,
            $"분홍색염색약을 썼는데 머리색이 16 이 되지 않았습니다. 마지막: {world.Self?.Wearing} · 서버가 한 말: {world.Said}");
        await Until(() => world.Pack.All(item => item.Name != "분홍색염색약"), "염색약이 줄지 않았습니다.");
    }

    private async Task<InventoryItem> Given(WorldClient world, string item)
    {
        await world.SayAsync($"/give \"{item}\" 1", _deadline.Token);
        InventoryItem? given = null;
        await Until(() => (given = world.Pack.FirstOrDefault(carried => carried.Name == item)) is not null,
            $"{item}이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}");
        return given!;
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void MakeGameMaster(IsolatedHadesServer server) => Waiting.MakeGameMaster(server, Name);

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
