using System.Net;
using System.Text.Json.Nodes;
using Lod.CompanionBot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 봇 2단계 — 주인이 봇 장비·포션을 채워 준다(0xF1 2·3 → 0x5E 종류 5), 봇은 주인 버프가 실제로 빠지면 다시 걸고(0x5E 종류 3),
/// 가방의 포션을 마신다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionKitTests : IDisposable
{
    private const string OwnerName = "kitowner";
    private const int NoviceVillage = 20373;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task The_owner_dresses_the_bot_and_hands_it_potions_within_the_wearing_rules()
    {
        using IsolatedHadesServer server = Ready(ownerLevel: 40);
        WorldClient owner = await Enter(server, OwnerName);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);
        await Call(owner, bot);

        // 봇 레벨 38 — 홀리파나(11) + 레더로브(11~40 남자 성직자 의상).
        await Until(() => Wears(bot, 1, "홀리파나") && Wears(bot, 2, "레더로브"), () => $"기본 장비: {Worn(bot)}");

        // 레벨이 모자라면 거절 — 맨틀은 41레벨.
        int mantle = await Give(owner, "맨틀");
        await owner.GiveToCompanionAsync(mantle, 0, _deadline.Token);
        await Until(() => owner.Said.Contains("41레벨부터", StringComparison.Ordinal), () => $"맨틀을 거절하지 않았습니다: {owner.Said}");
        Assert.Contains(owner.Pack, one => one.Name == "맨틀");

        // 직업이 다르면 거절 — 설단검은 도적 무기.
        int dagger = await Give(owner, "설단검");
        await owner.GiveToCompanionAsync(dagger, 0, _deadline.Token);
        await Until(() => owner.Said.Contains("성직자가 입을 수 없습니다", StringComparison.Ordinal), () => $"설단검을 거절하지 않았습니다: {owner.Said}");

        // 맞는 것은 입힌다 — 주인 가방에서 빠지고 봇이 입는다. 서버가 입힌 기본 홀리파나는 사라진다(주인에게 오지 않는다).
        int mercury = await Give(owner, "홀리머큐리아");
        await owner.GiveToCompanionAsync(mercury, 0, _deadline.Token);
        await Until(() => Wears(bot, 1, "홀리머큐리아") && !owner.Pack.Any(one => one.Name == "홀리머큐리아"),
            () => $"홀리머큐리아: 봇 {Worn(bot)} · 주인 말 {owner.Said}");
        Assert.DoesNotContain(owner.Pack, one => one.Name == "홀리파나");
        await Until(() => owner.CompanionKit?.Worn.Any(w => w.Slot == 1 && w.Name == "홀리머큐리아") == true, () => "사람의 봇 장비창이 바뀌지 않았습니다.");

        // 주인이 준 것 위에 또 주면 먼저 것은 주인 가방으로.
        int pana = await Give(owner, "홀리파나");
        await owner.GiveToCompanionAsync(pana, 0, _deadline.Token);
        await Until(() => Wears(bot, 1, "홀리파나") && owner.Pack.Any(one => one.Name == "홀리머큐리아"),
            () => $"바꿔 입히기: 봇 {Worn(bot)} · 주인 가방 {string.Join(",", owner.Pack.Select(p => p.Name))}");

        // 벗기기 — 주인이 준 것은 주인 가방으로(빈 무기 자리는 기본 것으로 다시 채운다), 서버가 입힌 기본 의상은 벗기지 않는다.
        await owner.TakeOffCompanionAsync(1, _deadline.Token);
        await Until(() => owner.Pack.Any(one => one.Name == "홀리파나") && Wears(bot, 1, "홀리파나"),
            () => $"벗기기: 봇 {Worn(bot)} · 주인 가방 {string.Join(",", owner.Pack.Select(p => p.Name))}");
        await owner.TakeOffCompanionAsync(2, _deadline.Token);
        await Until(() => owner.Said.Contains("기본 장비는 벗길 수 없습니다", StringComparison.Ordinal), () => owner.Said);

        // 포션 넘기기 — 열 개 중 넷.
        int kurum = await Give(owner, "쿠룸", 10);
        await owner.GiveToCompanionAsync(kurum, 4, _deadline.Token);
        await Until(() => AutoPotion.Count(bot.Pack, "쿠룸") == 4 && AutoPotion.Count(owner.Pack, "쿠룸") == 6,
            () => $"쿠룸: 봇 {AutoPotion.Count(bot.Pack, "쿠룸")} · 주인 {AutoPotion.Count(owner.Pack, "쿠룸")}");
        await Until(() => owner.CompanionKit?.Carried.Any(c => c.Name == "쿠룸" && c.Stacks == 4) == true, () => "봇 가방 포션이 사람에게 오지 않았습니다.");
    }

    [Fact]
    public async Task The_bot_recasts_a_buff_the_owner_lost_and_drinks_its_potions()
    {
        using IsolatedHadesServer server = Ready(ownerLevel: 40);
        WorldClient owner = await Enter(server, OwnerName);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName);

        List<string> did = [];
        // 포션 기준을 101% 로 — 가득 차 있어도 마신다. 판단(언제 마시나)은 알맹이 시험이 보고, 여기서는 마시면 서버가 한 병을 쓰는지 본다.
        CompanionRunner runner = new(bot, new MapWalls(HadesWorkspace.MapLayoutFolder),
            new CompanionSettings(PotionHealthPercent: 101, PotionManaPercent: 101), line =>
            {
                lock (did)
                {
                    did.Add(line);
                }
            });

        await Call(owner, bot);
        int kurum = await Give(owner, "쿠룸", 2);
        await owner.GiveToCompanionAsync(kurum, 0, _deadline.Token);
        int maradium = await Give(owner, "마라디움", 2);
        await owner.GiveToCompanionAsync(maradium, 0, _deadline.Token);
        await Until(() => AutoPotion.Count(bot.Pack, "쿠룸") == 2 && AutoPotion.Count(bot.Pack, "마라디움") == 2, () => "포션이 봇에게 가지 않았습니다.");

        _ = runner.RunAsync(_deadline.Token);

        // 체력 포션이 먼저(우선순위), 다 떨어지면 마력 포션.
        await Until(() => AutoPotion.Count(bot.Pack, "쿠룸") == 0 && AutoPotion.Count(bot.Pack, "마라디움") < 2,
            () => $"봇이 포션을 마시지 않았습니다: 쿠룸 {AutoPotion.Count(bot.Pack, "쿠룸")} · 마라디움 {AutoPotion.Count(bot.Pack, "마라디움")} · {Joined(did)}");

        // 주인에게 호르라마가 걸린다(서버가 알린 상태로 본다).
        await Until(() => bot.StatusesOf(owner.Serial)?.Any(s => s.Name == "horrama") == true,
            () => $"주인에게 호르라마가 걸리지 않았습니다: {Joined(did)}");

        // 주인도 제 상태를 받는다(내 판의 상태 아이콘 줄, 2026-09-26) — 이름과 그림 번호(호르라마 템플릿 Icon 11).
        await Until(() => owner.StatusesOf(owner.Serial)?.Any(s => s.Name == "horrama" && s.Icon == 11) == true,
            () => $"주인이 제 호르라마를 받지 못했습니다: {string.Join(",", owner.StatusesOf(owner.Serial)?.Select(s => $"{s.Name}/{s.Icon}") ?? [])}");

        // 봇의 상태도 주인에게 온다(봇 칸의 상태 아이콘 줄).
        await Until(() => owner.StatusesOf(bot.Serial) is not null, () => "봇의 상태가 주인에게 오지 않았습니다.");

        // 주인이 리베라토로 제 버프를 지운다 — 봇은 지속 시간(120초)을 기다리지 않고 곧 다시 건다.
        await owner.SayAsync("/spell \"리베라토\" 1", _deadline.Token);
        LearnedSpell? liberato = null;
        await Until(() => (liberato = owner.Spells.FirstOrDefault(s => CompanionSpells.Bare(s.Name) == "리베라토")) is not null, () => "리베라토를 받지 못했습니다.");
        // 외우기가 한 번에 안 먹을 때가 있다(앞 주문이 도는 중 등) — 지워질 때까지 2초마다 다시 외운다.
        bool gone = false;
        for (int tries = 0; tries < 10 && !gone; tries++)
        {
            await owner.UseSpellAsync(liberato!.Slot, owner.Serial, _deadline.Token);

            for (int wait = 0; wait < 40 && !gone; wait++)
            {
                gone = bot.StatusesOf(owner.Serial)?.Any(s => s.Name == "horrama") == false;
                await Task.Delay(50, _deadline.Token);
            }
        }

        Assert.True(gone, $"리베라토가 호르라마를 지우지 않았습니다: {owner.Said}");
        await Until(() => bot.StatusesOf(owner.Serial)?.Any(s => s.Name == "horrama") == true,
            () => $"빠진 호르라마를 다시 걸지 않았습니다: {Joined(did)}", TimeSpan.FromSeconds(15));
    }

    private IsolatedHadesServer Ready(int ownerLevel)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        CompanionCallTests.Configure(server);
        Waiting.MakeGameMaster(server, OwnerName);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);
        CompanionCallTests.Edit(server, OwnerName, saved => saved["ExpLevel"] = ownerLevel);
        return server;
    }

    private async Task Call(WorldClient owner, WorldClient bot)
    {
        for (int tries = 0; tries < 10 && bot.Master is null; tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        Assert.True(bot.Master?.Serial == owner.Serial, $"봇을 부르지 못했습니다: {owner.Said}");
    }

    /// <summary>운영자 명령으로 주인 가방에 넣고 그 칸을 돌려준다.</summary>
    private async Task<int> Give(WorldClient owner, string name, int count = 1)
    {
        int before = AutoPotion.Count(owner.Pack, name);
        await owner.SayAsync($"/give \"{name}\" {count}", _deadline.Token);
        await Until(() => AutoPotion.Count(owner.Pack, name) > before, () => $"{name} 이 가방에 오지 않았습니다: {owner.Said}");
        return owner.Pack.Last(one => one.Name == name).Slot;
    }

    private static bool Wears(WorldClient bot, int slot, string name) => bot.Worn.Any(w => w.Slot == slot && w.Name == name);

    private static string Worn(WorldClient bot) => string.Join(",", bot.Worn.Select(w => $"{w.Slot}:{w.Name}"));

    private static string Joined(List<string> lines)
    {
        lock (lines)
        {
            return string.Join(" | ", lines);
        }
    }

    private async Task Until(Func<bool> condition, Func<string> failure, TimeSpan? within = null)
    {
        DateTime giveUp = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(failure());
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State?.Map.Id == NoviceVillage && world.Serial != 0, () => $"{who} 가 노비스마을에 서지 못했습니다.");
        return world;
    }
}
