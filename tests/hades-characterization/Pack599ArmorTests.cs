using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
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
