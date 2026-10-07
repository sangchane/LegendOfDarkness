using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 그룹 전리품(<c>Types/GroupLoot.cs</c>, 설계 <c>autopilot/loot-auction/</c>) — 그룹이 잡은 괴물의 장비는 룰렛으로 한 사람 가방에,
/// 금화는 같은 맵 그룹원에게 똑같이, 장비 아닌 것은 차례로. 받을 이의 가방이 차면 그 사람 발밑에 그 사람만 주울 수 있게 떨어진다.
/// 동료 봇은 나누지 않는다(SPEC S-1).
/// </summary>
public sealed class GroupLootTests : IDisposable
{
    /// <summary>우드랜드1-1 — <see cref="MonsterGoldTests" /> 와 같은 방, 같은 문 앞칸.</summary>
    private const int Room = 20015;
    private static readonly Tile Stand = new(2, 35);
    private static readonly Tile Spot = new(2, 34);
    private const string Sword = "에페";
    private const string Potion = "상급체력포션";
    private const int Gold = 1_001;                 // 넷이면 250 씩, 남는 1 은 처치한 이
    private const int MaxCarryGold = 100_000_000;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(8));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Roll_gives_each_drop_to_one_member_and_splits_the_gold()
    {
        const int kills = 10;
        using IsolatedHadesServer server = Ready(target => Drops(target, Sword), config: null,
            ("lootlead", null), ("lootb", null), ("lootc", null), ("lootd", saved => saved["GoldPoints"] = MaxCarryGold - 100));
        WorldClient lead = await Enter(server, "lootlead");
        WorldClient[] mates = [await Enter(server, "lootb"), await Enter(server, "lootc"), await Enter(server, "lootd")];
        WorldClient[] all = [lead, .. mates];
        await Group(lead, "lootlead", (mates[0], "lootb"), (mates[1], "lootc"), (mates[2], "lootd"));
        long[] before = [.. all.Select(world => world.Vitals!.Gold)];

        for (int kill = 1; kill <= kills; kill++)
        {
            await Kill(lead, () => lead.RollCount >= kill);
            await Until(() => all.All(world => world.RollCount == kill), $"{kill}번째 룰렛이 넷 모두에게 오지 않았습니다: {string.Join(",", all.Select(world => world.RollCount))}");
            await Until(() => all.Sum(Swords) == kill, $"{kill}번째 뒤 에페가 {all.Sum(Swords)}개 — 한 사람 가방에만 하나씩이어야 합니다.");

            LootRoll roll = lead.LastRoll!;
            Assert.Equal(4, roll.Rolls.Count);
            Assert.Equal(all.Select(world => world.Serial).Order(), roll.Rolls.Select(one => one.Serial).Order());
            Assert.Contains(roll.Winner, roll.Rolls.Select(one => one.Serial));
            Assert.Null(LyingAt(lead, Spot));
        }

        // 금화: 250 씩, 처치한 이 251, 넷째는 상한까지 100 만 들고 나머지는 받을 것.
        await Until(() => lead.Vitals!.Gold - before[0] == kills * 251 && mates[0].Vitals!.Gold - before[1] == kills * 250
                          && mates[1].Vitals!.Gold - before[2] == kills * 250 && mates[2].Vitals!.Gold == MaxCarryGold,
            $"나눈 금화가 맞지 않습니다: {string.Join(", ", all.Select((world, i) => world.Vitals!.Gold - before[i]))}");
        long overflow = Claims(server, "lootd");
        Assert.Equal(kills * 250 - 100, overflow);
        Assert.Equal(kills * Gold, all.Select((world, i) => world.Vitals!.Gold - before[i]).Sum() + overflow);
    }

    [Fact]
    public async Task A_full_bag_drops_at_the_winners_feet_only_for_them()
    {
        // 이름 차례: fullbag 이 먼저 받는다 — 가방이 차 있어 발밑에.
        using IsolatedHadesServer server = Ready(target => Drops(target, Potion), config: null,
            ("fullbag", saved =>
            {
                for (int slot = 1; slot <= 150; slot++)
                {
                    saved["Inventory"]!["Items"]![slot.ToString()] = new JsonObject
                    {
                        ["Template"] = new JsonObject { ["Name"] = Sword }, ["Slot"] = slot, ["Stacks"] = 1, ["Durability"] = 100,
                    };
                }
            }),
            ("fullmate", null));
        WorldClient full = await Enter(server, "fullbag");
        WorldClient mate = await Enter(server, "fullmate");
        await Until(() => full.Pack.Count == 150, $"가방이 150칸이 아닙니다: {full.Pack.Count}");
        await Group(mate, "fullmate", (full, "fullbag"));

        await Kill(mate, () => LyingNear(mate) is not null);
        Creature dropped = LyingNear(mate)!;
        Assert.Equal(0, Potions(mate));

        // 보호는 3분 — 몇 번의 순회가 지나도 남이 주울 수 없다.
        await Task.Delay(TimeSpan.FromSeconds(2), _deadline.Token);
        await mate.PickUpAsync(dropped.Where, _deadline.Token);
        await Task.Delay(TimeSpan.FromSeconds(2), _deadline.Token);
        Assert.Equal(0, Potions(mate));
        Assert.NotNull(LyingNear(mate));

        // 다음 차례는 fullmate — 가방으로 바로.
        await Kill(mate, () => Potions(mate) > 0);
    }

    [Fact]
    public async Task A_companion_bot_does_not_share_the_loot()
    {
        using IsolatedHadesServer server = Ready(target => Drops(target, Sword), config =>
            {
                config["CompanionBots"] = new JsonArray("lootbot");
                config["CompanionHomeMap"] = Room;
                config["CompanionHomePosition"] = new JsonObject { ["X"] = Stand.X, ["Y"] = Stand.Y };
            },
            ("loothero", null), ("lootbot", null));
        WorldClient hero = await Enter(server, "loothero");
        WorldClient bot = await Enter(server, "lootbot");
        await Group(hero, "loothero", (bot, "lootbot"));
        Assert.Equal(Room, bot.State!.Map.Id);

        await Kill(hero, () => LyingAt(hero, Spot) is not null);
        Assert.Equal(0, hero.RollCount);
        Assert.Equal(0, Swords(hero) + Swords(bot));
    }

    [Fact]
    public async Task With_the_roll_turned_off_group_loot_lies_on_the_floor()
    {
        using IsolatedHadesServer server = Ready(target => Drops(target, Sword), config => config["GroupLootRoll"] = false,
            ("offlead", null), ("offmate", null));
        WorldClient lead = await Enter(server, "offlead");
        WorldClient mate = await Enter(server, "offmate");
        await Group(lead, "offlead", (mate, "offmate"));

        await Kill(lead, () => LyingAt(lead, Spot) is not null);
        Assert.Equal(0, lead.RollCount);
        Assert.Equal(0, Swords(lead) + Swords(mate));
    }

    // ---- 도우미 ----

    private static void Drops(JsonNode target, string item)
    {
        target["LootType"] = 2;      // LootQualifer.Random
        target["Drops"] = new JsonArray(item);
        target["DropRate"] = 1;      // × 1.5 — 늘 떨어진다
    }

    private IsolatedHadesServer Ready(Action<JsonNode> monster, Action<JsonNode>? config, params (string Who, Action<JsonNode>? Edit)[] people)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (Room, Stand.X, Stand.Y));
        StandTarget(server, monster);
        if (config is not null)
        {
            string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
            JsonNode settings = JsonNode.Parse(File.ReadAllText(path))!;
            config(settings["ServerConfig"]!);
            File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        server.Start(TimeSpan.FromMinutes(2));
        foreach (var (who, edit) in people)
        {
            LoginFlow.TryCreateAccount(server, who);
            if (edit is not null)
            {
                CompanionCallTests.Edit(server, who, edit);
            }
        }

        return server;
    }

    /// <summary>방의 정의는 모두 세우지 않고, 문 앞칸에 한 대에 쓰러지는 표적 하나를 1초마다 다시 세운다.</summary>
    private static void StandTarget(IsolatedHadesServer server, Action<JsonNode> customize)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        List<(string Path, JsonNode Template)> room = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(server.ContentLocation, "templates", "monsters"), "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (text.Contains($"\"AreaID\": {Room}", StringComparison.Ordinal))
            {
                room.Add((path, JsonNode.Parse(text, documentOptions: lenient)!));
            }
        }

        Assert.NotEmpty(room);
        JsonNode target = room.MinBy(one => (int?)one.Template["MaximumHP"] ?? 0).Template.DeepClone();
        foreach ((string path, JsonNode template) in room)
        {
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        target["Name"] = "룰렛시험표적";
        target["SpawnType"] = 4;     // Defined
        target["SpawnRate"] = 1;
        target["SpawnMax"] = 1;
        target["DefinedX"] = Spot.X;
        target["DefinedY"] = Spot.Y;
        target["PathQualifer"] = 2;  // Fixed
        target["MoodType"] = 1;      // Idle
        target["Grow"] = false;
        target["MaximumHP"] = 1;
        target["Exp"] = 100;
        target["GoldMinimum"] = Gold;
        customize(target);

        string folder = Path.Combine(server.ContentLocation, "templates", "monsters", "characterization");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "group-loot-target.json"), target.ToJsonString(indented));
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State is not null && world.Vitals is not null && world.Self?.Name is { Length: > 0 }, $"{who} 가 월드에 서지 못했습니다.");
        return world;
    }

    /// <summary>
    /// 그룹장이 청하고 그룹원이 받아들인다. 서버는 화면을 새로 그린 지 0.3초 안(<c>RefreshRate</c>)의 받아들임을 말없이 버린다 —
    /// 같은 칸에 넷이 서면 그런 일이 잦아, 들 때까지 다시 청한다.
    /// </summary>
    private async Task Group(WorldClient lead, string leadName, params (WorldClient World, string Name)[] mates)
    {
        foreach (var (mate, name) in mates)
        {
            for (int tries = 0; tries < 15 && lead.MemberStatus(mate.Serial) is null; tries++)
            {
                await lead.AskToGroupAsync(name, _deadline.Token);
                try
                {
                    string? asker = null;
                    await Waiting.Until(() => mate.TakeAsk(out asker), "0x63", _deadline.Token, TimeSpan.FromSeconds(1));
                    Assert.Equal(leadName, asker);
                    await mate.AcceptGroupAsync(leadName, _deadline.Token);
                    await Waiting.Until(() => lead.MemberStatus(mate.Serial) is not null, "0x5E 6", _deadline.Token, TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException)
                {
                }
            }

            Assert.True(lead.MemberStatus(mate.Serial) is not null, $"{name} 이 그룹에 들지 않았습니다.");
        }
    }

    /// <summary>북쪽 표적을 될 때까지 친다(GlobalBaseSkillDelay 500ms 보다 느리게).</summary>
    private async Task Kill(WorldClient killer, Func<bool> done)
    {
        await killer.TurnAsync(Direction.North, _deadline.Token);
        for (int swing = 0; swing < 60 && !done(); swing++)
        {
            await killer.AttackAsync(_deadline.Token);
            await Task.Delay(600, _deadline.Token);
        }

        await Until(done, "표적을 잡지 못했습니다.");
    }

    private static int Swords(WorldClient world) => world.Pack.Count(item => item.Name == Sword);

    private static int Potions(WorldClient world) => world.Pack.Where(item => item.Name == Potion).Sum(item => Math.Max(1, item.Stacks));

    /// <summary>바닥 물건(금화 그림 0x8089~0x808E 가 아닌 지나갈 수 있는 것).</summary>
    private static Creature? LyingAt(WorldClient world, Tile tile) =>
        world.Creatures.FirstOrDefault(c => c.Kind == CreatureKind.Passable && c.Where == tile && c.Sprite is not (>= 0x8089 and <= 0x808E));

    private static Creature? LyingNear(WorldClient world) =>
        world.Creatures.FirstOrDefault(c => c.Kind == CreatureKind.Passable && c.Where != Spot && c.Sprite is not (>= 0x8089 and <= 0x808E)
                                            && Math.Abs(c.Where.X - Stand.X) + Math.Abs(c.Where.Y - Stand.Y) <= 2);

    private static long Claims(IsolatedHadesServer server, string owner)
    {
        JsonNode book = JsonNode.Parse(File.ReadAllText(Path.Combine(server.ContentLocation, "auction", "auction.json")))!;
        return book["Claims"]!.AsArray().Where(claim => (string?)claim!["Owner"] == owner).Sum(claim => (long)claim!["Gold"]!);
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
