using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 죽음 벌칙(사용자 2026-10-11, 명세 <c>autopilot/death-penalty-gear/SPEC.md</c> SC-2) — <c>Aisling.SendToHell</c>.
/// 장비·가방의 사라짐(Perishable) 물건은 없어지고, 나머지와 금화는 죽은 자리 바닥에 — <c>DeathLootMinutes</c> 동안 죽은 본인만
/// 줍고 그 뒤엔 남도 줍는다. <c>DeathDropsItems=false</c> 면 소지품 그대로. 죽이는 법은 <see cref="ComaTests" /> 의 혼자 죽기와 같다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class DeathPenaltyTests : IDisposable
{
    private const int WoodlandOneOne = 20015;
    private const int MurekansRoom = 20138;
    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);
    private static readonly Tile Aside = new(2, 38);

    private const string Sword = "에페";                  // 무기 — 바닥에
    private const string Necklace = "바람의진주목걸이";    // 속성 목걸이 — 사라짐
    private const string Ring = "세토아의고루반지";        // 신 이름 접두사 — 사라짐
    private const string Potion = "상급체력포션";          // 가방 — 바닥에
    private const int Gold = 1000;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(8));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Death_destroys_perishables_and_leaves_the_rest_and_gold_for_the_dead_alone_until_the_lock_lifts()
    {
        const string victim = "deathvictim";
        const string other = "deathother";
        using IsolatedHadesServer server = Ready(drops: true, minutes: 1, victim, other);

        WorldClient dead = await Enter(server, victim);
        WorldClient passer = await Enter(server, other);
        await Until(() => dead.Worn.Count == 2 && dead.Pack.Count == 2, $"입힌 것·넣은 것이 보이지 않습니다: {Names(dead)}");

        await Die(dead);
        DateTime died = DateTime.UtcNow;

        // 죽은 본인: 장비·가방·금화가 모두 빠졌다.
        await Until(() => dead.Worn.Count == 0 && dead.Pack.Count == 0 && dead.Vitals?.Gold == 0,
            $"죽었는데 남은 것: {Names(dead)} · 금화 {dead.Vitals?.Gold}");

        // 바닥: 금화 한 무더기와 물건 둘(에페·포션) — 사라짐 둘(목걸이·반지)은 없다.
        Creature coin = null!;
        await Until(async () =>
        {
            await passer.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
            coin = passer.Creatures.FirstOrDefault(IsCoin)!;
            return coin is not null && Lying(passer, coin.Where).Count == 2;
        }, $"죽은 자리에 금화·물건 둘이 없습니다: {string.Join(", ", passer.Creatures)}");
        Tile spot = coin.Where;
        Assert.Equal(Start, spot);

        // 남은 못 줍는다.
        for (int tries = 0; tries < 3; tries++)
        {
            await passer.PickUpAsync(spot, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
        }

        Assert.DoesNotContain(passer.Pack, item => item.Name is Sword or Potion);
        Assert.Equal(0, passer.Vitals!.Gold);
        Assert.Equal(2, Lying(passer, spot).Count);
        Assert.Contains(passer.Creatures, IsCoin);

        // 활동 기록: 잃은 것·떨군 것·금화.
        string row = await DeathRow(server, victim);
        Assert.Contains(Necklace, row);
        Assert.Contains(Ring, row);
        Assert.Contains("떨군 것 ", row);
        Assert.Contains(Sword, row);
        Assert.Contains(Potion, row);
        Assert.Contains($"금화 {Gold}", row);

        // 본인은 다시 들어와(새 Serial) 줍는다 — 한 번 주우면 금화와 물건 하나.
        await dead.LogOutAsync(_deadline.Token);
        await Until(() => SavedMap(server, victim) == MurekansRoom, "죽은 캐릭터가 뮤레칸의방으로 저장되지 않았습니다.");
        CompanionCallTests.Edit(server, victim, saved =>
        {
            saved["Flags"] = 0;
            saved["_MaximumHp"] = 1_000_000;
            saved["CurrentHp"] = 1_000_000;
            saved["CurrentMapId"] = WoodlandOneOne;
            saved["X"] = spot.X;
            saved["Y"] = spot.Y;
        });
        WorldClient back = await Enter(server, victim);
        await back.PickUpAsync(spot, _deadline.Token);
        await Until(() => back.Vitals?.Gold == Gold && back.Pack.Count(item => item.Name is Sword or Potion) == 1,
            $"본인이 제 금화·물건을 줍지 못했습니다: {Names(back)} · 금화 {back.Vitals?.Gold} · 서버가 한 말: {back.Said}");
        Assert.True(DateTime.UtcNow < died.AddMinutes(1), "본인 줍기가 보호 시간(1분) 안에 끝나지 않았습니다 — 시험이 느립니다.");
        string left = back.Pack.Any(item => item.Name == Sword) ? Potion : Sword;

        // 보호가 풀리면 남도 줍는다.
        TimeSpan wait = died.AddMinutes(1) + TimeSpan.FromSeconds(3) - DateTime.UtcNow;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, _deadline.Token);
        await Until(async () =>
        {
            await passer.PickUpAsync(spot, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
            return passer.Pack.Any(item => item.Name == left);
        }, $"보호가 풀린 뒤에도 남이 {left} 를 줍지 못했습니다. 서버가 한 말: {passer.Said}");
    }

    [Fact]
    public async Task With_the_penalty_off_the_dead_keep_everything()
    {
        const string victim = "deathkeeper";
        using IsolatedHadesServer server = Ready(drops: false, minutes: 10, victim);

        WorldClient dead = await Enter(server, victim);
        await Until(() => dead.Worn.Count == 2 && dead.Pack.Count == 2, $"입힌 것·넣은 것이 보이지 않습니다: {Names(dead)}");
        string before = Names(dead);

        await Die(dead);
        await Task.Delay(1000, _deadline.Token);

        Assert.Equal(before, Names(dead));
        Assert.Equal(Gold, dead.Vitals!.Gold);
    }

    /// <summary>
    /// 리뷰 H1 — 같은 칸의 금화 더미 합침이 잠금을 버리지 않는다. 남이 먼저 버린 금화 위에서 죽어도 남의 금화는 잠기지 않고(따로 한 더미),
    /// 잠긴 죽은 자리 금화 위에 남이 금화를 버려도 남은 제 금화만 되줍는다.
    /// </summary>
    [Fact]
    public async Task Death_gold_lies_apart_from_anyone_elses_gold_on_the_same_tile()
    {
        const string victim = "deathpile";
        const string other = "deathpiler";
        using IsolatedHadesServer server = Ready(drops: true, minutes: 10, victim, other);
        CompanionCallTests.Edit(server, other, saved => saved["GoldPoints"] = 500);

        WorldClient dead = await Enter(server, victim);
        WorldClient passer = await Enter(server, other);
        await Until(() => passer.Vitals?.Gold == 500, "다른 이의 금화 500 이 보이지 않습니다.");

        // 남이 죽을 칸에 먼저 200 을 버린다.
        await passer.DropGoldAsync(200, Start, _deadline.Token);
        await Until(() => passer.Vitals?.Gold == 300, $"금화를 버리지 못했습니다: {passer.Vitals?.Gold} · 서버가 한 말: {passer.Said}");

        await Die(dead);
        await Until(() => dead.Vitals?.Gold == 0, $"죽었는데 금화가 남았습니다: {dead.Vitals?.Gold}");
        await Until(async () =>
        {
            await passer.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
            return passer.Creatures.Count(c => IsCoin(c) && c.Where == Start) == 2;
        }, $"죽은 칸에 금화 두 더미(남의 것·죽은 이 것)가 없습니다: {string.Join(", ", passer.Creatures)}");

        // 남은 제 200 만 되줍는다 — 죽은 이의 1000 은 잠겨 남는다.
        await Until(async () =>
        {
            await passer.PickUpAsync(Start, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
            return passer.Vitals?.Gold == 500;
        }, $"남이 제 금화를 되줍지 못했습니다: {passer.Vitals?.Gold} · 서버가 한 말: {passer.Said}");
        await Task.Delay(1000, _deadline.Token);
        Assert.Equal(500, passer.Vitals!.Gold);
        Assert.Contains(passer.Creatures, c => IsCoin(c) && c.Where == Start);

        // 잠긴 더미 위에 100 을 버리고 주워도 100 만 돌아온다.
        await passer.DropGoldAsync(100, Start, _deadline.Token);
        await Until(() => passer.Vitals?.Gold == 400, $"금화를 다시 버리지 못했습니다: {passer.Vitals?.Gold}");
        await Until(async () =>
        {
            await passer.PickUpAsync(Start, _deadline.Token);
            await Task.Delay(500, _deadline.Token);
            return passer.Vitals?.Gold >= 500;
        }, $"남이 다시 버린 금화를 되줍지 못했습니다: {passer.Vitals?.Gold}");
        await Task.Delay(1000, _deadline.Token);
        Assert.Equal(500, passer.Vitals!.Gold);
        Assert.Contains(passer.Creatures, c => IsCoin(c) && c.Where == Start);
        Assert.Contains($"금화 {Gold}", await DeathRow(server, victim));
    }

    /// <summary>리뷰 M1 — 교환 중에 죽으면 올려 둔 물건·금화가 먼저 돌아온 뒤 벌칙을 받는다(상대에게 넘어가지 않는다).</summary>
    [Fact]
    public async Task Dying_mid_exchange_takes_the_offer_back_before_the_penalty()
    {
        const string victim = "deathtrader";
        const string other = "deathpeer";
        using IsolatedHadesServer server = Ready(drops: true, minutes: 10, victim, other);

        using WorldSession session = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, victim, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient dead = new(session);
        _ = dead.PumpAsync(_deadline.Token);
        WorldClient passer = await Enter(server, other);
        await Until(() => dead.Pack.Count == 2 && dead.Vitals?.Gold == Gold && passer.Serial != 0, $"교환할 것이 보이지 않습니다: {Names(dead)}");

        // 교환 열기(0) · 포션 올리기(1, 칸) · 금화 100 올리기(3) — ActivityLogTests 와 같은 0x4A.
        int potionSlot = dead.Pack.First(item => item.Name == Potion).Slot;
        byte ordinal = 50;
        Task Exchange(byte kind, byte[] extra) => session.Connection.SendAsync(HadesCipher.EncodeSecured(0x4A, ordinal++,
            [kind, (byte)(passer.Serial >> 24), (byte)(passer.Serial >> 16), (byte)(passer.Serial >> 8), (byte)passer.Serial, .. extra],
            session.Parameters), _deadline.Token);
        await Exchange(0, []);
        await Exchange(1, [(byte)potionSlot, 0]);
        await Until(() => dead.Pack.All(item => item.Name != Potion), $"포션을 교환에 올리지 못했습니다: {Names(dead)}");
        await Exchange(3, [0, 0, 0, 100]);
        await Until(() => dead.Vitals?.Gold == Gold - 100, $"금화를 교환에 올리지 못했습니다: {dead.Vitals?.Gold}");

        await Die(dead);

        // 돌려받은 포션·금화 전부가 벌칙을 받았다 — 기록에 포션과 금화 1000.
        string row = await DeathRow(server, victim);
        Assert.Contains(Potion, row);
        Assert.Contains($"금화 {Gold}", row);
        Assert.DoesNotContain(passer.Pack, item => item.Name == Potion);
        Assert.Equal(0, passer.Vitals!.Gold);
    }

    // ---- 도우미 ----

    /// <summary>우드랜드1-1 앞칸에 한 방 괴물을 세우고, 첫 사람(죽을 이)에게 장비 둘·가방 둘·금화를, 다른 이는 곁에 체력 넉넉히.</summary>
    private IsolatedHadesServer Ready(bool drops, double minutes, string victim, params string[] others)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        ComaTests.StandOneKillerAhead(server);
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode settings = JsonNode.Parse(File.ReadAllText(path))!;
        settings["ServerConfig"]!["DeathDropsItems"] = drops;
        settings["ServerConfig"]!["DeathLootMinutes"] = minutes;
        File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, victim);
        CompanionCallTests.Edit(server, victim, saved =>
        {
            saved["_MaximumHp"] = 100;
            saved["CurrentHp"] = 100;
            saved["GoldPoints"] = Gold;
            JsonObject equipment = saved["EquipmentManager"]!["Equipment"]!.AsObject();
            foreach (string place in equipment.Select(pair => pair.Key).ToList())
                equipment[place] = null;    // 새 캐릭터의 기본 옷을 벗겨 둔다
            saved["EquipmentManager"]!["Equipment"]!["1"] = Worn(1, Sword);
            saved["EquipmentManager"]!["Equipment"]!["6"] = Worn(6, Necklace);
            saved["Inventory"]!["Items"]!["1"] = Carried(1, Ring, 1);
            saved["Inventory"]!["Items"]!["2"] = Carried(2, Potion, 3);
        });
        foreach (string other in others)
        {
            LoginFlow.TryCreateAccount(server, other);
            CompanionCallTests.Edit(server, other, saved =>
            {
                saved["X"] = Aside.X;
                saved["Y"] = Aside.Y;
                saved["_MaximumHp"] = 1_000_000;
                saved["CurrentHp"] = 1_000_000;
            });
        }

        return server;
    }

    private static JsonObject Worn(int slot, string name) => new()
    {
        ["Slot"] = slot,
        ["Item"] = new JsonObject { ["Template"] = new JsonObject { ["Name"] = name }, ["Stacks"] = 1, ["Durability"] = 1000 },
    };

    private static JsonObject Carried(int slot, string name, int stacks) => new()
    {
        ["Template"] = new JsonObject { ["Name"] = name }, ["Slot"] = slot, ["Stacks"] = stacks, ["Durability"] = 1000,
    };

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State is not null && world.Vitals is not null && world.Self?.Name is { Length: > 0 }, $"{who} 가 월드에 서지 못했습니다.");
        return world;
    }

    /// <summary>앞칸 괴물을 쳐 깨우고 맞아 죽는다 — 무리가 없으니 바로 뮤레칸의방(ComaTests 혼자 죽기).</summary>
    private async Task Die(WorldClient world)
    {
        await Until(async () =>
        {
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
            return world.State?.Where == Start && world.Creatures.Any(c => c.Where == Ahead);
        }, "우드랜드1-1 입구 앞칸에 괴물이 서지 않았습니다.");

        await world.TurnAsync(Direction.North, _deadline.Token);
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (world.State?.Map.Id != MurekansRoom && DateTime.UtcNow < giveUp)
        {
            if (world.State?.Map.Id == WoodlandOneOne)
                await world.AttackAsync(_deadline.Token);
            await Task.Delay(300, _deadline.Token);
        }

        Assert.True(world.State?.Map.Id == MurekansRoom, $"죽어 뮤레칸의방으로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}");
    }

    private static bool IsCoin(Creature c) => c.Kind == CreatureKind.Passable && c.Sprite is >= 0x8089 and <= 0x808E;

    private static List<Creature> Lying(WorldClient world, Tile tile) =>
        [.. world.Creatures.Where(c => c.Kind == CreatureKind.Passable && c.Where == tile && !IsCoin(c))];

    private static string Names(WorldClient world) =>
        $"입은 것 [{string.Join(",", world.Worn.Select(w => w.Name))}] 가방 [{string.Join(",", world.Pack.Select(p => $"{p.Name}x{p.Stacks}"))}]";

    private static int? SavedMap(IsolatedHadesServer server, string who)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(Path.Combine(server.ContentLocation, "aislings", $"{who}.json")))?["CurrentMapId"]?.GetValue<int>();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;   // 서버가 쓰는 도중
        }
    }

    /// <summary>활동 기록(RunRoot/activity/*.jsonl)에서 이 사람의 죽음 줄의 detail.</summary>
    private async Task<string> DeathRow(IsolatedHadesServer server, string who)
    {
        string folder = Path.Combine(server.RunRoot, "activity");
        string? found = null;
        await Until(() =>
        {
            if (!Directory.Exists(folder))
                return false;
            foreach (string path in Directory.GetFiles(folder, "*.jsonl"))
            {
                using FileStream read = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader text = new(read);
                foreach (string line in text.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    JsonNode? one = JsonNode.Parse(line);
                    if ((string?)one?["kind"] == "death" && (string?)one["player"] == who)
                        found = (string?)one["detail"];
                }
            }

            return found is not null;
        }, "활동 기록에 죽음 줄이 없습니다.");
        return found!;
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);

    private async Task Until(Func<Task<bool>> condition, string failure)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!await condition())
        {
            if (DateTime.UtcNow > giveUp)
                throw new TimeoutException(failure);
        }
    }
}
