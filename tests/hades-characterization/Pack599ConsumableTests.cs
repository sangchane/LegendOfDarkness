using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 서버팩의 소모품(물약·음식·귀환 주문서)을 하데스 아이템 템플릿으로 옮긴 것(`scripts/build-pack-consumables.py`).
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
