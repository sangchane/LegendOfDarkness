using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 귀환(리콜). 원작: 마을 이름이 붙은 리콜은 그 마을의 정해진 자리로, 그냥 `리콜` 은 정해지지 않은 마을의 정해진 자리로 간다.
/// 마을 이름 리콜은 `scripts/build-pack-consumables.py`(5.99 Recoll.txt → RecallArea·RecallX·RecallY, `Consumable`),
/// 그냥 리콜은 `scripts/build-recall.py` 템플릿 + 서버 스크립트 `scripts/Items/Recall.cs` 의 `Villages` 가 움직인다.
/// </summary>
public sealed class RecallTests : IDisposable
{
    private const string Name = "recaller";

    // Recall.cs 의 Villages 와 같은 마을들 — 그 마을 리콜 템플릿의 자리다(수오미 20355 · 노비스 20373, plans/5.99-맵번호표.tsv).
    private static readonly (string Recall, int Map, Tile Where)[] Villages =
    {
        ("노비스마을리콜", 20373, new Tile(40, 33)),
        ("수오미마을리콜", 20355, new Tile(33, 22)),
    };

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_village_recall_goes_to_that_village_and_a_plain_recall_to_one_of_the_villages()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        Waiting.MakeGameMaster(server, Name);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State is not null, "처음 자리가 오지 않았습니다.");

        // 시작 자리(노비스마을 37,29)는 어느 마을 자리도 아니다 — 리콜이 움직이지 않으면 아래가 걸린다.
        Assert.DoesNotContain(Villages, v => v.Map == world.State!.Map.Id && v.Where == world.State.Where);

        InventoryItem plain = await Given(world, "리콜");
        await world.UseAsync(plain.Slot, _deadline.Token);
        await Until(() => world.Pack.All(item => item.Name != "리콜"), "리콜이 줄지 않았습니다.");
        await Until(() => world.State is { } state && Villages.Any(v => v.Map == state.Map.Id && v.Where == state.Where),
            $"리콜을 썼는데 마을 자리({string.Join(" · ", Villages.Select(v => $"{v.Map} {v.Where}"))}) 어디에도 있지 않습니다. "
            + $"마지막: {world.State} · 서버가 한 말: {world.Said}");

        // 이름 리콜은 리콜이 내려 준 곳이 아닌 다른 마을 것을 쓴다 — 같은 자리면 움직였는지 알 수 없다.
        (string recall, int map, Tile spot) = Villages.First(v => v.Map != world.State!.Map.Id || v.Where != world.State.Where);
        InventoryItem named = await Given(world, recall);
        await world.UseAsync(named.Slot, _deadline.Token);
        await Until(() => world.State is { } state && state.Map.Id == map && state.Where == spot,
            $"{recall}을 썼는데 {map} {spot} 으로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}");
        await Until(() => world.Pack.All(item => item.Name != recall), $"{recall}이 줄지 않았습니다.");
    }

    /// <summary>그냥 리콜은 노비스마을 잡화상(베이가)이 마을 리콜들과 함께 판다 — 혼든 노비스마을_shop.txt 의 노베스 목록.</summary>
    [Fact]
    public void The_novice_general_store_sells_the_plain_recall_and_every_village_recall()
    {
        string path = Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "mundanes", "베이가@노비스잡화상점#3,14.json");
        JsonArray stock = JsonNode.Parse(File.ReadAllText(path))!["DefaultMerchantStock"]!.AsArray();
        string[] sold = stock.Select(n => n!.GetValue<string>()).ToArray();

        Assert.Contains("리콜", sold);
        foreach ((string recall, _, _) in Villages)
            Assert.Contains(recall, sold);
    }

    private async Task<InventoryItem> Given(WorldClient world, string item)
    {
        await world.SayAsync($"/give \"{item}\" 1", _deadline.Token);
        InventoryItem? given = null;
        await Until(() => (given = world.Pack.FirstOrDefault(carried => carried.Name == item)) is not null,
            $"{item}이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}");
        return given!;
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
