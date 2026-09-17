using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 서버팩의 무기를 하데스 아이템 템플릿으로 옮긴 것(`scripts/build-pack-weapons.py`). 서버가 한글 이름 템플릿을
/// 싣고, 운영자가 주면 소지품에 5.99 아이콘으로 들어오고, 끼면 겉모습의 무기 번호가 5.99 `착용이미지` 가 되는지 본다.
/// </summary>
public sealed class Pack599WeaponTests : IDisposable
{
    private const string Name = "packweapon";

    // 5.99 공통무기 에페: 이미지 87 · 착용이미지 2 · 직업 무관 · 레벨 1 — 새 캐릭터가 바로 낄 수 있다.
    private const string Weapon = "에페";
    private const int Icon = 0x8000 + 87;
    private const int Worn = 2;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_599_weapon_is_given_carried_with_its_icon_and_worn_with_its_picture()
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
        await world.SayAsync($"/give \"{Weapon}\" 1", _deadline.Token);

        InventoryItem? given = null;
        await Until(() => (given = world.Pack.FirstOrDefault(item => item.Name == Weapon)) is not null,
            $"{Weapon}이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}");

        Assert.Equal(Icon, given!.Icon);

        await world.UseAsync(given.Slot, _deadline.Token);
        await Until(() => world.Self?.Wearing?.Weapon == Worn,
            $"{Weapon}을 낀 겉모습이 오지 않았습니다. 마지막: {world.Self?.Wearing} · 서버가 한 말: {world.Said}");
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
