using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 은행 — 오리아나@아벨은행#5,3 (<c>scripts/Mundanes/Banker.cs</c>). 물건(겹치는 것은 수량까지)·금화를 맡기고,
/// 나갔다 들어와도 남아 있고, 찾을 때 가방이 차 있거나 금화 한도를 넘으면 아무것도 바뀌지 않는다. 연타는 두 번 맡기지 않는다.
/// 창은 앱이 이미 그리는 것만 쓴다 — 메뉴 · 가방 칸(0x05) · 맡긴 목록(0x04) · 수 입력(0x02).
/// </summary>
public sealed class BankTests : IDisposable
{
    private const int AbelBank = 20039;
    private static readonly Tile Oriana = new(5, 3);
    private const string Potion = "상급체력포션"; // 겹친다(MaxStack 1000)
    private const string Sword = "에페";          // 안 겹친다

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Deposits_survive_coming_back_and_come_out_whole()
    {
        const string who = "bankkeep";
        using IsolatedHadesServer server = Ready(who, saved =>
        {
            saved["GoldPoints"] = 1000;
            Put(saved, 1, Potion, 300); // 255 를 넘는 묶음 — 일부를 맡겨도 290 이 남아야 한다
            Put(saved, 2, Sword, 1);
        });

        using (WorldSession first = await Login(server, who))
        {
            WorldClient world = Pump(first);
            Creature banker = await Menu(world);

            // 겹치는 물건 10개 — 칸을 고르고 수를 적는다.
            Dialogue slots = await Pick(world, banker, "물건 맡기기", talk => talk.Kind == DialogueKind.PackSlots);
            Assert.Contains(1, slots.Slots);
            Dialogue howMany = await Answer(world, banker, slots.Step, "1", talk => talk.Kind == DialogueKind.TextInput);
            Dialogue after = await Answer(world, banker, howMany.Step, "10", talk => talk.Kind == DialogueKind.PackSlots);
            await Until(() => Stacks(world, Potion) == 290, $"물약이 290 이 아닙니다: {Stacks(world, Potion)}");

            // 안 겹치는 물건은 바로.
            await Answer(world, banker, after.Step, "2", talk => talk.Kind == DialogueKind.PackSlots || talk.Kind == DialogueKind.Options);
            await Until(() => world.Pack.All(item => item.Name != Sword), "에페가 가방에 남았습니다.");

            // 금화 400.
            await world.ShutDialogueAsync(_deadline.Token);
            banker = await Menu(world);
            Dialogue gold = await Pick(world, banker, "금화 맡기기", talk => talk.Kind == DialogueKind.TextInput);
            await Answer(world, banker, gold.Step, "400", talk => talk.What.Contains("맡았습니다"));
            await Until(() => world.Vitals?.Gold == 600, $"금화가 600 이 아닙니다: {world.Vitals?.Gold}");

            await Task.Delay(TimeSpan.FromSeconds(3), _deadline.Token); // 나갈 때 저장은 마지막 저장에서 2초 뒤부터
        }

        string path = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        await Until(() => Saved(path) is { } bank && (long?)bank["Gold"] == 400,
            "맡긴 금화가 캐릭터 파일에 남지 않았습니다.");

        WorldClient again = await LoginAgain(server, who);
        Creature oriana = await Menu(again);
        Assert.Contains("맡긴 금화 400전", again.Talking!.What);

        Dialogue list = await Pick(again, oriana, "물건 찾기", talk => talk.Kind == DialogueKind.Goods);
        Assert.Equal(10u, Assert.Single(list.Goods, goods => goods.Name == Potion).Price);
        Assert.Equal(1u, Assert.Single(list.Goods, goods => goods.Name == Sword).Price);

        Dialogue next = await Answer(again, oriana, list.Step, Sword, talk => talk.Kind == DialogueKind.Goods && talk.What.Contains("돌려드렸습니다"));
        await Until(() => again.Pack.Any(item => item.Name == Sword), "에페를 찾지 못했습니다.");
        Assert.DoesNotContain(next.Goods, goods => goods.Name == Sword);

        Dialogue ask = await Answer(again, oriana, next.Step, Potion, talk => talk.Kind == DialogueKind.TextInput);
        await Answer(again, oriana, ask.Step, "4", talk => talk.What.Contains("돌려드렸습니다"));
        await Until(() => Stacks(again, Potion) == 294, $"물약이 294 가 아닙니다: {Stacks(again, Potion)}");
        Assert.Equal(6u, Assert.Single(again.Talking!.Goods, goods => goods.Name == Potion).Price);

        await again.ShutDialogueAsync(_deadline.Token);
        oriana = await Menu(again);
        Dialogue take = await Pick(again, oriana, "금화 찾기", talk => talk.Kind == DialogueKind.TextInput);
        await Answer(again, oriana, take.Step, "400", talk => talk.What.Contains("돌려드렸습니다"));
        await Until(() => again.Vitals?.Gold == 1000, $"금화가 1000 이 아닙니다: {again.Vitals?.Gold}");
    }

    [Fact]
    public async Task A_full_pack_takes_nothing_out_and_the_bank_keeps_it()
    {
        const string who = "bankfull";
        using IsolatedHadesServer server = Ready(who, saved =>
        {
            for (int slot = 1; slot <= 150; slot++) Put(saved, slot, Sword, 1);
            saved["BankManager"] = new JsonObject
            {
                ["Items"] = new JsonObject { [Sword] = new JsonArray(new JsonObject { ["Template"] = new JsonObject { ["Name"] = Sword }, ["Stacks"] = 1 }) },
                ["Gold"] = 0
            };
        });

        using WorldSession session = await Login(server, who);
        WorldClient world = Pump(session);
        await Until(() => world.Pack.Count == 150, $"가방이 150칸이 아닙니다: {world.Pack.Count}");

        Creature banker = await Menu(world);
        Dialogue held = await Pick(world, banker, "물건 찾기", talk => talk.Kind == DialogueKind.Goods);
        Dialogue refused = await Answer(world, banker, held.Step, Sword, talk => talk.What.Contains("자리가 없"));

        Assert.Equal(1u, Assert.Single(refused.Goods, goods => goods.Name == Sword).Price);
        await Task.Delay(500, _deadline.Token);
        Assert.Equal(150, world.Pack.Count);

        // 한 칸 비우면(맡기면) 그때는 둘 다 찾힌다 — 막혔던 것이 사라지지 않았다.
        await world.ShutDialogueAsync(_deadline.Token);
        banker = await Menu(world);
        Dialogue slots = await Pick(world, banker, "물건 맡기기", talk => talk.Kind == DialogueKind.PackSlots);
        await Answer(world, banker, slots.Step, "150", talk => talk.What.Contains("맡았습니다"));
        await Until(() => world.Pack.Count == 149, "맡겼는데 가방이 그대로입니다.");

        await world.ShutDialogueAsync(_deadline.Token);
        banker = await Menu(world);
        held = await Pick(world, banker, "물건 찾기", talk => talk.Kind == DialogueKind.Goods);
        Assert.Equal(2u, Assert.Single(held.Goods, goods => goods.Name == Sword).Price);
        await Answer(world, banker, held.Step, Sword, talk => talk.What.Contains("돌려드렸습니다"));
        await Until(() => world.Pack.Count == 150, "찾았는데 가방이 차지 않았습니다.");
        Dialogue last = await Answer(world, banker, world.Talking!.Step, Sword, talk => talk.What.Contains("자리가 없"));
        Assert.Equal(1u, Assert.Single(last.Goods, goods => goods.Name == Sword).Price);
    }

    [Fact]
    public async Task Gold_outside_what_is_held_or_carried_is_refused_and_a_double_tap_deposits_once()
    {
        const string who = "bankgold";
        using IsolatedHadesServer server = Ready(who, saved =>
        {
            saved["GoldPoints"] = 100_000_000 - 10; // MaxCarryGold(LoruleConfig) 바로 밑
            saved["BankManager"] = new JsonObject { ["Items"] = new JsonObject(), ["Gold"] = 100 };
            Put(saved, 1, Potion, 20);
            Put(saved, 2, Sword, 1);
        });

        using WorldSession session = await Login(server, who);
        WorldClient world = Pump(session);
        Creature banker = await Menu(world);
        Assert.Contains("맡긴 금화 100전", world.Talking!.What);

        // 맡긴 것보다 많이 · 음수 · 숫자 아님 · 들 수 있는 한도 넘음 — 모두 거절, 금화 그대로.
        foreach (string typed in new[] { "101", "-5", "0", "abc", "50" })
        {
            Dialogue take = await Pick(world, banker, "금화 찾기", talk => talk.Kind == DialogueKind.TextInput);
            Dialogue said = await Answer(world, banker, take.Step, typed, talk => talk.Kind == DialogueKind.Options && talk != take);
            Assert.DoesNotContain("돌려드렸습니다", said.What);
        }

        Assert.Equal(100_000_000 - 10, world.Vitals!.Gold);

        Dialogue ten = await Pick(world, banker, "금화 찾기", talk => talk.Kind == DialogueKind.TextInput);
        await Answer(world, banker, ten.Step, "10", talk => talk.What.Contains("맡긴 금화 90전"));
        await Until(() => world.Vitals?.Gold == 100_000_000, $"금화가 한도까지 오지 않았습니다: {world.Vitals?.Gold}");

        Dialogue over = await Pick(world, banker, "금화 맡기기", talk => talk.Kind == DialogueKind.TextInput);
        await Answer(world, banker, over.Step, "100000001", talk => talk.What.Contains("가진 금화"));
        Assert.Equal(100_000_000, world.Vitals!.Gold);

        // 연타 — 수를 적은 답이 두 번 가도 한 번만 맡는다. 안 겹치는 물건 칸을 두 번 골라도 하나만.
        Dialogue slots = await Pick(world, banker, "물건 맡기기", talk => talk.Kind == DialogueKind.PackSlots);
        Dialogue howMany = await Answer(world, banker, slots.Step, "1", talk => talk.Kind == DialogueKind.TextInput);
        int seen = world.TalkCount;
        await Task.WhenAll(
            world.AnswerAsync(banker.Serial, howMany.Step, "5", _deadline.Token),
            world.AnswerAsync(banker.Serial, howMany.Step, "5", _deadline.Token));
        await Until(() => world.TalkCount >= seen + 2, "두 번 보낸 답에 창이 두 번 오지 않았습니다.");
        await Until(() => Stacks(world, Potion) == 15, $"물약이 15 가 아닙니다(두 번 맡았나?): {Stacks(world, Potion)}");

        Dialogue again = world.Talking!.Kind == DialogueKind.PackSlots
            ? world.Talking
            : await Pick(world, banker, "물건 맡기기", talk => talk.Kind == DialogueKind.PackSlots);
        seen = world.TalkCount;
        await Task.WhenAll(
            world.AnswerAsync(banker.Serial, again.Step, "2", _deadline.Token),
            world.AnswerAsync(banker.Serial, again.Step, "2", _deadline.Token));
        await Until(() => world.TalkCount >= seen + 2, "두 번 고른 칸에 창이 두 번 오지 않았습니다.");
        await Until(() => world.Pack.All(item => item.Name != Sword), "에페가 가방에 남았습니다.");

        await world.ShutDialogueAsync(_deadline.Token);
        banker = await Menu(world);
        Dialogue list = await Pick(world, banker, "물건 찾기", talk => talk.Kind == DialogueKind.Goods);
        Assert.Equal(5u, Assert.Single(list.Goods, goods => goods.Name == Potion).Price);
        Assert.Equal(1u, Assert.Single(list.Goods, goods => goods.Name == Sword).Price);
    }

    /// <summary>
    /// 앱의 은행 창은 상점 창과 같다(TalkPanel) — 여러 줄을 고르고 수량을 정해 [선택 맡기기]/[선택 찾기], 0xF2 일괄 거래로 간다.
    /// 수가 가진 것·맡긴 것보다 많으면 그만큼으로 줄이고, 같은 일괄을 두 번 보내도 없는 것을 또 꺼내지 않는다.
    /// </summary>
    [Fact]
    public async Task The_shop_like_window_puts_in_and_takes_out_many_at_once()
    {
        const string who = "bankbulk";
        using IsolatedHadesServer server = Ready(who, saved =>
        {
            Put(saved, 1, Potion, 300);
            Put(saved, 2, Sword, 1);
            Put(saved, 3, Sword, 1);
        });

        using WorldSession session = await Login(server, who);
        WorldClient world = Pump(session);
        Creature banker = await Menu(world);

        await Pick(world, banker, "물건 맡기기", talk => talk.Kind == DialogueKind.PackSlots);
        int seen = world.TalkCount;
        await world.BulkTradeAsync(banker.Serial, selling: true, [(Potion, 1, 999), (Sword, 2, 1), (Sword, 3, 1)], _deadline.Token);
        Dialogue put = await Window(world, seen, talk => talk.What.Contains("3종 302개를 맡았습니다"));
        Assert.Contains("맡길 수 있는 물건이 없습니다", put.What); // 가방이 비었으니 목록 대신 메뉴로
        await Until(() => world.Pack.Count == 0, $"가방이 비지 않았습니다: {world.Pack.Count}");

        await world.ShutDialogueAsync(_deadline.Token);
        banker = await Menu(world);
        await Pick(world, banker, "물건 찾기", talk => talk.Kind == DialogueKind.Goods);
        seen = world.TalkCount;
        await Task.WhenAll(
            world.BulkTradeAsync(banker.Serial, selling: false, [(Potion, 0, 250), (Sword, 0, 5)], _deadline.Token),
            world.BulkTradeAsync(banker.Serial, selling: false, [(Potion, 0, 250), (Sword, 0, 5)], _deadline.Token));
        await Until(() => world.TalkCount >= seen + 2, "두 번 보낸 일괄 찾기에 창이 두 번 오지 않았습니다.");
        await Until(() => Stacks(world, Potion) == 300 && world.Pack.Count(item => item.Name == Sword) == 2,
            $"물약 {Stacks(world, Potion)} · 에페 {world.Pack.Count(item => item.Name == Sword)} — 300·2 여야 합니다.");
        await Task.Delay(500, _deadline.Token);
        Assert.Equal(300, Stacks(world, Potion));
        Assert.Equal(2, world.Pack.Count(item => item.Name == Sword));
        Assert.Contains("맡기신 물건이 없습니다", world.Talking!.What);
    }

    // ---- 도우미 ----

    private IsolatedHadesServer Ready(string who, Action<JsonNode> seed)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (AbelBank, 5, 5));
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);
        CompanionCallTests.Edit(server, who, seed);
        return server;
    }

    private static void Put(JsonNode saved, int slot, string name, int stacks) =>
        saved["Inventory"]!["Items"]![slot.ToString()] = new JsonObject
        {
            ["Template"] = new JsonObject { ["Name"] = name },
            ["Slot"] = slot,
            ["Stacks"] = stacks,
            ["Durability"] = 100,
        };

    private static JsonNode? Saved(string path)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path))?["BankManager"];
        }
        catch (Exception)
        {
            return null; // 쓰는 도중
        }
    }

    private static int Stacks(WorldClient world, string name) =>
        world.Pack.Where(item => item.Name == name).Sum(item => Math.Max(1, item.Stacks));

    private Task<WorldSession> Login(IsolatedHadesServer server, string who) =>
        HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

    private WorldClient Pump(WorldSession session)
    {
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        return world;
    }

    private async Task<WorldClient> LoginAgain(IsolatedHadesServer server, string who)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            try
            {
                WorldClient world = Pump(await Login(server, who));
                await Until(() => world.State is not null, "다시 들어가지 못했습니다.");
                return world;
            }
            catch (Exception) when (DateTime.UtcNow < giveUp)
            {
                await Task.Delay(500, _deadline.Token);
            }
        }
    }

    /// <summary>오리아나를 눌러 첫 메뉴를 연다.</summary>
    private async Task<Creature> Menu(WorldClient world)
    {
        await Until(() => world.Creatures.Any(one => one.Where == Oriana), "오리아나가 보이지 않습니다.");
        Creature banker = world.Creatures.First(one => one.Where == Oriana);
        int seen = world.TalkCount;
        await world.ClickAsync(banker.Serial, _deadline.Token);
        await Window(world, seen, talk => talk.Options.Any(option => option.Text == "물건 맡기기"));
        return banker;
    }

    /// <summary>지금 창(또는 첫 메뉴)에서 그 글자로 시작하는 고르기를 누른다.</summary>
    private async Task<Dialogue> Pick(WorldClient world, Creature banker, string text, Func<Dialogue, bool> wanted)
    {
        DialogueOption option = world.Talking!.Options.First(one => one.Text.StartsWith(text));
        int seen = world.TalkCount;
        await world.AnswerAsync(banker.Serial, option.Step, _deadline.Token);
        return await Window(world, seen, wanted);
    }

    private async Task<Dialogue> Answer(WorldClient world, Creature banker, ushort step, string words, Func<Dialogue, bool> wanted)
    {
        int seen = world.TalkCount;
        await world.AnswerAsync(banker.Serial, step, words, _deadline.Token);
        return await Window(world, seen, wanted);
    }

    private async Task<Dialogue> Window(WorldClient world, int seen, Func<Dialogue, bool> wanted)
    {
        await Until(() => world.TalkCount > seen && world.Talking is { } talk && wanted(talk),
            $"기다린 창이 오지 않았습니다. 마지막 창: {world.Talking?.Kind} {world.Talking?.What}");
        return world.Talking!;
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
