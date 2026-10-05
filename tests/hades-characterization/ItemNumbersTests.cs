using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 우리 확장 — 소지품 0x0F 끝에 아이템 수치를 덧붙인다(ServerFormat0F, 2026-10-01). 도복 템플릿(방어 10 빼기 · 레벨 1 · 무도가 ·
/// 무게 4)이 앱의 정보 상자 줄까지 그대로 오는지, 입으면 장비(0x37)에도 오는지 본다.
/// </summary>
public sealed class ItemNumbersTests : IDisposable
{
    private const string Name = "itemnums";
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_carried_thing_comes_with_its_numbers()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (20015, 2, 35));
        string config = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode settings = JsonNode.Parse(File.ReadAllText(config))!;
        settings["ServerConfig"]!["GameMasters"] = new JsonArray(Name);
        File.WriteAllText(config, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode aisling = JsonNode.Parse(File.ReadAllText(saved))!;
        aisling["Path"] = 5; // 도복은 무도가 옷
        File.WriteAllText(saved, aisling.ToJsonString());
        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Self is not null, "들어가지 못했습니다.");
        await world.SayAsync("/give \"도복\" 1", _deadline.Token);

        InventoryItem? robe = null;
        await Until(() => (robe = world.Pack.FirstOrDefault(carried => carried.Name == "도복")) is not null,
            $"도복이 소지품에 오지 않았습니다: {world.Said}");

        Assert.NotNull(robe!.Stats);
        Assert.Equal(["방어 -10", "요구 레벨 1 · 무도가"], ItemActions.Stats(robe).Select(line => line.Text));
        Assert.Equal(2, robe.Stats!.Place); // 갑옷 자리
        // 새 캐릭터는 처음부터 옷을 입고 있다 — 그러면 갑옷 자리를 바꾸는 것이다.
        Assert.Equal(world.Worn.Any(on => on.Slot == 2) ? "교체" : "장착", ItemActions.Primary(robe, world.Worn));

        // 입으면 장비창(0x37)에도 같은 수치가 온다.
        await world.UseAsync(robe.Slot, _deadline.Token);
        WornItem? worn = null;
        await Until(() => (worn = world.Worn.FirstOrDefault(on => on.Slot == 2 && on.Name == "도복")) is not null, $"도복을 입지 못했습니다: {world.Said}");
        Assert.Equal(["방어 -10", "요구 레벨 1 · 무도가"], ItemActions.Stats(worn!.Stats).Select(line => line.Text));

        // 물약은 마시면 얼마나 차는지가 붙어 온다(2026-10-05).
        await world.SayAsync("/give \"하급마력포션\" 1", _deadline.Token);
        InventoryItem? potion = null;
        await Until(() => (potion = world.Pack.FirstOrDefault(carried => carried.Name == "하급마력포션")) is not null,
            $"하급마력포션이 소지품에 오지 않았습니다: {world.Said}");
        Assert.Contains("마력 회복 +1,000", ItemActions.Stats(potion!).Select(line => line.Text));
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
