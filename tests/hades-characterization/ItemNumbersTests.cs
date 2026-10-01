using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 우리 확장 — 소지품 0x0F 끝에 아이템 수치를 덧붙인다(ServerFormat0F, 2026-10-01). 도복 템플릿(방어 10 빼기 · 레벨 1 · 무도가 ·
/// 무게 4)이 앱의 정보 상자 줄까지 그대로 오는지 본다.
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
        Assert.Equal(["방어 -10", "요구 레벨 1 · 무도가", "무게 4"], ItemActions.Stats(robe));
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
