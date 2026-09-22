using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 서버팩의 무기를 하데스 아이템 템플릿으로 옮긴 것(`scripts/build-pack-equipment.py`). 서버가 한글 이름 템플릿을
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

    /// <summary>
    /// 5.99 서버(Novaonline.exe 0x4160f7)는 평타의 몸 동작을 장비에서 고른다 — 무기를 꼈으면 무기의 공격모션·공격속도,
    /// 아니면 갑옷의 것, 둘 다 없으면 (1, 20). 설단검은 도적 단검이라 찌르기(134)를 속도 18 로 보낸다.
    /// </summary>
    [Fact]
    public async Task A_blow_with_a_599_weapon_moves_the_way_the_weapon_says()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => saved["Path"] = "Rogue");

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.Self is { Wearing: not null }, "처음 겉모습이 오지 않았습니다.");

        // 무기 없이: 이제 새 캐릭터는 레더튜닉을 입고 시작한다(LoginServer.EquipStarterOutfit). 그 갑옷은
        // AttackMotion·AttackSpeed 를 안 적어 둘 다 0 이라 BlowMotion 의 갑옷 가지(무기 없을 때 갑옷의 것)가
        // 동작 1, 속도 22 를 낸다 — 정말로 아무것도 안 걸쳤을 때의 (1, 20) 이 아니다.
        Motion bare = await Blow(world);
        Assert.Equal((1, 22), (bare.Number, bare.Speed));

        await world.SayAsync("/give \"설단검\" 1", _deadline.Token);
        InventoryItem? dagger = null;
        await Until(() => (dagger = world.Pack.FirstOrDefault(item => item.Name == "설단검")) is not null,
            $"설단검이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}");
        await world.UseAsync(dagger!.Slot, _deadline.Token);
        await Until(() => world.Self?.Wearing?.Weapon == 6, $"설단검을 끼지 못했습니다. 서버가 한 말: {world.Said}");

        Motion stab = await Blow(world);
        Assert.Equal((134, 18), (stab.Number, stab.Speed));
    }

    /// <summary>평타를 치고 내 몸 동작이 오기를 기다린다. 평타 간격(450ms 남짓)을 넘기려고 여러 번 친다.</summary>
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
